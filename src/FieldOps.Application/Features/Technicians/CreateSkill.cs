using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Technicians;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Technicians;

public sealed record CreateSkillCommand(SkillInput Input);

public sealed class CreateSkillHandler(IAppDbContext db) : ICommandHandler<CreateSkillCommand, Result<SkillDto>>
{
    public async Task<Result<SkillDto>> Handle(CreateSkillCommand cmd, CancellationToken ct)
    {
        var name = cmd.Input.Name.Trim();
        if (await SkillNames.IsTakenAsync(db, name, null, ct)) return TechnicianErrors.SkillNameTaken;

        var skill = Skill.Create(name);
        db.Skills.Add(skill);
        await db.SaveChangesAsync(ct);
        return new SkillDto(skill.Id, skill.Name);
    }
}

internal static class SkillNames
{
    public static Task<bool> IsTakenAsync(IAppDbContext db, string name, Guid? exceptId, CancellationToken ct)
    {
        var lower = name.ToLower();
        return db.Skills.AnyAsync(s => s.Name.ToLower() == lower && s.Id != exceptId, ct);
    }
}
