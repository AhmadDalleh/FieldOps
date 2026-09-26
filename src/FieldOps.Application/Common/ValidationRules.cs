using FluentValidation;

namespace FieldOps.Application.Common;

public static class ValidationRules
{
    /// <summary>Mirrors the Identity password policy so clients get a 400 with field errors before Identity runs.</summary>
    public static IRuleBuilderOptions<T, string> Password<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MinimumLength(8)
            .Matches("[0-9]").WithMessage("The password must contain a digit.")
            .Matches("[A-Z]").WithMessage("The password must contain an uppercase letter.");
}
