using FieldOps.Application.Abstractions;
using FieldOps.Domain.Assets;

namespace FieldOps.Application.Features.Assets;

internal static class AssetQueries
{
    /// <summary>Projects assets with their site name and warranty flag for the given business date.</summary>
    public static IQueryable<AssetDto> ToDtos(this IQueryable<Asset> assets, IAppDbContext db, DateOnly today) =>
        from a in assets
        join s in db.Sites on a.SiteId equals s.Id
        select new AssetDto(
            a.Id, a.SiteId, s.Name, a.AssetType, a.Name, a.Manufacturer, a.Model, a.SerialNumber,
            a.InstallDate, a.WarrantyExpiresOn, a.WarrantyExpiresOn != null && today <= a.WarrantyExpiresOn,
            a.Status, a.Notes, a.IsActive);
}
