using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Common;
using FieldOps.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Assets;

public sealed record ListSiteAssetsQuery(Guid SiteId);

public sealed class ListSiteAssetsHandler(IAppDbContext db, TimeProvider clock)
    : IQueryHandler<ListSiteAssetsQuery, Result<IReadOnlyList<AssetDto>>>
{
    public async Task<Result<IReadOnlyList<AssetDto>>> Handle(ListSiteAssetsQuery query, CancellationToken ct)
    {
        if (!await db.Sites.AnyAsync(s => s.Id == query.SiteId, ct)) return CustomerErrors.SiteNotFound;

        return await db.Assets.AsNoTracking()
            .Where(a => a.SiteId == query.SiteId)
            .OrderBy(a => a.Name)
            .ToDtos(db, clock.Today())
            .ToListAsync(ct);
    }
}

public sealed record ListCustomerAssetsQuery(Guid CustomerId);

public sealed class ListCustomerAssetsHandler(IAppDbContext db, TimeProvider clock)
    : IQueryHandler<ListCustomerAssetsQuery, Result<IReadOnlyList<AssetDto>>>
{
    public async Task<Result<IReadOnlyList<AssetDto>>> Handle(ListCustomerAssetsQuery query, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == query.CustomerId, ct)) return CustomerErrors.NotFound;

        var siteIds = db.Sites.Where(s => s.CustomerId == query.CustomerId).Select(s => s.Id);
        var assets = await db.Assets.AsNoTracking()
            .Where(a => siteIds.Contains(a.SiteId))
            .ToDtos(db, clock.Today())
            .ToListAsync(ct);
        return assets.OrderBy(a => a.SiteName).ThenBy(a => a.Name).ToList();
    }
}
