using FieldOps.Application.Abstractions;
using FieldOps.Domain.Technicians;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Technicians;

/// <summary>Builds technician DTOs, joining in names from Identity and skill names.</summary>
public sealed class TechnicianReader(IAppDbContext db, IIdentityService identity)
{
    public async Task<IReadOnlyList<TechnicianDto>> ReadAsync(IQueryable<Technician> technicians, CancellationToken ct)
    {
        var rows = await technicians.AsNoTracking().Include(t => t.Skills).ToListAsync(ct);
        var skillNames = await db.Skills.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var users = await identity.FindByIdsAsync(rows.Select(t => t.UserId), ct);

        return rows
            .Select(t => new TechnicianDto(
                t.Id,
                t.UserId,
                users.TryGetValue(t.UserId, out var user) ? user.FullName : "",
                user?.Email ?? "",
                t.EmployeeCode,
                t.Phone,
                t.Color,
                t.HourlyCost,
                t.WorkingHoursStart,
                t.WorkingHoursEnd,
                t.IsActive,
                t.Skills.Select(s => new SkillDto(s.SkillId, skillNames[s.SkillId])).OrderBy(s => s.Name).ToList()))
            .OrderBy(t => t.FullName)
            .ToList();
    }
}
