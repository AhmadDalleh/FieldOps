namespace FieldOps.Domain.Invoicing;

/// <summary>The money rules of an invoice (US-BIL-01 AC2 and AC4), kept in one place so they are tested once.</summary>
public static class InvoiceCalculator
{
    /// <summary>Rounds money half away from zero to 2 decimals (so 9.375 becomes 9.38).</summary>
    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    /// <summary>Worked minutes as billable hours, rounded up to the next quarter hour (70 min is 1.25 h).</summary>
    public static decimal LaborHours(int minutes) => minutes <= 0 ? 0 : Math.Ceiling(minutes / 15m) / 4m;

    public static decimal LineTotal(decimal quantity, decimal unitPrice) => Round(quantity * unitPrice);

    public static decimal Vat(decimal subtotal, decimal vatRate) => Round(subtotal * vatRate / 100m);

    /// <summary>Subtotal of the (already rounded) line totals, VAT on the subtotal, and their sum.</summary>
    public static (decimal Subtotal, decimal Vat, decimal Total) Totals(IEnumerable<decimal> lineTotals, decimal vatRate)
    {
        var subtotal = lineTotals.Sum();
        var vat = Vat(subtotal, vatRate);
        return (subtotal, vat, subtotal + vat);
    }
}
