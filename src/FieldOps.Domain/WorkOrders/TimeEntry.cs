using FieldOps.Domain.Common;

namespace FieldOps.Domain.WorkOrders;

public enum TimeEntryType { Travel, Work }

/// <summary>Time a technician spent travelling to or working on a job, started and stopped by the job's status changes.</summary>
public sealed class TimeEntry : Entity
{
    private TimeEntry() { }

    internal TimeEntry(Guid workOrderId, Guid technicianId, TimeEntryType type, DateTimeOffset startedAt)
    {
        WorkOrderId = workOrderId;
        TechnicianId = technicianId;
        Type = type;
        StartedAt = startedAt;
    }

    public Guid WorkOrderId { get; private init; }
    public Guid TechnicianId { get; private init; }
    public TimeEntryType Type { get; private init; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>Whole minutes between start and end, set when the entry stops.</summary>
    public int? DurationMinutes { get; private set; }

    public bool IsOpen => EndedAt is null;

    public bool Overlaps(DateTimeOffset start, DateTimeOffset? end, DateTimeOffset now) =>
        StartedAt < (end ?? now) && start < (EndedAt ?? now);

    internal void Stop(DateTimeOffset now)
    {
        if (!IsOpen) return;
        Set(StartedAt, now < StartedAt ? StartedAt : now);
    }

    internal void Set(DateTimeOffset start, DateTimeOffset? end)
    {
        StartedAt = start;
        EndedAt = end;
        DurationMinutes = end is { } e ? (int)Math.Round((e - start).TotalMinutes, MidpointRounding.AwayFromZero) : null;
    }
}
