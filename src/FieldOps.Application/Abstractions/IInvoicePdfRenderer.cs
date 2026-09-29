namespace FieldOps.Application.Abstractions;

/// <summary>Everything printed on an invoice PDF (US-BIL-04), gathered by the handler so the renderer does no data access.</summary>
public sealed record InvoicePdfModel(
    InvoicePdfCompany Company,
    string Title,
    string? Banner,
    DateOnly? IssueDate,
    DateOnly? DueDate,
    IReadOnlyList<InvoicePdfLine> Lines,
    decimal Subtotal,
    decimal VatRate,
    decimal VatAmount,
    decimal Total,
    InvoicePdfCustomer Customer,
    InvoicePdfJob Job);

public sealed record InvoicePdfCompany(string Name, string? Address, string? Trn, string Currency, byte[]? Logo);

public sealed record InvoicePdfCustomer(string Name, string Code, string? BillingAddress, string? Trn, string Phone, string? Email);

public sealed record InvoicePdfLine(string Description, decimal Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record InvoicePdfJob(
    string Number,
    string Title,
    string SiteAddress,
    DateTimeOffset? CompletedAt,
    string? CompletionNotes,
    IReadOnlyList<InvoicePdfNote> Notes,
    string? SignedByName,
    byte[]? Signature);

public sealed record InvoicePdfNote(DateTimeOffset At, string Body);

public interface IInvoicePdfRenderer
{
    byte[] Render(InvoicePdfModel model);
}
