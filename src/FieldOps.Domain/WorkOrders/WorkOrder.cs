using FieldOps.Domain.Common;

namespace FieldOps.Domain.WorkOrders;

public sealed class WorkOrder : AuditableEntity
{
    public static readonly TimeSpan UrgentDueWithin = TimeSpan.FromHours(4);

    /// <summary>The transition table from docs/07-flows.md: (current status, action) → next status.</summary>
    private static readonly Dictionary<(WorkOrderStatus, WorkOrderAction), WorkOrderStatus> Transitions = new()
    {
        [(WorkOrderStatus.New, WorkOrderAction.Schedule)] = WorkOrderStatus.Scheduled,
        [(WorkOrderStatus.New, WorkOrderAction.Cancel)] = WorkOrderStatus.Cancelled,

        [(WorkOrderStatus.Scheduled, WorkOrderAction.Schedule)] = WorkOrderStatus.Scheduled,
        [(WorkOrderStatus.Scheduled, WorkOrderAction.Unassign)] = WorkOrderStatus.New,
        [(WorkOrderStatus.Scheduled, WorkOrderAction.Dispatch)] = WorkOrderStatus.Dispatched,
        [(WorkOrderStatus.Scheduled, WorkOrderAction.Cancel)] = WorkOrderStatus.Cancelled,

        [(WorkOrderStatus.Dispatched, WorkOrderAction.Schedule)] = WorkOrderStatus.Dispatched,
        [(WorkOrderStatus.Dispatched, WorkOrderAction.Unassign)] = WorkOrderStatus.New,
        [(WorkOrderStatus.Dispatched, WorkOrderAction.EnRoute)] = WorkOrderStatus.EnRoute,
        [(WorkOrderStatus.Dispatched, WorkOrderAction.Start)] = WorkOrderStatus.InProgress,
        [(WorkOrderStatus.Dispatched, WorkOrderAction.Cancel)] = WorkOrderStatus.Cancelled,

        [(WorkOrderStatus.EnRoute, WorkOrderAction.Start)] = WorkOrderStatus.InProgress,
        [(WorkOrderStatus.EnRoute, WorkOrderAction.Cancel)] = WorkOrderStatus.Cancelled,

        [(WorkOrderStatus.InProgress, WorkOrderAction.Hold)] = WorkOrderStatus.OnHold,
        [(WorkOrderStatus.InProgress, WorkOrderAction.Complete)] = WorkOrderStatus.Completed,
        [(WorkOrderStatus.InProgress, WorkOrderAction.Cancel)] = WorkOrderStatus.Cancelled,

        [(WorkOrderStatus.OnHold, WorkOrderAction.Resume)] = WorkOrderStatus.InProgress,
        [(WorkOrderStatus.OnHold, WorkOrderAction.Cancel)] = WorkOrderStatus.Cancelled,

        [(WorkOrderStatus.Completed, WorkOrderAction.Invoice)] = WorkOrderStatus.Invoiced,
        [(WorkOrderStatus.Invoiced, WorkOrderAction.VoidInvoice)] = WorkOrderStatus.Completed,
    };

    private readonly List<WorkOrderTask> _tasks = [];
    private readonly List<WorkOrderStatusHistory> _history = [];

    private WorkOrder() { }

