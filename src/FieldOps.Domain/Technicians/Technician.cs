using FieldOps.Domain.Common;

namespace FieldOps.Domain.Technicians;

public sealed class Technician : AuditableEntity
{
    private Technician() { }

    public Guid UserId { get; private init; }
    public string EmployeeCode { get; private init; } = null!;
    public string? Phone { get; private set; }
    public string Color { get; private set; } = DefaultColor;
    public decimal HourlyCost { get; private set; }
    public TimeOnly WorkingHoursStart { get; private set; } = new(8, 0);
    public TimeOnly WorkingHoursEnd { get; private set; } = new(17, 0);
    public bool IsActive { get; private set; } = true;

    public const string DefaultColor = "#1E88E5";

    public static Technician Create(Guid userId, string employeeCode, string? phone) => new()
    {
        UserId = userId,
        EmployeeCode = employeeCode,
        Phone = phone,
    };

    public void UpdatePhone(string? phone) => Phone = phone;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
