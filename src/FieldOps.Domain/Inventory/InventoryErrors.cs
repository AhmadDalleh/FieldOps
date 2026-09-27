using FieldOps.Domain.Common;

namespace FieldOps.Domain.Inventory;

public static class InventoryErrors
{
    public static readonly Error PartNotFound = Error.NotFound("Part.NotFound", "The part was not found.");
    public static readonly Error PartNotAvailable = Error.Validation("Part.NotAvailable", "The part was not found or is inactive.");
    public static readonly Error DuplicateSku = Error.Conflict("Part.DuplicateSku", "Another part already uses this SKU.");
    public static readonly Error NegativeAmount = Error.Validation("Part.NegativeAmount",
        "Cost, price and reorder level cannot be negative.");
    public static readonly Error LocationNotFound = Error.Validation("Stock.LocationNotFound",
        "The stock location was not found or is inactive.");
    public static readonly Error ReceiveIntoWarehouse = Error.Validation("Stock.ReceiveIntoWarehouse",
        "Stock is received into a warehouse.");
    public static readonly Error InvalidQuantity = Error.Validation("Stock.InvalidQuantity",
        "Quantities must be more than 0 with at most 2 decimals.");
    public static readonly Error SameLocation = Error.Validation("Stock.SameLocation", "Choose two different locations.");
    public static readonly Error ReasonRequired = Error.Validation("Stock.ReasonRequired", "Say why the stock is being adjusted.");
    public static readonly Error NoChange = Error.Validation("Stock.NoChange", "The counted quantity is the same as the current stock.");
    public static readonly Error ConcurrencyConflict = Error.Conflict("Stock.ConcurrencyConflict",
        "The stock changed while you were saving. Try again.");
    public static readonly Error NoVan = Error.Conflict("Stock.NoVan", "The job has no technician with a van to take parts from.");

    public static Error Insufficient(decimal available) =>
        Error.Conflict("Stock.Insufficient", $"Only {available:0.##} in stock at this location.");
}
