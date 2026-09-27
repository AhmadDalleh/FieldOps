using FieldOps.Domain.Customers;
using FluentValidation;

namespace FieldOps.Application.Features.Customers;

public sealed record ContactInput(string Name, string? Email, string? Phone, string? JobTitle, bool IsPrimary)
{
    public ContactDetails ToDetails() => new(
        Name.Trim(),
        CustomerInput.Normalize(Email),
        CustomerInput.Normalize(Phone),
        CustomerInput.Normalize(JobTitle),
        IsPrimary);
}

public sealed class ContactInputValidator : AbstractValidator<ContactInput>
{
    public ContactInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.JobTitle).MaximumLength(100);
    }
}

public sealed record ContactDto(Guid Id, string Name, string? Email, string? Phone, string? JobTitle, bool IsPrimary)
{
    public static ContactDto From(CustomerContact c) => new(c.Id, c.Name, c.Email, c.Phone, c.JobTitle, c.IsPrimary);
}
