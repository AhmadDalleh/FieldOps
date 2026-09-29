using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Customers;
using FieldOps.Domain.Invoicing;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Invoices;

public sealed record InvoicePdfFile(Stream Content, string FileName);

public sealed record GetInvoicePdfQuery(Guid Id);

/// <summary>
/// US-BIL-04: an issued or paid invoice returns the PDF stored when it was issued, so later changes to settings or the job never
/// alter it. A draft or a voided invoice is rendered on request with a DRAFT or VOID banner.
/// </summary>
public sealed class GetInvoicePdfHandler(IAppDbContext db, IFileStorage files, InvoicePdfBuilder builder)
    : IQueryHandler<GetInvoicePdfQuery, Result<InvoicePdfFile>>
{
    public async Task<Result<InvoicePdfFile>> Handle(GetInvoicePdfQuery query, CancellationToken ct)
    {
        var invoice = await db.Invoices.AsNoTracking().Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == query.Id, ct);
        if (invoice is null) return InvoiceErrors.NotFound;

        var workOrderNumber = await db.WorkOrders.Where(w => w.Id == invoice.WorkOrderId).Select(w => w.Number).SingleAsync(ct);
        var fileName = $"{invoice.Number ?? "Draft-" + workOrderNumber}.pdf";
        if (invoice.Status is InvoiceStatus.Issued or InvoiceStatus.Paid && invoice.PdfKey is { } key
            && await files.OpenReadAsync(key, ct) is { } stored)
            return new InvoicePdfFile(stored, fileName);

        return new InvoicePdfFile(new MemoryStream(await builder.RenderAsync(invoice, ct)), fileName);
    }
}

/// <summary>Gathers the company, customer and job details for an invoice and renders its PDF.</summary>
public sealed class InvoicePdfBuilder(IAppDbContext db, IFileStorage files, IInvoicePdfRenderer renderer)
{
    public async Task<byte[]> RenderAsync(Invoice invoice, CancellationToken ct)
    {
        var settings = await db.AppSettings.AsNoTracking().SingleAsync(ct);
        var customer = await db.Customers.AsNoTracking().SingleAsync(c => c.Id == invoice.CustomerId, ct);
        var workOrder = await db.WorkOrders.AsNoTracking().SingleAsync(w => w.Id == invoice.WorkOrderId, ct);
        var site = await db.Sites.AsNoTracking().SingleAsync(s => s.Id == workOrder.SiteId, ct);
        var notes = await db.WorkOrderNotes.AsNoTracking()
            .Where(n => n.WorkOrderId == workOrder.Id && !n.IsInternal)
            .OrderBy(n => n.CreatedAt)
            .Select(n => new InvoicePdfNote(n.CreatedAt, n.Body))
            .ToListAsync(ct);
        var signatureKey = workOrder.SignatureAttachmentId is { } signatureId
            ? await db.Attachments.Where(a => a.Id == signatureId).Select(a => a.StorageKey).FirstOrDefaultAsync(ct)
            : null;

        var model = new InvoicePdfModel(
            new InvoicePdfCompany(settings.CompanyName, settings.CompanyAddress, settings.Trn, settings.Currency,
                await ReadAsync(settings.LogoKey, ct)),
            invoice.Number is { } number ? $"Tax invoice {number}" : "Tax invoice",
            invoice.Status switch { InvoiceStatus.Draft => "DRAFT", InvoiceStatus.Void => "VOID", InvoiceStatus.Paid => "PAID", _ => null },
            invoice.IssueDate,
            invoice.DueDate,
            invoice.Lines.OrderBy(l => l.Position).Select(l => new InvoicePdfLine(l.Description, l.Quantity, l.UnitPrice, l.LineTotal)).ToList(),
            invoice.Subtotal,
            invoice.VatRate,
            invoice.VatAmount,
            invoice.Total,
            new InvoicePdfCustomer(customer.Name, customer.Code, customer.BillingAddress, customer.TaxRegistrationNumber, customer.Phone,
                customer.Email),
            new InvoicePdfJob(workOrder.Number, workOrder.Title, Address(site), workOrder.CompletedAt, workOrder.CompletionNotes, notes,
                workOrder.SignedByName, await ReadAsync(signatureKey, ct)));
        return renderer.Render(model);
    }

    private static string Address(Site s) =>
        string.Join(", ", new[] { s.Name, s.AddressLine1, s.AddressLine2, s.City }.Where(x => !string.IsNullOrWhiteSpace(x)));

    private async Task<byte[]?> ReadAsync(string? key, CancellationToken ct)
    {
        if (key is null) return null;
        await using var stream = await files.OpenReadAsync(key, ct);
        if (stream is null) return null;
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }
}
