using System.Text.RegularExpressions;
using FieldOps.Domain.Common;

namespace FieldOps.Domain.Technicians;

public sealed partial class Technician : AuditableEntity
{
    private readonly List<TechnicianSkill> _skills = [];

    private Technician() { }

    public Guid UserId { get; private init; }
    public string EmployeeCode { get; private set; } = null!;
    public string? Phone { get; private set; }
    public string Color { get; private set; } = DefaultColor;
    public decimal HourlyCost { get; private set; }
    public TimeOnly WorkingHoursStart { get; private set; } = new(8, 0);
    public TimeOnly WorkingHoursEnd { get; private set; } = new(17, 0);
    public bool IsActive { get; private set; } = true;

    public IReadOnlyList<TechnicianSkill> Skills => _skills;

    public const string DefaultColor = "#1E88E5";

    public static Technician Create(Guid userId, string employeeCode, string? phone) => new()
    {
        UserId = userId,
        EmployeeCode = employeeCode,
        Phone = phone,
    };

    public Result UpdateProfile(TechnicianProfile profile)
    {
        if (!HexColor().IsMatch(profile.Color))
            return Error.Validation("Technician.InvalidColor", "The color must be a hex value such as #1E88E5.");
        if (profile.HourlyCost < 0)
            return Error.Validation("Technician.InvalidHourlyCost", "The hourly cost must be at least 0.");
        if (profile.WorkingHoursEnd <= profile.WorkingHoursStart)
            return Error.Validation("Technician.InvalidWorkingHours", "Working hours must end after they start.");

        EmployeeCode = profile.EmployeeCode;
        Phone = profile.Phone;
        Color = profile.Color.ToUpperInvariant();
        HourlyCost = profile.HourlyCost;
        WorkingHoursStart = profile.WorkingHoursStart;
        WorkingHoursEnd = profile.WorkingHoursEnd;
        return Result.Success();
    }

    public void SetSkills(IEnumerable<Guid> skillIds)
    {
        var wanted = skillIds.ToHashSet();
        _skills.RemoveAll(s => !wanted.Contains(s.SkillId));
        foreach (var id in wanted.Where(id => _skills.All(s => s.SkillId != id)))
            _skills.Add(new TechnicianSkill(Id, id));
    }

    public void UpdatePhone(string? phone) => Phone = phone;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();
}

public sealed record TechnicianProfile(
    string EmployeeCode,
    string? Phone,
    string Color,
    decimal HourlyCost,
    TimeOnly WorkingHoursStart,
    TimeOnly WorkingHoursEnd);
