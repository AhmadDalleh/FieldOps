using FieldOps.Domain.Customers;
using FluentValidation;

namespace FieldOps.Application.Features.Customers;

public sealed record CustomerInput(
    string Name,
    CustomerType Type,
    string? Email,
    string Phone,
    string? TaxRegistrationNumber,
    string? BillingAddress,
    string? Notes)
{
    public CustomerDetails ToDetails() => new(
        Name.Trim(),
        Type,
        Normalize(Email),
        Phone.Trim(),
        Normalize(TaxRegistrationNumber),
        Normalize(BillingAddress),
        Normalize(Notes));

    internal static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class CustomerInputValidator : AbstractValidator<CustomerInput>
{
    public CustomerInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);
        RuleFor(x => x.TaxRegistrationNumber).MaximumLength(50);
        RuleFor(x => x.BillingAddress).MaximumLength(500);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
