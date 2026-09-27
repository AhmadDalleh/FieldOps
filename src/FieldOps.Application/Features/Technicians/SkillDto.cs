using FluentValidation;

namespace FieldOps.Application.Features.Technicians;

public sealed record SkillDto(Guid Id, string Name);

public sealed record SkillInput(string Name);

public sealed class SkillInputValidator : AbstractValidator<SkillInput>
{
    public SkillInputValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
}
