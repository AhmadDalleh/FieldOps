using FieldOps.Application.Features.Notifications;
using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.WorkOrders;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.WorkOrders;

/// <param name="AllowOverlap">Save even though the technician has another job at this time.</param>
public sealed record ScheduleInput(Guid TechnicianId, DateTimeOffset Start, DateTimeOffset End, bool AllowOverlap = false);

public sealed class ScheduleInputValidator : AbstractValidator<ScheduleInput>
{
    public ScheduleInputValidator()
    {
        RuleFor(x => x.TechnicianId).NotEmpty();
        RuleFor(x => x.End - x.Start)
            .InclusiveBetween(SchedulingErrors.MinDuration, SchedulingErrors.MaxDuration)
            .OverridePropertyName("End")
            .WithMessage("A job must end after it starts and last between 15 minutes and 12 hours.");
    }
}

public sealed record ScheduleResult(WorkOrderDto WorkOrder, IReadOnlyList<string> Warnings);

public sealed record ScheduleWorkOrderCommand(Guid Id, ScheduleInput Input);

/// <summary>Assigns and schedules, or reschedules, a job (US-DSP-01; flow 4 in docs/07-flows.md).</summary>
public sealed class ScheduleWorkOrderHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock, WorkOrderReader reader, Notifier notifier)
    : ICommandHandler<ScheduleWorkOrderCommand, Result<ScheduleResult>>
{
    public async Task<Result<ScheduleResult>> Handle(ScheduleWorkOrderCommand cmd, CancellationToken ct)
    {
        var input = cmd.Input;
        var duration = input.End - input.Start;
        if (duration < SchedulingErrors.MinDuration || duration > SchedulingErrors.MaxDuration) return SchedulingErrors.InvalidDuration;

        var workOrder = await db.WorkOrders.FirstOrDefaultAsync(w => w.Id == cmd.Id, ct);
        if (workOrder is null) return WorkOrderErrors.NotFound;
        if (!WorkOrder.IsAllowed(workOrder.Status, WorkOrderAction.Schedule))
            return WorkOrderErrors.InvalidTransition(workOrder.Status, WorkOrderAction.Schedule);

        var technician = await db.Technicians.AsNoTracking().Include(t => t.Skills)
            .FirstOrDefaultAsync(t => t.Id == input.TechnicianId && t.IsActive, ct);
        if (technician is null) return SchedulingErrors.TechnicianNotAvailable;

        if (await Scheduling.OnTimeOffAsync(db, technician.Id, input.Start, input.End, ct)) return SchedulingErrors.OnTimeOff;

        var warnings = new List<string>();
        var overlaps = await Scheduling.OverlappingJobsAsync(db, technician.Id, input.Start, input.End, workOrder.Id, ct);
        if (overlaps.Count > 0)
        {
            if (!input.AllowOverlap) return SchedulingErrors.Overlap(overlaps);
            warnings.Add($"Overlaps {string.Join(", ", overlaps.Select(o => o.Number))}");
        }

        if (workOrder.RequiredSkillId is { } skillId && technician.Skills.All(s => s.SkillId != skillId))
        {
            var skill = await db.Skills.AsNoTracking().Where(s => s.Id == skillId).Select(s => s.Name).SingleAsync(ct);
            warnings.Add($"Missing skill: {skill}");
        }

        var (technicianBefore, startBefore, endBefore) = (workOrder.AssignedTechnicianId, workOrder.ScheduledStart, workOrder.ScheduledEnd);
        var result = workOrder.Schedule(technician.Id, input.Start, input.End, user.UserId, clock.GetUtcNow());
        if (result.IsFailure) return result.Error;

        // US-DSP-01 AC6: the technician hears about new and moved jobs; one who lost the job hears that too.
        if (technicianBefore != technician.Id)
        {
            await notifier.JobAssignedAsync(workOrder, technician.Id, rescheduled: false, ct);
            if (technicianBefore is { } previous) await notifier.JobRemovedAsync(workOrder, previous, cancelled: false, null, ct);
        }
        else if (startBefore != input.Start || endBefore != input.End)
        {
            await notifier.JobAssignedAsync(workOrder, technician.Id, rescheduled: true, ct);
        }

        var saved = await db.SaveAsync(ct);
        if (saved.IsFailure) return saved.Error;

        return new ScheduleResult(await reader.ReadAsync(workOrder.Id, ct), warnings);
    }
}
