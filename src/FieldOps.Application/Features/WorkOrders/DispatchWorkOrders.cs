using FieldOps.Application.Features.Notifications;
using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Common;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.WorkOrders;

public sealed record UnassignWorkOrderCommand(Guid Id);

public sealed record DispatchWorkOrderCommand(Guid Id);

public sealed record DispatchDayInput(Guid TechnicianId, DateOnly Date);

public sealed record DispatchDayCommand(DispatchDayInput Input);

public sealed record DispatchDayResult(IReadOnlyList<string> Dispatched);

/// <summary>Returns a Scheduled or Dispatched job to New, clearing technician and times (US-DSP-04).</summary>
public sealed class UnassignWorkOrderHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock, WorkOrderReader reader, Notifier notifier)
    : ICommandHandler<UnassignWorkOrderCommand, Result<WorkOrderDto>>
{
    public Task<Result<WorkOrderDto>> Handle(UnassignWorkOrderCommand cmd, CancellationToken ct) =>
        StatusChange.RunAsync(db, user, reader, cmd.Id, w => w.Unassign(user.UserId, clock.GetUtcNow()), ct,
            (w, technician) => technician is { } t ? notifier.JobRemovedAsync(w, t, cancelled: false, null, ct) : Task.CompletedTask);
}

/// <summary>Confirms a scheduled job to its technician (US-DSP-03).</summary>
public sealed class DispatchWorkOrderHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock, WorkOrderReader reader)
    : ICommandHandler<DispatchWorkOrderCommand, Result<WorkOrderDto>>
{
    public Task<Result<WorkOrderDto>> Handle(DispatchWorkOrderCommand cmd, CancellationToken ct) =>
        StatusChange.RunAsync(db, user, reader, cmd.Id, w => w.Dispatch(user.UserId, clock.GetUtcNow()), ct);
}

/// <summary>Dispatches every Scheduled job a technician starts on the given Dubai day.</summary>
public sealed class DispatchDayHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<DispatchDayCommand, Result<DispatchDayResult>>
{
    public async Task<Result<DispatchDayResult>> Handle(DispatchDayCommand cmd, CancellationToken ct)
    {
        var (from, to) = BusinessCalendar.DayRange(cmd.Input.Date);
        var jobs = await db.WorkOrders
            .Where(w => w.AssignedTechnicianId == cmd.Input.TechnicianId && w.Status == WorkOrderStatus.Scheduled
                && w.ScheduledStart >= from && w.ScheduledStart < to)
            .OrderBy(w => w.ScheduledStart)
            .ToListAsync(ct);

        var now = clock.GetUtcNow();
        foreach (var job in jobs)
        {
            var result = job.Dispatch(user.UserId, now);
            if (result.IsFailure) return result.Error;
        }

        var saved = await db.SaveAsync(ct);
        if (saved.IsFailure) return saved.Error;
        return new DispatchDayResult(jobs.Select(j => j.Number).ToList());
    }
}
