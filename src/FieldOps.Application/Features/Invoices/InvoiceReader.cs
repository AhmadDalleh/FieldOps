using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Common;
using FieldOps.Domain.Invoicing;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Invoices;

public sealed record InvoiceLineDto(Guid Id, InvoiceLineType LineType, string Description, decimal Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record InvoiceDto(
    Guid Id,
    string? Number,
    InvoiceStatus Status,
    Guid WorkOrderId,
    string WorkOrderNumber,
    string WorkOrderTitle,
    Guid CustomerId,
    string CustomerName,
    DateOnly? IssueDate,
    DateOnly? DueDate,
    bool IsOverdue,
    decimal Subtotal,
    decimal VatRate,
    decimal VatAmount,
    decimal Total,
    string Currency,
    DateOnly? PaidAt,
    string? PaymentReference,
    string? VoidReason,
    DateTimeOffset CreatedAt,
    IReadOnlyList<InvoiceLineDto> Lines,
    uint Version);

/// <summary>Builds invoice DTOs with the work order and customer they belong to.</summary>
public sealed class InvoiceReader(IAppDbContext db, TimeProvider clock)
{
    public async Task<InvoiceDto> ReadAsync(Guid id, CancellationToken ct)
    {
        var i = await db.Invoices.AsNoTracking().Include(x => x.Lines).SingleAsync(x => x.Id == id, ct);
        var job = await db.WorkOrders.AsNoTracking().Where(w => w.Id == i.WorkOrderId).Select(w => new { w.Number, w.Title }).SingleAsync(ct);
        var customer = await db.Customers.AsNoTracking().Where(c => c.Id == i.CustomerId).Select(c => c.Name).SingleAsync(ct);
        var currency = await db.AppSettings.AsNoTracking().Select(s => s.Currency).FirstOrDefaultAsync(ct) ?? "AED";

        return new InvoiceDto(
            i.Id, i.Number, i.Status, i.WorkOrderId, job.Number, job.Title, i.CustomerId, customer, i.IssueDate, i.DueDate,
            i.IsOverdue(clock.Today()), i.Subtotal, i.VatRate, i.VatAmount, i.Total, currency, i.PaidAt, i.PaymentReference,
            i.VoidReason, i.CreatedAt,
            i.Lines.OrderBy(l => l.Position).Select(l => new InvoiceLineDto(l.Id, l.LineType, l.Description, l.Quantity, l.UnitPrice, l.LineTotal)).ToList(),
            i.Version);
    }
}

internal static class InvoiceStore
{
    /// <summary>Loads a tracked invoice with its lines.</summary>
    public static async Task<Result<Invoice>> LoadAsync(IAppDbContext db, Guid id, CancellationToken ct)
    {
        var invoice = await db.Invoices.Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == id, ct);
        return invoice is null ? InvoiceErrors.NotFound : invoice;
    }

    /// <summary>
    /// Saves, turning a clash into a 409: a stale <c>xmin</c> on the invoice or work order, or the unique index that allows one
    /// live invoice per work order (<paramref name="onConflict"/>).
    /// </summary>
    public static async Task<Result> SaveAsync(IAppDbContext db, CancellationToken ct, Error? onConflict = null)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return InvoiceErrors.ConcurrencyConflict;
        }
        catch (DbUpdateException) when (onConflict is not null)
        {
            return onConflict;
        }
    }
}
