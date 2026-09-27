using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Technicians;
using FieldOps.Domain.WorkOrders;
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

        var jobCounts = await db.WorkOrders.AsNoTracking()
            .Where(w => w.AssignedTechnicianId != null && w.Status != WorkOrderStatus.Cancelled
                && w.ScheduledStart >= from && w.ScheduledStart < to)
            .GroupBy(w => w.AssignedTechnicianId!.Value)
            .Select(g => new { TechnicianId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TechnicianId, x => x.Count, ct);

        return technicians
            .Select(t =>
            {
                var slots = timeOffByTechnician[t.Id]
                    .OrderBy(x => x.StartsAt)
                    .Select(x => new TimeOffSlot(x.Id, x.StartsAt, x.EndsAt, x.Reason))
                    .ToList();
                return new TechnicianAvailabilityDto(t, day, jobCounts.GetValueOrDefault(t.Id), slots, t.IsActive && slots.Count == 0);
            })
            .ToList();
    }
}
