using FieldOps.Domain.Common;
using FieldOps.Domain.WorkOrders;

namespace FieldOps.Domain.Invoicing;

public enum InvoiceStatus { Draft, Issued, Paid, Void }

/// <summary>A part line to bill, taken from a <c>work_order_parts</c> row with its snapshot price (US-BIL-01 AC3).</summary>
public sealed record BillablePart(string Description, decimal Quantity, decimal UnitPrice);

/// <summary>The bill for one completed work order (docs/07-flows.md §2: Draft → Issued → Paid, or Issued → Void).</summary>
public sealed class Invoice : AuditableEntity
{
    public const string NumberSequence = "Invoice";

    private readonly List<InvoiceLine> _lines = [];

    private Invoice() { }

    /// <summary>Assigned when the invoice is issued; drafts have none.</summary>
    public string? Number { get; private set; }
    public Guid WorkOrderId { get; private init; }
    public Guid CustomerId { get; private init; }
    public InvoiceStatus Status { get; private set; }
    public DateOnly? IssueDate { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal VatRate { get; private init; }
    public decimal VatAmount { get; private set; }
    public decimal Total { get; private set; }
    public DateOnly? PaidAt { get; private set; }
    public string? PaymentReference { get; private set; }
    public string? VoidReason { get; private set; }

    /// <summary>The PDF rendered when the invoice was issued, so it never changes afterwards.</summary>
    public string? PdfKey { get; private set; }

    /// <summary>Maps to the Postgres <c>xmin</c> system column for optimistic concurrency.</summary>
    public uint Version { get; private set; }

    public IReadOnlyList<InvoiceLine> Lines => _lines;

    public bool IsDraft => Status == InvoiceStatus.Draft;

    /// <summary>Issued and past its due date (US-BIL-07).</summary>
    public bool IsOverdue(DateOnly today) => Status == InvoiceStatus.Issued && DueDate < today;

    /// <summary>
    /// US-BIL-01: a draft with a labor line (worked minutes rounded up to a quarter hour × the labor rate) and one line per part used.
    /// The "one non-void invoice per work order" rule needs the database, so the handler checks it.
    /// </summary>
    public static Result<Invoice> Generate(
        WorkOrder workOrder, int workedMinutes, decimal laborRate, decimal vatRate, IEnumerable<BillablePart> parts)
    {
        if (workOrder.Status != WorkOrderStatus.Completed) return InvoiceErrors.WorkOrderNotCompleted;

        var invoice = new Invoice
        {
            WorkOrderId = workOrder.Id,
            CustomerId = workOrder.CustomerId,
            Status = InvoiceStatus.Draft,
            VatRate = vatRate,
        };
        var hours = InvoiceCalculator.LaborHours(workedMinutes);
        if (hours > 0)
            invoice._lines.Add(InvoiceLine.Create(invoice.Id, invoice.NextPosition, InvoiceLineType.Labor, $"Labor ({hours:0.##} h)", hours, laborRate).Value);
        foreach (var part in parts)
        {
            var line = InvoiceLine.Create(invoice.Id, invoice.NextPosition, InvoiceLineType.Part, part.Description, part.Quantity, part.UnitPrice);
            if (line.IsFailure) return line.Error;
            invoice._lines.Add(line.Value);
        }
        invoice.Recalculate();
        return invoice;
    }

    /// <summary>US-BIL-02: adds a line to a draft; a negative Other line is a discount, but the total stays at or above zero.</summary>
    public Result<InvoiceLine> AddLine(InvoiceLineType type, string description, decimal quantity, decimal unitPrice)
    {
        if (!IsDraft) return InvoiceErrors.NotDraft;
        var line = InvoiceLine.Create(Id, NextPosition, type, description, quantity, unitPrice);
        if (line.IsFailure) return line.Error;
        if (TotalWith(_lines.Append(line.Value)) < 0) return InvoiceErrors.NegativeTotal;

        _lines.Add(line.Value);
        Recalculate();
        return line.Value;
    }

    public Result UpdateLine(Guid lineId, InvoiceLineType type, string description, decimal quantity, decimal unitPrice)
    {
        if (!IsDraft) return InvoiceErrors.NotDraft;
        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null) return InvoiceErrors.LineNotFound;
        var candidate = InvoiceLine.Create(Id, line.Position, type, description, quantity, unitPrice);
        if (candidate.IsFailure) return candidate.Error;
        if (TotalWith(_lines.Select(l => l == line ? candidate.Value : l)) < 0) return InvoiceErrors.NegativeTotal;

        line.Set(type, description, quantity, unitPrice);
        Recalculate();
        return Result.Success();
    }

