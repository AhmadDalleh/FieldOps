using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Technicians;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Technicians;

public sealed record TimeOffSlot(Guid Id, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? Reason);

public sealed record TechnicianAvailabilityDto(
    TechnicianDto Technician,
    DateOnly Date,
    int JobCount,
    IReadOnlyList<TimeOffSlot> TimeOff,
    bool IsAvailable);

/// <param name="Date">The Dubai calendar day to report on; today when omitted.</param>
public sealed record ListTechniciansQuery(DateOnly? Date = null, bool IncludeInactive = false);

public sealed class ListTechniciansHandler(IAppDbContext db, TechnicianReader reader, TimeProvider clock)
    : IQueryHandler<ListTechniciansQuery, IReadOnlyList<TechnicianAvailabilityDto>>
{
    public async Task<IReadOnlyList<TechnicianAvailabilityDto>> Handle(ListTechniciansQuery query, CancellationToken ct)
    {
        var day = query.Date ?? clock.Today();
        var (from, to) = BusinessCalendar.DayRange(day);

        var technicians = await reader.ReadAsync(
            query.IncludeInactive ? db.Technicians : db.Technicians.Where(t => t.IsActive), ct);

        var timeOff = await db.TimeOffs.AsNoTracking()
            .Where(t => t.Status == TimeOffStatus.Approved && t.StartsAt < to && from < t.EndsAt)
            .ToListAsync(ct);
        var timeOffByTechnician = timeOff.ToLookup(t => t.TechnicianId);

        // TODO(P5): count the technician's scheduled work orders on this day (US-TEC-04).
        const int jobCount = 0;

        return technicians
            .Select(t =>
            {
                var slots = timeOffByTechnician[t.Id]
                    .OrderBy(x => x.StartsAt)
                    .Select(x => new TimeOffSlot(x.Id, x.StartsAt, x.EndsAt, x.Reason))
                    .ToList();
                return new TechnicianAvailabilityDto(t, day, jobCount, slots, t.IsActive && slots.Count == 0);
            })
            .ToList();
    }
}
