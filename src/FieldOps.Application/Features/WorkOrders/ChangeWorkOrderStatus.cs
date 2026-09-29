using FieldOps.Application.Features.Notifications;
using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Common;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.WorkOrders;

public sealed record HoldInput(string? Note);

public sealed record CancelInput(string? Reason);

public sealed record HoldWorkOrderCommand(Guid Id, HoldInput Input);

public sealed record ResumeWorkOrderCommand(Guid Id);

public sealed record CancelWorkOrderCommand(Guid Id, CancelInput Input);

/// <summary>Office puts a job on hold; so may its assigned technician (docs/07-flows.md).</summary>
public sealed class HoldWorkOrderHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock, WorkOrderReader reader, Notifier notifier)
    : ICommandHandler<HoldWorkOrderCommand, Result<WorkOrderDto>>
{
    public Task<Result<WorkOrderDto>> Handle(HoldWorkOrderCommand cmd, CancellationToken ct) =>
        StatusChange.RunAsync(db, user, reader, cmd.Id, w => w.Hold(cmd.Input.Note, user.UserId, clock.GetUtcNow()), ct,
            (w, _) => notifier.JobOnHoldAsync(w, cmd.Input.Note, ct));
}

public sealed class ResumeWorkOrderHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock, WorkOrderReader reader)
    : ICommandHandler<ResumeWorkOrderCommand, Result<WorkOrderDto>>
{
    public Task<Result<WorkOrderDto>> Handle(ResumeWorkOrderCommand cmd, CancellationToken ct) =>
        StatusChange.RunAsync(db, user, reader, cmd.Id, w => w.Resume(user.UserId, clock.GetUtcNow(), user.TechnicianId), ct);
}

/// <summary>Office only (US-WO-08).</summary>
public sealed class CancelWorkOrderHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock, WorkOrderReader reader, Notifier notifier)
    : ICommandHandler<CancelWorkOrderCommand, Result<WorkOrderDto>>
{
    public Task<Result<WorkOrderDto>> Handle(CancelWorkOrderCommand cmd, CancellationToken ct)
    {
        if (!user.IsOffice()) return Task.FromResult<Result<WorkOrderDto>>(Errors.Forbidden);

        return RunAsync();

        async Task<Result<WorkOrderDto>> RunAsync()
        {
            // US-WO-08 AC1: parts taken for the job must go back to stock first.
            if (await db.WorkOrderParts.AnyAsync(p => p.WorkOrderId == cmd.Id, ct)) return WorkOrderErrors.PartsNotReturned;
            // US-WO-08 AC2: the assigned technician hears about it.
            return await StatusChange.RunAsync(db, user, reader, cmd.Id, w => w.Cancel(cmd.Input.Reason, user.UserId, clock.GetUtcNow()), ct,
                (w, technician) => technician is { } t ? notifier.JobRemovedAsync(w, t, cancelled: true, cmd.Input.Reason, ct) : Task.CompletedTask);
        }
    }
}

internal static class StatusChange
{
    public static async Task<Result<WorkOrderDto>> RunAsync(
        IAppDbContext db, ICurrentUser user, WorkOrderReader reader, Guid id, Func<WorkOrder, Result> change, CancellationToken ct,
        Func<WorkOrder, Guid?, Task>? notify = null)
    {
        // Tasks for the completion rule, time entries so the change can stop and start them.
        var found = await db.WorkOrders.Include(w => w.Tasks).Include(w => w.TimeEntries).FindAccessibleAsync(id, user, ct);
        if (found.IsFailure) return found.Error;

        var technicianBefore = found.Value.AssignedTechnicianId;
        var result = change(found.Value);
        if (result.IsFailure) return result.Error;
        if (notify is not null) await notify(found.Value, technicianBefore);

        var saved = await db.SaveAsync(ct);
        if (saved.IsFailure) return saved.Error;
        return await reader.ReadAsync(id, ct);
    }
}
