using FieldOps.Domain.Common;

namespace FieldOps.Domain.Invoicing;

public enum InvoiceLineType { Labor, Part, Other }

public sealed class InvoiceLine : Entity
{
    private InvoiceLine() { }

    public Guid InvoiceId { get; private init; }

    /// <summary>Where the line prints on the invoice, in the order lines were added.</summary>
    public int Position { get; private init; }
    public InvoiceLineType LineType { get; private set; }
    public string Description { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal { get; private set; }

    internal static Result<InvoiceLine> Create(
        Guid invoiceId, int position, InvoiceLineType type, string description, decimal quantity, decimal unitPrice)
    {
        var line = new InvoiceLine { InvoiceId = invoiceId, Position = position };
        var set = line.Set(type, description, quantity, unitPrice);
        return set.IsFailure ? set.Error : line;
    }

    internal Result Set(InvoiceLineType type, string description, decimal quantity, decimal unitPrice)
    {
        description = description?.Trim() ?? "";
        if (description.Length is 0 or > 300 || quantity <= 0 || HasMoreThanTwoDecimals(quantity) || HasMoreThanTwoDecimals(unitPrice)
            || (unitPrice < 0 && type != InvoiceLineType.Other))
            return InvoiceErrors.InvalidLine;

        LineType = type;
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        LineTotal = InvoiceCalculator.LineTotal(quantity, unitPrice);
        return Result.Success();
    }

    private static bool HasMoreThanTwoDecimals(decimal value) => decimal.Round(value, 2) != value;
}
