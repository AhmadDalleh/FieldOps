using FieldOps.Domain.Common;

namespace FieldOps.Domain.Inventory;

public enum PartUnit { Pcs, M, Kg, L }

/// <summary>A catalog item that can be stocked and used on jobs (US-INV-01).</summary>
public sealed class Part : AuditableEntity
{
    private Part() { }

    public string Sku { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public PartUnit Unit { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal ReorderLevel { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>SKUs are compared trimmed and upper-case so "flt-01" and "FLT-01" are the same part.</summary>
    public static string NormalizeSku(string sku) => sku.Trim().ToUpperInvariant();

    public static Result<Part> Create(PartDetails details)
    {
        var part = new Part();
        var result = part.Update(details);
        return result.IsSuccess ? part : result.Error;
    }

    public Result Update(PartDetails details)
    {
        if (details.UnitCost < 0 || details.UnitPrice < 0 || details.ReorderLevel < 0) return InventoryErrors.NegativeAmount;

        Sku = NormalizeSku(details.Sku);
        Name = details.Name.Trim();
        Description = string.IsNullOrWhiteSpace(details.Description) ? null : details.Description.Trim();
        Unit = details.Unit;
        UnitCost = details.UnitCost;
        UnitPrice = details.UnitPrice;
        ReorderLevel = details.ReorderLevel;
        IsActive = details.IsActive;
        return Result.Success();
    }

    /// <summary>Low stock: the total across all locations is below the reorder level (US-INV-02).</summary>
    public bool IsLow(decimal totalQuantity) => totalQuantity < ReorderLevel;
}

public sealed record PartDetails(
    string Sku, string Name, string? Description, PartUnit Unit, decimal UnitCost, decimal UnitPrice, decimal ReorderLevel,
    bool IsActive = true);
