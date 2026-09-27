using FieldOps.Application.Abstractions;
using FieldOps.Domain.Technicians;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Technicians;

public sealed class TimeOffReader(IAppDbContext db, IIdentityService identity)
{
    public async Task<IReadOnlyList<TimeOffDto>> ReadAsync(IQueryable<TimeOff> query, CancellationToken ct)
    {
        var rows = await (
            from t in query.AsNoTracking()
            join tech in db.Technicians on t.TechnicianId equals tech.Id
            select new { TimeOff = t, tech.UserId }).ToListAsync(ct);
        var users = await identity.FindByIdsAsync(rows.Select(r => r.UserId), ct);

        return rows
            .Select(r => new TimeOffDto(
                r.TimeOff.Id,
                r.TimeOff.TechnicianId,
                users.TryGetValue(r.UserId, out var user) ? user.FullName : "",
                r.TimeOff.StartsAt,
                r.TimeOff.EndsAt,
                r.TimeOff.Reason,
                r.TimeOff.Status,
                r.TimeOff.CreatedAt))
            .OrderByDescending(t => t.StartsAt)
            .ToList();
    }
}