    public string Number { get; private init; } = null!;
    public Guid CustomerId { get; private init; }
    public Guid SiteId { get; private init; }
    public Guid? AssetId { get; private set; }
    public Guid? RequiredSkillId { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public WorkOrderType Type { get; private set; }
    public WorkOrderPriority Priority { get; private set; }
    public WorkOrderStatus Status { get; private set; }
    public DateTimeOffset? DueBy { get; private set; }
    public DateTimeOffset? ScheduledStart { get; private set; }
    public DateTimeOffset? ScheduledEnd { get; private set; }
    public Guid? AssignedTechnicianId { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? CompletionNotes { get; private set; }
    public string? SignedByName { get; private set; }
    public Guid? SignatureAttachmentId { get; private set; }
    public string? CancelReason { get; private set; }

    /// <summary>Maps to the Postgres <c>xmin</c> system column for optimistic concurrency.</summary>
    public uint Version { get; private set; }

    public IReadOnlyList<WorkOrderTask> Tasks => _tasks;
    public IReadOnlyList<WorkOrderStatusHistory> History => _history;

    /// <summary>Open work orders still need work: anything not Completed, Invoiced or Cancelled.</summary>
    public bool IsOpen => Status is not (WorkOrderStatus.Completed or WorkOrderStatus.Invoiced or WorkOrderStatus.Cancelled);

    public static readonly WorkOrderStatus[] ClosedStatuses =
        [WorkOrderStatus.Completed, WorkOrderStatus.Invoiced, WorkOrderStatus.Cancelled];

    public static readonly WorkOrderStatus[] EditableStatuses =
        [WorkOrderStatus.New, WorkOrderStatus.Scheduled, WorkOrderStatus.Dispatched, WorkOrderStatus.OnHold];

    public static bool IsAllowed(WorkOrderStatus from, WorkOrderAction action) => Transitions.ContainsKey((from, action));

    public static WorkOrder Create(
        string number, Guid customerId, Guid siteId, WorkOrderDetails details, IEnumerable<string> tasks,
        Guid userId, DateTimeOffset now)
    {
        var workOrder = new WorkOrder
        {
            Number = number,
            CustomerId = customerId,
            SiteId = siteId,
            Status = WorkOrderStatus.New,
        };
        workOrder.Apply(details);
        if (workOrder is { Priority: WorkOrderPriority.Urgent, DueBy: null }) workOrder.DueBy = now + UrgentDueWithin;

        foreach (var description in tasks) workOrder.AppendTask(description);
        workOrder._history.Add(new WorkOrderStatusHistory(workOrder.Id, null, WorkOrderStatus.New, userId, now, null, null, null));
        return workOrder;
    }

    public Result Update(WorkOrderDetails details)
    {
        if (!EditableStatuses.Contains(Status)) return WorkOrderErrors.NotEditable;
        Apply(details);
        return Result.Success();
    }

    // ---- State machine ----

    public Result Schedule(Guid technicianId, DateTimeOffset start, DateTimeOffset end, Guid userId, DateTimeOffset now)
    {
        if (!IsAllowed(Status, WorkOrderAction.Schedule)) return WorkOrderErrors.InvalidTransition(Status, WorkOrderAction.Schedule);
        if (end <= start) return WorkOrderErrors.InvalidSchedule;

        AssignedTechnicianId = technicianId;
        ScheduledStart = start;
        ScheduledEnd = end;
        return Transition(WorkOrderAction.Schedule, userId, now);
    }

    public Result Unassign(Guid userId, DateTimeOffset now)
    {
        var result = Transition(WorkOrderAction.Unassign, userId, now);
        if (result.IsFailure) return result;

        AssignedTechnicianId = null;
        ScheduledStart = null;
        ScheduledEnd = null;
        return result;
    }

    public Result Dispatch(Guid userId, DateTimeOffset now) => Transition(WorkOrderAction.Dispatch, userId, now);

    // TODO(P6): start a Travel time entry.
    public Result EnRoute(Guid userId, DateTimeOffset now, double? latitude = null, double? longitude = null) =>
        Transition(WorkOrderAction.EnRoute, userId, now, null, latitude, longitude);

    // TODO(P6): stop the Travel time entry and start a Work one.
    public Result Start(Guid userId, DateTimeOffset now, double? latitude = null, double? longitude = null)
    {
        var result = Transition(WorkOrderAction.Start, userId, now, null, latitude, longitude);
        if (result.IsSuccess) StartedAt ??= now;
        return result;
    }

    public Result Hold(string? note, Guid userId, DateTimeOffset now)
    {
        if (!IsAllowed(Status, WorkOrderAction.Hold)) return WorkOrderErrors.InvalidTransition(Status, WorkOrderAction.Hold);
        if (string.IsNullOrWhiteSpace(note)) return WorkOrderErrors.NoteRequired;
        return Transition(WorkOrderAction.Hold, userId, now, note.Trim());
    }

    public Result Resume(Guid userId, DateTimeOffset now) => Transition(WorkOrderAction.Resume, userId, now);

    public Result Complete(string? completionNotes, string? signedByName, Guid? signatureAttachmentId, Guid userId, DateTimeOffset now)
    {
        if (!IsAllowed(Status, WorkOrderAction.Complete)) return WorkOrderErrors.InvalidTransition(Status, WorkOrderAction.Complete);
        if (string.IsNullOrWhiteSpace(completionNotes) || string.IsNullOrWhiteSpace(signedByName) || signatureAttachmentId is null)
            return WorkOrderErrors.CompletionDetailsRequired;

        CompletionNotes = completionNotes.Trim();
        SignedByName = signedByName.Trim();
        SignatureAttachmentId = signatureAttachmentId;
        CompletedAt = now;
        return Transition(WorkOrderAction.Complete, userId, now);
    }

    /// <remarks>The handler must first check that no consumed parts remain on the job (US-WO-08 AC1).</remarks>
    public Result Cancel(string? reason, Guid userId, DateTimeOffset now)
    {
        if (!IsAllowed(Status, WorkOrderAction.Cancel)) return WorkOrderErrors.InvalidTransition(Status, WorkOrderAction.Cancel);
        if (string.IsNullOrWhiteSpace(reason)) return WorkOrderErrors.ReasonRequired;

        CancelReason = reason.Trim();
        return Transition(WorkOrderAction.Cancel, userId, now, CancelReason);
    }

    public Result MarkInvoiced(Guid userId, DateTimeOffset now) => Transition(WorkOrderAction.Invoice, userId, now);

    public Result VoidInvoice(Guid userId, DateTimeOffset now) => Transition(WorkOrderAction.VoidInvoice, userId, now);

    private Result Transition(
        WorkOrderAction action, Guid userId, DateTimeOffset now, string? note = null, double? latitude = null, double? longitude = null)
    {
        if (!Transitions.TryGetValue((Status, action), out var next)) return WorkOrderErrors.InvalidTransition(Status, action);

        _history.Add(new WorkOrderStatusHistory(Id, Status, next, userId, now, note, latitude, longitude));
        Status = next;
        return Result.Success();
    }

    // ---- Tasks ----

    public bool TasksLocked => !IsOpen;

    public Result<WorkOrderTask> AddTask(string description)
    {
        if (TasksLocked) return WorkOrderErrors.TasksLocked;
        return AppendTask(description);
    }

    public Result UpdateTask(Guid taskId, string description)
    {
        if (TasksLocked) return WorkOrderErrors.TasksLocked;
        var task = _tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null) return WorkOrderErrors.TaskNotFound;

        task.Description = description;
        return Result.Success();
    }

    public Result RemoveTask(Guid taskId)
    {
        if (TasksLocked) return WorkOrderErrors.TasksLocked;
        var task = _tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null) return WorkOrderErrors.TaskNotFound;

        _tasks.Remove(task);
        Renumber(_tasks.OrderBy(t => t.SortOrder).ToList());
        return Result.Success();
    }

