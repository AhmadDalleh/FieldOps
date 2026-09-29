using FieldOps.Domain.Assets;

namespace FieldOps.Application.Features.Assets;

public sealed record AssetDto(
    Guid Id,
    Guid SiteId,
    string SiteName,
    AssetType AssetType,
    string Name,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    DateOnly? InstallDate,
    DateOnly? WarrantyExpiresOn,
    bool UnderWarranty,
    AssetStatus Status,
    string? Notes,
    bool IsActive);
