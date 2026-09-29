using FieldOps.Domain.Common;

namespace FieldOps.Domain.Technicians;

public static class TechnicianErrors
{
    public static readonly Error NotFound = Error.NotFound("Technician.NotFound", "The technician was not found.");
    public static readonly Error EmployeeCodeTaken = Error.Conflict("Technician.EmployeeCodeTaken", "Another technician already uses this employee code.");
    public static readonly Error UnknownSkill = Error.Validation("Technician.UnknownSkill", "One or more skills do not exist.");
    public static readonly Error SkillNotFound = Error.NotFound("Skill.NotFound", "The skill was not found.");
    public static readonly Error SkillNameTaken = Error.Conflict("Skill.NameTaken", "A skill with this name already exists.");
    public static readonly Error TimeOffNotFound = Error.NotFound("TimeOff.NotFound", "The time-off request was not found.");
}
