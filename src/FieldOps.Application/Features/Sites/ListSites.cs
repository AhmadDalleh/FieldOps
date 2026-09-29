using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Sites;

public sealed record ListSitesQuery(Guid CustomerId, bool IncludeInactive = false);

public sealed class ListSitesHandler(IAppDbContext db) : IQueryHandler<ListSitesQuery, Result<IReadOnlyList<SiteDto>>>
{
    public async Task<Result<IReadOnlyList<SiteDto>>> Handle(ListSitesQuery query, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == query.CustomerId, ct)) return CustomerErrors.NotFound;

        var sites = db.Sites.AsNoTracking().Where(s => s.CustomerId == query.CustomerId);
        if (!query.IncludeInactive) sites = sites.Where(s => s.IsActive);

        return await sites
            .OrderBy(s => s.Name)
            .Select(s => new SiteDto(s.Id, s.CustomerId, s.Name, s.AddressLine1, s.AddressLine2, s.City, s.Region, s.Country,
                s.Latitude, s.Longitude, s.AccessNotes, s.IsActive))
            .ToListAsync(ct);
    }
}
