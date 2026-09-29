using FieldOps.Domain.Common;

namespace FieldOps.Domain.Invoicing;

public static class InvoiceErrors
{
    public static readonly Error NotFound = Error.NotFound("Invoice.NotFound", "The invoice was not found.");
    public static readonly Error LineNotFound = Error.NotFound("Invoice.LineNotFound", "The invoice line was not found.");
    public static readonly Error WorkOrderNotCompleted = Error.Conflict("Invoice.WorkOrderNotCompleted",
        "Only a completed work order can be invoiced.");
    public static readonly Error AlreadyInvoiced = Error.Conflict("Invoice.AlreadyInvoiced",
        "This work order already has an invoice. Void it before creating another.");
    public static readonly Error NotDraft = Error.Conflict("Invoice.NotDraft", "Only a draft invoice can be changed.");
    public static readonly Error NotIssued = Error.Conflict("Invoice.NotIssued", "Only an issued invoice can be paid or voided.");
    public static readonly Error PaidCannotBeVoided = Error.Conflict("Invoice.PaidCannotBeVoided", "A paid invoice cannot be voided.");
    public static readonly Error Empty = Error.Validation("Invoice.Empty", "Add at least one line before issuing.");
    public static readonly Error NegativeTotal = Error.Validation("Invoice.NegativeTotal", "The invoice total cannot be negative.");
    public static readonly Error InvalidLine = Error.Validation("Invoice.InvalidLine",
        "A line needs a description, a quantity above 0 and a price; only Other lines may have a negative price.");
    public static readonly Error ReasonRequired = Error.Validation("Invoice.ReasonRequired", "Say why the invoice is being voided.");
    public static readonly Error ReferenceRequired = Error.Validation("Invoice.ReferenceRequired", "Enter the payment reference.");
    public static readonly Error InvalidPaidDate = Error.Validation("Invoice.InvalidPaidDate",
        "The payment date must be between the issue date and today.");
    public static readonly Error ConcurrencyConflict = Error.Conflict("Invoice.ConcurrencyConflict",
        "The invoice changed while you were saving. Reload and try again.");
}
