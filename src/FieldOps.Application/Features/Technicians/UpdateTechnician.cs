using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Technicians;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Technicians;

public sealed record UpdateTechnicianCommand(Guid Id, TechnicianInput Input);

public sealed class UpdateTechnicianHandler(IAppDbContext db, TechnicianReader reader)
    : ICommandHandler<UpdateTechnicianCommand, Result<TechnicianDto>>
{
    public async Task<Result<TechnicianDto>> Handle(UpdateTechnicianCommand cmd, CancellationToken ct)
    {
        var technician = await db.Technicians.Include(t => t.Skills).FirstOrDefaultAsync(t => t.Id == cmd.Id, ct);
        if (technician is null) return TechnicianErrors.NotFound;

        var input = cmd.Input;
        var code = input.EmployeeCode.Trim().ToUpperInvariant();
        if (await db.Technicians.AnyAsync(t => t.EmployeeCode == code && t.Id != cmd.Id, ct))
            return TechnicianErrors.EmployeeCodeTaken;

        var skillIds = input.SkillIds.Distinct().ToList();
        if (await db.Skills.CountAsync(s => skillIds.Contains(s.Id), ct) != skillIds.Count)
            return TechnicianErrors.UnknownSkill;

        var phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim();
        var result = technician.UpdateProfile(new TechnicianProfile(
            code, phone, input.Color, input.HourlyCost, input.WorkingHoursStart, input.WorkingHoursEnd));
        if (result.IsFailure) return result.Error;

        technician.SetSkills(skillIds);
        await db.SaveChangesAsync(ct);
        return (await reader.ReadAsync(db.Technicians.Where(t => t.Id == cmd.Id), ct))[0];
    }
}
