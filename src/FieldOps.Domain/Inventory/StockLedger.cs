using FieldOps.Domain.Common;

namespace FieldOps.Domain.Inventory;

/// <summary>
/// Every stock change goes through here: it updates the levels and returns the movement to store with them,
/// so levels and history always agree and no level goes below zero.
/// </summary>
public static class StockLedger
{
    public const int QuantityDecimals = 2;

    public static Result<StockMovement> Receive(StockLevel into, decimal quantity, Guid userId, DateTimeOffset now)
    {
        if (!IsValidQuantity(quantity)) return InventoryErrors.InvalidQuantity;
        into.Quantity += quantity;
        return new StockMovement(into.PartId, StockMovementType.Receive, null, into.StockLocationId, quantity, null, null, userId, now);
    }

    public static Result<StockMovement> Transfer(StockLevel from, StockLevel to, decimal quantity, Guid userId, DateTimeOffset now)
    {
        if (!IsValidQuantity(quantity)) return InventoryErrors.InvalidQuantity;
        if (from.PartId != to.PartId) throw new ArgumentException("Both levels must be for the same part.");
        if (from.StockLocationId == to.StockLocationId) return InventoryErrors.SameLocation;
        if (from.Quantity < quantity) return InventoryErrors.Insufficient(from.Quantity);

        from.Quantity -= quantity;
        to.Quantity += quantity;
        return new StockMovement(from.PartId, StockMovementType.Transfer, from.StockLocationId, to.StockLocationId, quantity, null, null,
            userId, now);
    }

    /// <summary>Takes parts out of a van for a job (US-TAPP-07 AC2/AC3).</summary>
    public static Result<StockMovement> Consume(StockLevel from, decimal quantity, Guid workOrderId, Guid userId, DateTimeOffset now)
    {
        if (!IsValidQuantity(quantity)) return InventoryErrors.InvalidQuantity;
        if (from.Quantity < quantity) return InventoryErrors.Insufficient(from.Quantity);

        from.Quantity -= quantity;
        return new StockMovement(from.PartId, StockMovementType.Consume, from.StockLocationId, null, quantity, workOrderId, null, userId, now);
    }

    /// <summary>Puts parts removed from a job back where they came from (US-TAPP-07 AC4).</summary>
    public static Result<StockMovement> Return(StockLevel into, decimal quantity, Guid workOrderId, Guid userId, DateTimeOffset now)
    {
        if (!IsValidQuantity(quantity)) return InventoryErrors.InvalidQuantity;
        into.Quantity += quantity;
        return new StockMovement(into.PartId, StockMovementType.Return, null, into.StockLocationId, quantity, workOrderId, null, userId, now);
    }

    /// <summary>Sets the counted quantity (US-INV-05); the movement records the difference and the reason.</summary>
    public static Result<StockMovement> Adjust(StockLevel level, decimal newQuantity, string? reason, Guid userId, DateTimeOffset now)
    {
        if (newQuantity < 0 || decimal.Round(newQuantity, QuantityDecimals) != newQuantity) return InventoryErrors.InvalidQuantity;
        if (string.IsNullOrWhiteSpace(reason)) return InventoryErrors.ReasonRequired;
        var delta = newQuantity - level.Quantity;
        if (delta == 0) return InventoryErrors.NoChange;

        level.Quantity = newQuantity;
        return new StockMovement(level.PartId, StockMovementType.Adjust,
            delta < 0 ? level.StockLocationId : null, delta > 0 ? level.StockLocationId : null,
            Math.Abs(delta), null, reason.Trim(), userId, now);
    }

    public static bool IsValidQuantity(decimal quantity) =>
        quantity > 0 && quantity <= 99_999_999.99m && decimal.Round(quantity, QuantityDecimals) == quantity;
}
