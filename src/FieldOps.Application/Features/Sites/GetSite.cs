using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Sites;

public sealed record GetSiteQuery(Guid Id);

public sealed class GetSiteHandler(IAppDbContext db) : IQueryHandler<GetSiteQuery, Result<SiteDto>>
{
    public async Task<Result<SiteDto>> Handle(GetSiteQuery query, CancellationToken ct)
    {
        var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.Id == query.Id, ct);
        return site is null ? CustomerErrors.SiteNotFound : SiteDto.From(site);
    }
}
