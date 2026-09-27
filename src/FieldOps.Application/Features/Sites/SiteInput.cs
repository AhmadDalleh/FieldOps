using FieldOps.Application.Features.Customers;
using FieldOps.Domain.Customers;
using FluentValidation;

namespace FieldOps.Application.Features.Sites;

public sealed record SiteInput(
    string Name,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string? Region,
    string? Country,
    double? Latitude,
    double? Longitude,
    string? AccessNotes)
{
    public SiteDetails ToDetails() => new(
        Name.Trim(),
        AddressLine1.Trim(),
        CustomerInput.Normalize(AddressLine2),
        City.Trim(),
        CustomerInput.Normalize(Region),
        CustomerInput.Normalize(Country)?.ToUpperInvariant(),
        Latitude,
        Longitude,
        CustomerInput.Normalize(AccessNotes));
}

public sealed class SiteInputValidator : AbstractValidator<SiteInput>
{
    public SiteInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AddressLine1).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AddressLine2).MaximumLength(200);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Region).MaximumLength(100);
        RuleFor(x => x.Country).Length(2).When(x => !string.IsNullOrWhiteSpace(x.Country));
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.Longitude).NotNull().When(x => x.Latitude.HasValue).WithMessage("Latitude and longitude must be given together.");
        RuleFor(x => x.Latitude).NotNull().When(x => x.Longitude.HasValue).WithMessage("Latitude and longitude must be given together.");
        RuleFor(x => x.AccessNotes).MaximumLength(2000);
    }
}

public sealed record SiteDto(
    Guid Id,
    Guid CustomerId,
    string Name,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string? Region,
    string Country,
    double? Latitude,
    double? Longitude,
    string? AccessNotes,
    bool IsActive)
{
    public static SiteDto From(Site s) =>
        new(s.Id, s.CustomerId, s.Name, s.AddressLine1, s.AddressLine2, s.City, s.Region, s.Country, s.Latitude, s.Longitude, s.AccessNotes, s.IsActive);
}
