using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Common;
using FieldOps.Domain.WorkOrders;

namespace FieldOps.Application.Features.WorkOrders;

public sealed record HoldInput(string? Note);

public sealed record CancelInput(string? Reason);

public sealed record HoldWorkOrderCommand(Guid Id, HoldInput Input);

public sealed record ResumeWorkOrderCommand(Guid Id);

public sealed record CancelWorkOrderCommand(Guid Id, CancelInput Input);

/// <summary>Office puts a job on hold; so may its assigned technician (docs/07-flows.md).</summary>
public sealed class HoldWorkOrderHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock, WorkOrderReader reader)
    : ICommandHandler<HoldWorkOrderCommand, Result<WorkOrderDto>>
{
    public Task<Result<WorkOrderDto>> Handle(HoldWorkOrderCommand cmd, CancellationToken ct) =>
        StatusChange.RunAsync(db, user, reader, cmd.Id, w => w.Hold(cmd.Input.Note, user.UserId, clock.GetUtcNow()), ct);
    // TODO(P9): notify Office that the job is on hold.
}

public sealed class ResumeWorkOrderHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock, WorkOrderReader reader)
    : ICommandHandler<ResumeWorkOrderCommand, Result<WorkOrderDto>>
{
    public Task<Result<WorkOrderDto>> Handle(ResumeWorkOrderCommand cmd, CancellationToken ct) =>
        StatusChange.RunAsync(db, user, reader, cmd.Id, w => w.Resume(user.UserId, clock.GetUtcNow()), ct);
}

/// <summary>Office only (US-WO-08).</summary>
public sealed class CancelWorkOrderHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock, WorkOrderReader reader)
    : ICommandHandler<CancelWorkOrderCommand, Result<WorkOrderDto>>
{
    public Task<Result<WorkOrderDto>> Handle(CancelWorkOrderCommand cmd, CancellationToken ct)
    {
        if (!user.IsOffice()) return Task.FromResult<Result<WorkOrderDto>>(Errors.Forbidden);

        // TODO(P7): return 409 while consumed parts are still on the job (US-WO-08 AC1).
        // TODO(P6): stop any open time entries.
        // TODO(P9): notify the assigned technician (US-WO-08 AC2).
        return StatusChange.RunAsync(db, user, reader, cmd.Id, w => w.Cancel(cmd.Input.Reason, user.UserId, clock.GetUtcNow()), ct);
    }
}

internal static class StatusChange
{
    public static async Task<Result<WorkOrderDto>> RunAsync(
        IAppDbContext db, ICurrentUser user, WorkOrderReader reader, Guid id, Func<WorkOrder, Result> change, CancellationToken ct)
    {
        var found = await db.WorkOrders.FindAccessibleAsync(id, user, ct);
        if (found.IsFailure) return found.Error;

        var result = change(found.Value);
        if (result.IsFailure) return result.Error;

        var saved = await db.SaveAsync(ct);
        if (saved.IsFailure) return saved.Error;
        return await reader.ReadAsync(id, ct);
    }
}
