using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Common;
using FieldOps.Domain.WorkOrders;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.WorkOrders;

public sealed record TimeEntryDto(
    Guid Id, Guid TechnicianId, string TechnicianName, TimeEntryType Type, DateTimeOffset StartedAt, DateTimeOffset? EndedAt,
    int? DurationMinutes, bool CanEdit);

public sealed record TimeEntryInput(DateTimeOffset StartedAt, DateTimeOffset? EndedAt);

public sealed class TimeEntryInputValidator : AbstractValidator<TimeEntryInput>
{
    public TimeEntryInputValidator()
    {
        RuleFor(x => x.EndedAt).GreaterThan(x => x.StartedAt).When(x => x.EndedAt is not null)
            .WithMessage("The end must be after the start.");
    }
}

public sealed record ListTimeEntriesQuery(Guid WorkOrderId);

public sealed record CorrectTimeEntryCommand(Guid Id, TimeEntryInput Input);

public sealed class ListTimeEntriesHandler(IAppDbContext db, ICurrentUser user, TimeEntryReader reader)
    : IQueryHandler<ListTimeEntriesQuery, Result<IReadOnlyList<TimeEntryDto>>>
{
    public async Task<Result<IReadOnlyList<TimeEntryDto>>> Handle(ListTimeEntriesQuery query, CancellationToken ct)
    {
        var found = await db.WorkOrders.AsNoTracking().FindAccessibleAsync(query.WorkOrderId, user, ct);
        if (found.IsFailure) return found.Error;
        return Result.Success(await reader.ReadAsync(found.Value, ct));
    }
}

/// <summary>US-TAPP-09: a technician corrects their own entries before completion; the office may correct anyone's.</summary>
public sealed class CorrectTimeEntryHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock, TimeEntryReader reader)
    : ICommandHandler<CorrectTimeEntryCommand, Result<IReadOnlyList<TimeEntryDto>>>
{
    public async Task<Result<IReadOnlyList<TimeEntryDto>>> Handle(CorrectTimeEntryCommand cmd, CancellationToken ct)
    {
        var entry = await db.TimeEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Id == cmd.Id, ct);
        if (entry is null) return WorkOrderErrors.TimeEntryNotFound;
        var found = await db.WorkOrders.Include(w => w.TimeEntries).FindAccessibleAsync(entry.WorkOrderId, user, ct);
        if (found.IsFailure) return found.Error;
        if (!TimeEntryReader.CanEdit(user, entry)) return WorkOrderErrors.TimeEntryNotYours;

        var now = clock.GetUtcNow();
        var (start, end) = (cmd.Input.StartedAt, cmd.Input.EndedAt);
        var overlaps = await db.TimeEntries.AnyAsync(e => e.TechnicianId == entry.TechnicianId && e.Id != entry.Id
            && e.StartedAt < (end ?? now) && start < (e.EndedAt ?? now), ct);
        if (overlaps) return WorkOrderErrors.TimeEntryOverlap;

        var corrected = found.Value.CorrectTimeEntry(entry.Id, start, end, now);
        if (corrected.IsFailure) return corrected.Error;

        var saved = await db.SaveAsync(ct);
        if (saved.IsFailure) return saved.Error;
        return Result.Success(await reader.ReadAsync(found.Value, ct));
    }
}

public sealed class TimeEntryReader(IAppDbContext db, ICurrentUser user, IIdentityService identity)
{
    public static bool CanEdit(ICurrentUser user, TimeEntry entry) => user.IsOffice() || user.TechnicianId == entry.TechnicianId;

    public async Task<IReadOnlyList<TimeEntryDto>> ReadAsync(WorkOrder workOrder, CancellationToken ct)
    {
        var entries = await db.TimeEntries.AsNoTracking().Where(e => e.WorkOrderId == workOrder.Id)
            .OrderBy(e => e.StartedAt).ToListAsync(ct);
        var technicianIds = entries.Select(e => e.TechnicianId).Distinct().ToList();
        var technicians = await db.Technicians.AsNoTracking().Where(t => technicianIds.Contains(t.Id))
            .Select(t => new { t.Id, t.UserId }).ToListAsync(ct);
        var people = await identity.FindByIdsAsync(technicians.Select(t => t.UserId), ct);
        var names = technicians.ToDictionary(t => t.Id, t => people.TryGetValue(t.UserId, out var p) ? p.FullName : "");

        return entries.Select(e => new TimeEntryDto(e.Id, e.TechnicianId, names.GetValueOrDefault(e.TechnicianId, ""), e.Type,
                e.StartedAt, e.EndedAt, e.DurationMinutes, workOrder.IsOpen && CanEdit(user, e)))
            .ToList();
    }
}
