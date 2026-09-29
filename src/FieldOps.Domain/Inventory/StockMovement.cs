using FieldOps.Domain.Common;

namespace FieldOps.Domain.Inventory;

public enum StockMovementType { Receive, Transfer, Consume, Return, Adjust }

/// <summary>An append-only record of stock moving into, out of or between locations (US-INV-06).</summary>
public sealed class StockMovement : Entity
{
    private StockMovement() { }

    internal StockMovement(
        Guid partId, StockMovementType type, Guid? fromLocationId, Guid? toLocationId, decimal quantity, Guid? workOrderId,
        string? reason, Guid createdBy, DateTimeOffset createdAt)
    {
        PartId = partId;
        Type = type;
        FromLocationId = fromLocationId;
        ToLocationId = toLocationId;
        Quantity = quantity;
        WorkOrderId = workOrderId;
        Reason = reason;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public Guid PartId { get; private init; }
    public StockMovementType Type { get; private init; }
    public Guid? FromLocationId { get; private init; }
    public Guid? ToLocationId { get; private init; }

    /// <summary>Always positive; the direction is given by the from and to locations.</summary>
    public decimal Quantity { get; private init; }

    public Guid? WorkOrderId { get; private init; }
    public string? Reason { get; private init; }
    public Guid CreatedBy { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }
}
