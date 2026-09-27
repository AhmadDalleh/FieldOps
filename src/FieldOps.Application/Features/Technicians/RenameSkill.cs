using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Technicians;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Technicians;

public sealed record RenameSkillCommand(Guid Id, SkillInput Input);

public sealed class RenameSkillHandler(IAppDbContext db) : ICommandHandler<RenameSkillCommand, Result<SkillDto>>
{
    public async Task<Result<SkillDto>> Handle(RenameSkillCommand cmd, CancellationToken ct)
    {
        var skill = await db.Skills.FirstOrDefaultAsync(s => s.Id == cmd.Id, ct);
        if (skill is null) return TechnicianErrors.SkillNotFound;

        var name = cmd.Input.Name.Trim();
        if (await SkillNames.IsTakenAsync(db, name, skill.Id, ct)) return TechnicianErrors.SkillNameTaken;

        skill.Rename(name);
        await db.SaveChangesAsync(ct);
        return new SkillDto(skill.Id, skill.Name);
    }
}
