using FieldOps.Domain.Common;

namespace FieldOps.Domain.WorkOrders;

public sealed class WorkOrderTask : Entity
{
    private WorkOrderTask() { }

    internal WorkOrderTask(Guid workOrderId, int sortOrder, string description)
    {
        WorkOrderId = workOrderId;
        SortOrder = sortOrder;
        Description = description;
    }

    public Guid WorkOrderId { get; private init; }
    public int SortOrder { get; internal set; }
    public string Description { get; internal set; } = null!;
    public bool IsDone { get; private set; }
    public DateTimeOffset? DoneAt { get; private set; }
    public Guid? DoneBy { get; private set; }

    internal void Toggle(Guid userId, DateTimeOffset now)
    {
        IsDone = !IsDone;
        DoneAt = IsDone ? now : null;
        DoneBy = IsDone ? userId : null;
    }
}
