using FieldOps.Application.Features.Customers;
using FieldOps.Domain.Assets;
using FluentValidation;

namespace FieldOps.Application.Features.Assets;

public sealed record AssetInput(
    AssetType AssetType,
    string Name,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    DateOnly? InstallDate,
    DateOnly? WarrantyExpiresOn,
    AssetStatus Status = AssetStatus.Active,
    string? Notes = null)
{
    public AssetDetails ToDetails() => new(
        AssetType,
        Name.Trim(),
        CustomerInput.Normalize(Manufacturer),
        CustomerInput.Normalize(Model),
        CustomerInput.Normalize(SerialNumber),
        InstallDate,
        WarrantyExpiresOn,
        Status,
        CustomerInput.Normalize(Notes));
}

public sealed class AssetInputValidator : AbstractValidator<AssetInput>
{
    public AssetInputValidator()
    {
        RuleFor(x => x.AssetType).IsInEnum();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Manufacturer).MaximumLength(100);
        RuleFor(x => x.Model).MaximumLength(100);
        RuleFor(x => x.SerialNumber).MaximumLength(100);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.WarrantyExpiresOn)
            .GreaterThanOrEqualTo(x => x.InstallDate)
            .When(x => x.InstallDate.HasValue && x.WarrantyExpiresOn.HasValue)
            .WithMessage("The warranty cannot end before the install date.");
    }
}
