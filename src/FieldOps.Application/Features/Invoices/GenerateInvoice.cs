using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Invoicing;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Invoices;

public sealed record GenerateInvoiceCommand(Guid WorkOrderId);

/// <summary>
/// US-BIL-01: a draft from a completed work order, billing the logged work time and the parts used. A second live invoice for the
/// same job is refused here and, for two requests racing each other, by the partial unique index on <c>work_order_id</c>.
/// </summary>
public sealed class GenerateInvoiceHandler(IAppDbContext db, InvoiceReader reader)
    : ICommandHandler<GenerateInvoiceCommand, Result<InvoiceDto>>
{
    public async Task<Result<InvoiceDto>> Handle(GenerateInvoiceCommand cmd, CancellationToken ct)
    {
        var workOrder = await db.WorkOrders.AsNoTracking().FirstOrDefaultAsync(w => w.Id == cmd.WorkOrderId, ct);
        if (workOrder is null) return WorkOrderErrors.NotFound;
        if (await db.Invoices.AnyAsync(i => i.WorkOrderId == workOrder.Id && i.Status != InvoiceStatus.Void, ct))
            return InvoiceErrors.AlreadyInvoiced;

        var workedMinutes = await db.TimeEntries
            .Where(e => e.WorkOrderId == workOrder.Id && e.Type == TimeEntryType.Work && e.DurationMinutes != null)
            .SumAsync(e => e.DurationMinutes!.Value, ct);
        var parts = await db.WorkOrderParts.AsNoTracking()
            .Where(l => l.WorkOrderId == workOrder.Id)
            .Join(db.Parts, l => l.PartId, p => p.Id, (l, p) => new { l.Id, l.Quantity, l.UnitPrice, p.Sku, p.Name })
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
        var settings = await db.AppSettings.AsNoTracking().SingleAsync(ct);

        var invoice = Invoice.Generate(workOrder, workedMinutes, settings.LaborRatePerHour, settings.VatRate,
            parts.Select(p => new BillablePart($"{p.Name} ({p.Sku})", p.Quantity, p.UnitPrice)));
        if (invoice.IsFailure) return invoice.Error;

        db.Invoices.Add(invoice.Value);
        var saved = await InvoiceStore.SaveAsync(db, ct, InvoiceErrors.AlreadyInvoiced);
        if (saved.IsFailure) return saved.Error;
        return await reader.ReadAsync(invoice.Value.Id, ct);
    }
}
