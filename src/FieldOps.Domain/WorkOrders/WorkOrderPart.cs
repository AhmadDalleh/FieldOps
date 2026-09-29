using FieldOps.Domain.Common;
using FieldOps.Domain.Inventory;

namespace FieldOps.Domain.WorkOrders;

/// <summary>A part used on a job, with the price at the time of use for the invoice (US-TAPP-07 AC2).</summary>
public sealed class WorkOrderPart : Entity
{
    /// <summary>Parts are recorded once work has started and until the job is completed.</summary>
    public static readonly WorkOrderStatus[] RecordableStatuses = [WorkOrderStatus.InProgress, WorkOrderStatus.OnHold];

    private WorkOrderPart() { }

    public Guid WorkOrderId { get; private init; }
    public Guid PartId { get; private init; }
    public Guid StockLocationId { get; private init; }
    public decimal Quantity { get; private init; }
    public decimal UnitPrice { get; private init; }
    public Guid StockMovementId { get; private init; }

    public decimal LineTotal => Quantity * UnitPrice;

    /// <summary>Takes the parts from <paramref name="van"/> and records the line; the handler saves both with the movement.</summary>
    public static Result<(WorkOrderPart Line, StockMovement Movement)> Use(
        WorkOrder workOrder, Part part, StockLevel van, decimal quantity, Guid userId, DateTimeOffset now)
    {
        if (!RecordableStatuses.Contains(workOrder.Status)) return WorkOrderErrors.PartsNotAllowedNow(workOrder.Status);
        if (!part.IsActive) return InventoryErrors.PartNotAvailable;

        var consumed = StockLedger.Consume(van, quantity, workOrder.Id, userId, now);
        if (consumed.IsFailure) return consumed.Error;

        var line = new WorkOrderPart
        {
            WorkOrderId = workOrder.Id,
            PartId = part.Id,
            StockLocationId = van.StockLocationId,
            Quantity = quantity,
            UnitPrice = part.UnitPrice,
            StockMovementId = consumed.Value.Id,
        };
        return (line, consumed.Value);
    }

    /// <summary>Returns the parts to the location they came from; allowed until the job is completed (US-TAPP-07 AC4).</summary>
    public Result<StockMovement> ReturnTo(StockLevel level, WorkOrder workOrder, Guid userId, DateTimeOffset now)
    {
        if (!workOrder.IsOpen) return WorkOrderErrors.PartsLocked;
        if (level.PartId != PartId || level.StockLocationId != StockLocationId) throw new ArgumentException("Return to the source level.");
        return StockLedger.Return(level, Quantity, WorkOrderId, userId, now);
    }
}
