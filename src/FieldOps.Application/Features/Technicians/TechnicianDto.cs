using FluentValidation;

namespace FieldOps.Application.Features.Technicians;

public sealed record TechnicianDto(
    Guid Id,
    Guid UserId,
    string FullName,
    string Email,
    string EmployeeCode,
    string? Phone,
    string Color,
    decimal HourlyCost,
    TimeOnly WorkingHoursStart,
    TimeOnly WorkingHoursEnd,
    bool IsActive,
    IReadOnlyList<SkillDto> Skills);

public sealed record TechnicianInput(
    string EmployeeCode,
    string? Phone,
    string Color,
    decimal HourlyCost,
    TimeOnly WorkingHoursStart,
    TimeOnly WorkingHoursEnd,
    IReadOnlyList<Guid> SkillIds);

public sealed class TechnicianInputValidator : AbstractValidator<TechnicianInput>
{
    public TechnicianInputValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Color).Matches("^#[0-9A-Fa-f]{6}$").WithMessage("The color must be a hex value such as #1E88E5.");
        RuleFor(x => x.HourlyCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.WorkingHoursEnd).GreaterThan(x => x.WorkingHoursStart).WithMessage("Working hours must end after they start.");
        RuleFor(x => x.SkillIds).NotNull();
    }
}
