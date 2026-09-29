using FieldOps.Domain.Common;

namespace FieldOps.Domain.Technicians;

public enum TimeOffStatus { Pending, Approved, Rejected }

public sealed class TimeOff : AuditableEntity
{
    private TimeOff() { }

    public Guid TechnicianId { get; private init; }
    public DateTimeOffset StartsAt { get; private init; }
    public DateTimeOffset EndsAt { get; private init; }
    public string? Reason { get; private init; }
    public TimeOffStatus Status { get; private set; } = TimeOffStatus.Pending;

    public static Result<TimeOff> Request(Guid technicianId, DateTimeOffset startsAt, DateTimeOffset endsAt, string? reason)
    {
        if (endsAt <= startsAt)
            return Error.Validation("TimeOff.InvalidRange", "The end must be after the start.");

        return new TimeOff { TechnicianId = technicianId, StartsAt = startsAt, EndsAt = endsAt, Reason = reason };
    }

    public Result Approve() => Decide(TimeOffStatus.Approved);

    public Result Reject() => Decide(TimeOffStatus.Rejected);

    /// <summary>True when this time off shares any moment with [from, to).</summary>
    public bool Overlaps(DateTimeOffset from, DateTimeOffset to) => StartsAt < to && from < EndsAt;

    private Result Decide(TimeOffStatus decision)
    {
        if (Status != TimeOffStatus.Pending)
            return Error.Conflict("TimeOff.AlreadyDecided", $"This request was already {Status.ToString().ToLowerInvariant()}.");

        Status = decision;
        return Result.Success();
    }
}
