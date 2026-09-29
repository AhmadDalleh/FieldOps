using FieldOps.Domain.Technicians;
using FluentValidation;

namespace FieldOps.Application.Features.Technicians;

public sealed record TimeOffDto(
    Guid Id,
    Guid TechnicianId,
    string TechnicianName,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string? Reason,
    TimeOffStatus Status,
    DateTimeOffset CreatedAt);

/// <param name="TechnicianId">Required for office staff; technicians always request for themselves.</param>
public sealed record TimeOffInput(DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? Reason, Guid? TechnicianId = null);

public sealed class TimeOffInputValidator : AbstractValidator<TimeOffInput>
{
    public TimeOffInputValidator()
    {
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt).WithMessage("The end must be after the start.");
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

/// <summary>A job that an approved time off now overlaps and that should be rescheduled.</summary>
public sealed record ConflictingJob(Guid WorkOrderId, string Number, DateTimeOffset ScheduledStart, DateTimeOffset ScheduledEnd);

public sealed record TimeOffDecision(TimeOffDto TimeOff, IReadOnlyList<ConflictingJob> ConflictingJobs);
