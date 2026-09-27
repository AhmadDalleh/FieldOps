using FieldOps.Domain.Common;

namespace FieldOps.Domain.Assets;

public enum AssetType { AC, Chiller, Generator, Elevator, Pump, Boiler, Electrical, Plumbing, Other }

public enum AssetStatus { Active, OutOfService, Retired }

public sealed class Asset : AuditableEntity
{
    private Asset() { }

    public Guid SiteId { get; private init; }
    public AssetType AssetType { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Manufacturer { get; private set; }
    public string? Model { get; private set; }
    public string? SerialNumber { get; private set; }
    public DateOnly? InstallDate { get; private set; }
    public DateOnly? WarrantyExpiresOn { get; private set; }
    public AssetStatus Status { get; private set; } = AssetStatus.Active;
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static Result<Asset> Create(Guid siteId, AssetDetails details)
    {
        var asset = new Asset { SiteId = siteId };
        var result = asset.Update(details);
        return result.IsSuccess ? asset : result.Error;
    }

    public Result Update(AssetDetails details)
    {
        if (details.InstallDate is { } installed && details.WarrantyExpiresOn is { } expires && expires < installed)
            return Error.Validation("Asset.WarrantyBeforeInstall", "The warranty cannot end before the install date.");

        AssetType = details.AssetType;
        Name = details.Name;
        Manufacturer = details.Manufacturer;
        Model = details.Model;
        SerialNumber = details.SerialNumber;
        InstallDate = details.InstallDate;
        WarrantyExpiresOn = details.WarrantyExpiresOn;
        Status = details.Status;
        Notes = details.Notes;
        IsActive = details.Status != AssetStatus.Retired;
        return Result.Success();
    }

    /// <summary>An asset is under warranty up to and including its expiry date.</summary>
    public static bool IsUnderWarranty(DateOnly? warrantyExpiresOn, DateOnly today) =>
        warrantyExpiresOn is { } expires && today <= expires;
}

public sealed record AssetDetails(
    AssetType AssetType,
    string Name,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    DateOnly? InstallDate,
    DateOnly? WarrantyExpiresOn,
    AssetStatus Status,
    string? Notes);