    public Result RemoveLine(Guid lineId)
    {
        if (!IsDraft) return InvoiceErrors.NotDraft;
        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null) return InvoiceErrors.LineNotFound;
        if (TotalWith(_lines.Where(l => l != line)) < 0) return InvoiceErrors.NegativeTotal;

        _lines.Remove(line);
        Recalculate();
        return Result.Success();
    }

    /// <summary>
    /// US-BIL-03: gives the draft its number, today's issue date and a due date, and moves the work order to Invoiced.
    /// </summary>
    public Result Issue(long number, DateOnly today, int dueDays, WorkOrder workOrder, Guid userId, DateTimeOffset now)
    {
        if (!IsDraft) return InvoiceErrors.NotDraft;
        if (_lines.Count == 0) return InvoiceErrors.Empty;
        if (workOrder.Id != WorkOrderId) throw new ArgumentException("Issue with this invoice's work order.");
        var invoiced = workOrder.MarkInvoiced(userId, now);
        if (invoiced.IsFailure) return invoiced.Error;

        Number = FormatNumber(number);
        Status = InvoiceStatus.Issued;
        IssueDate = today;
        DueDate = today.AddDays(dueDays);
        return Result.Success();
    }

    public void AttachPdf(string key) => PdfKey = key;

    /// <summary>US-BIL-05: records the payment of an issued invoice.</summary>
    public Result MarkPaid(DateOnly paidAt, string reference, DateOnly today)
    {
        if (Status != InvoiceStatus.Issued) return InvoiceErrors.NotIssued;
        reference = reference?.Trim() ?? "";
        if (reference.Length == 0) return InvoiceErrors.ReferenceRequired;
        if (paidAt < IssueDate || paidAt > today) return InvoiceErrors.InvalidPaidDate;

        Status = InvoiceStatus.Paid;
        PaidAt = paidAt;
        PaymentReference = reference;
        return Result.Success();
    }

    /// <summary>US-BIL-06: voids an issued invoice and returns the work order to Completed so it can be invoiced again.</summary>
    public Result Void(string reason, WorkOrder workOrder, Guid userId, DateTimeOffset now)
    {
        if (Status == InvoiceStatus.Paid) return InvoiceErrors.PaidCannotBeVoided;
        if (Status != InvoiceStatus.Issued) return InvoiceErrors.NotIssued;
        reason = reason?.Trim() ?? "";
        if (reason.Length == 0) return InvoiceErrors.ReasonRequired;
        if (workOrder.Id != WorkOrderId) throw new ArgumentException("Void with this invoice's work order.");
        var reopened = workOrder.VoidInvoice(userId, now);
        if (reopened.IsFailure) return reopened.Error;

        Status = InvoiceStatus.Void;
        VoidReason = reason;
        return Result.Success();
    }

    public static string FormatNumber(long number) => $"INV-{number:D6}";

    private int NextPosition => _lines.Count == 0 ? 1 : _lines.Max(l => l.Position) + 1;

    private decimal TotalWith(IEnumerable<InvoiceLine> lines) => InvoiceCalculator.Totals(lines.Select(l => l.LineTotal), VatRate).Total;

    private void Recalculate() => (Subtotal, VatAmount, Total) = InvoiceCalculator.Totals(_lines.Select(l => l.LineTotal), VatRate);
}
