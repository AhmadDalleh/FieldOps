using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.WorkOrders;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.WorkOrders;

public sealed record TaskInput(string Description);

public sealed class TaskInputValidator : AbstractValidator<TaskInput>
{
    public TaskInputValidator() => RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
}

public sealed record ReorderTasksInput(IReadOnlyList<Guid> TaskIds);

public sealed record AddTaskCommand(Guid WorkOrderId, TaskInput Input);

public sealed record UpdateTaskCommand(Guid WorkOrderId, Guid TaskId, TaskInput Input);

public sealed record RemoveTaskCommand(Guid WorkOrderId, Guid TaskId);

public sealed record ReorderTasksCommand(Guid WorkOrderId, ReorderTasksInput Input);

public sealed record ToggleTaskCommand(Guid WorkOrderId, Guid TaskId);

/// <summary>Office adds, edits, removes and reorders tasks; the endpoints enforce that.</summary>
public sealed class AddTaskHandler(IAppDbContext db, ICurrentUser user, WorkOrderReader reader)
    : ICommandHandler<AddTaskCommand, Result<WorkOrderDto>>
{
    public Task<Result<WorkOrderDto>> Handle(AddTaskCommand cmd, CancellationToken ct) =>
        TaskChange.RunAsync(db, user, reader, cmd.WorkOrderId, w => w.AddTask(cmd.Input.Description.Trim()), ct);
}

public sealed class UpdateTaskHandler(IAppDbContext db, ICurrentUser user, WorkOrderReader reader)
    : ICommandHandler<UpdateTaskCommand, Result<WorkOrderDto>>
{
    public Task<Result<WorkOrderDto>> Handle(UpdateTaskCommand cmd, CancellationToken ct) =>
        TaskChange.RunAsync(db, user, reader, cmd.WorkOrderId, w => w.UpdateTask(cmd.TaskId, cmd.Input.Description.Trim()), ct);
}

public sealed class RemoveTaskHandler(IAppDbContext db, ICurrentUser user, WorkOrderReader reader)
    : ICommandHandler<RemoveTaskCommand, Result<WorkOrderDto>>
{
    public Task<Result<WorkOrderDto>> Handle(RemoveTaskCommand cmd, CancellationToken ct) =>
        TaskChange.RunAsync(db, user, reader, cmd.WorkOrderId, w => w.RemoveTask(cmd.TaskId), ct);
}

public sealed class ReorderTasksHandler(IAppDbContext db, ICurrentUser user, WorkOrderReader reader)
    : ICommandHandler<ReorderTasksCommand, Result<WorkOrderDto>>
{
    public Task<Result<WorkOrderDto>> Handle(ReorderTasksCommand cmd, CancellationToken ct) =>
        TaskChange.RunAsync(db, user, reader, cmd.WorkOrderId, w => w.ReorderTasks(cmd.Input.TaskIds ?? []), ct);
}

/// <summary>Office or the assigned technician ticks a task (US-WO-05 AC1).</summary>
public sealed class ToggleTaskHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock, WorkOrderReader reader)
    : ICommandHandler<ToggleTaskCommand, Result<WorkOrderDto>>
{
    public Task<Result<WorkOrderDto>> Handle(ToggleTaskCommand cmd, CancellationToken ct) =>
        TaskChange.RunAsync(db, user, reader, cmd.WorkOrderId, w => w.ToggleTask(cmd.TaskId, user.UserId, clock.GetUtcNow()), ct);
}

internal static class TaskChange
{
    public static async Task<Result<WorkOrderDto>> RunAsync(
        IAppDbContext db, ICurrentUser user, WorkOrderReader reader, Guid id, Func<WorkOrder, Result> change, CancellationToken ct)
    {
        var found = await db.WorkOrders.Include(w => w.Tasks).FindAccessibleAsync(id, user, ct);
        if (found.IsFailure) return found.Error;

        var result = change(found.Value);
        if (result.IsFailure) return result.Error;

        var saved = await db.SaveAsync(ct);
        if (saved.IsFailure) return saved.Error;
        return await reader.ReadAsync(id, ct);
    }
}
