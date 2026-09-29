using FieldOps.Domain.Common;

namespace FieldOps.Domain.WorkOrders;

/// <summary>One row per status change, written by <see cref="WorkOrder"/> itself.</summary>
public sealed class WorkOrderStatusHistory : Entity
{
    private WorkOrderStatusHistory() { }

    internal WorkOrderStatusHistory(
        Guid workOrderId, WorkOrderStatus? from, WorkOrderStatus to, Guid changedBy, DateTimeOffset changedAt,
        string? note, double? latitude, double? longitude)
    {
        WorkOrderId = workOrderId;
        FromStatus = from;
        ToStatus = to;
        ChangedBy = changedBy;
        ChangedAt = changedAt;
        Note = note;
        Latitude = latitude;
        Longitude = longitude;
    }

    public Guid WorkOrderId { get; private init; }
    public WorkOrderStatus? FromStatus { get; private init; }
    public WorkOrderStatus ToStatus { get; private init; }
    public Guid ChangedBy { get; private init; }
    public DateTimeOffset ChangedAt { get; private init; }
    public string? Note { get; private init; }
    public double? Latitude { get; private init; }
    public double? Longitude { get; private init; }
}
