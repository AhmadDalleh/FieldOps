namespace FieldOps.Domain.Technicians;

public sealed class TechnicianSkill
{
    private TechnicianSkill() { }

    internal TechnicianSkill(Guid technicianId, Guid skillId)
    {
        TechnicianId = technicianId;
        SkillId = skillId;
    }

    public Guid TechnicianId { get; private init; }
    public Guid SkillId { get; private init; }
}