    public Result ReorderTasks(IReadOnlyList<Guid> taskIds)
    {
        if (TasksLocked) return WorkOrderErrors.TasksLocked;
        if (taskIds.Count != _tasks.Count || taskIds.Distinct().Count() != taskIds.Count || taskIds.Any(id => _tasks.All(t => t.Id != id)))
            return WorkOrderErrors.InvalidTaskOrder;

        Renumber(taskIds.Select(id => _tasks.Single(t => t.Id == id)).ToList());
        return Result.Success();
    }

    public Result<WorkOrderTask> ToggleTask(Guid taskId, Guid userId, DateTimeOffset now)
    {
        if (TasksLocked) return WorkOrderErrors.TasksLocked;
        var task = _tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null) return WorkOrderErrors.TaskNotFound;

        task.Toggle(userId, now);
        return task;
    }

    private WorkOrderTask AppendTask(string description)
    {
        var task = new WorkOrderTask(Id, _tasks.Count == 0 ? 1 : _tasks.Max(t => t.SortOrder) + 1, description);
        _tasks.Add(task);
        return task;
    }

    private static void Renumber(List<WorkOrderTask> ordered)
    {
        for (var i = 0; i < ordered.Count; i++) ordered[i].SortOrder = i + 1;
    }

    private void Apply(WorkOrderDetails details)
    {
        Title = details.Title;
        Description = details.Description;
        Type = details.Type;
        Priority = details.Priority;
        DueBy = details.DueBy;
        AssetId = details.AssetId;
        RequiredSkillId = details.RequiredSkillId;
    }
}

public sealed record WorkOrderDetails(
    string Title,
    string? Description,
    WorkOrderType Type,
    WorkOrderPriority Priority,
    DateTimeOffset? DueBy,
    Guid? AssetId,
    Guid? RequiredSkillId = null);
