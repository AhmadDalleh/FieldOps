using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Technicians;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.WorkOrders;

public sealed record ScheduledJob(Guid WorkOrderId, string Number, DateTimeOffset ScheduledStart, DateTimeOffset ScheduledEnd);

public static class SchedulingErrors
{
    public static readonly TimeSpan MinDuration = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(12);

    public static readonly Error InvalidDuration = Error.Validation("Schedule.InvalidDuration",
        "A job must end after it starts and last between 15 minutes and 12 hours.");
    public static readonly Error TechnicianNotAvailable = Error.Validation("Schedule.TechnicianNotAvailable",
        "The technician was not found or is inactive.");
    public static readonly Error OnTimeOff = Error.Conflict("Technician.OnTimeOff",
        "The technician has approved time off during this slot.");

    public static Error Overlap(IReadOnlyList<ScheduledJob> conflicts) =>
        Error.Conflict("Schedule.Overlap",
            $"The technician already has {string.Join(", ", conflicts.Select(c => c.Number))} at this time. Schedule anyway?")
        with { Extensions = new Dictionary<string, object?> { ["conflicts"] = conflicts } };
}

internal static class Scheduling
{
    /// <summary>Open, scheduled jobs of the technician that overlap [start, end), apart from <paramref name="exceptId"/>.</summary>
    public static Task<List<ScheduledJob>> OverlappingJobsAsync(
        IAppDbContext db, Guid technicianId, DateTimeOffset start, DateTimeOffset end, Guid? exceptId, CancellationToken ct) =>
        db.WorkOrders.AsNoTracking()
            .Where(w => w.AssignedTechnicianId == technicianId && w.Id != exceptId
                && !WorkOrder.ClosedStatuses.Contains(w.Status)
                && w.ScheduledStart < end && start < w.ScheduledEnd)
            .OrderBy(w => w.ScheduledStart)
            .Select(w => new ScheduledJob(w.Id, w.Number, w.ScheduledStart!.Value, w.ScheduledEnd!.Value))
            .ToListAsync(ct);

    public static Task<bool> OnTimeOffAsync(
        IAppDbContext db, Guid technicianId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct) =>
        db.TimeOffs.AnyAsync(t => t.TechnicianId == technicianId && t.Status == TimeOffStatus.Approved
            && t.StartsAt < end && start < t.EndsAt, ct);
}
