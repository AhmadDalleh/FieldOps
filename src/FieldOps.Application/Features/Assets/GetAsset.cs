using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Assets;
using FieldOps.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Assets;

public sealed record GetAssetQuery(Guid Id);

public sealed class GetAssetHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock) : IQueryHandler<GetAssetQuery, Result<AssetDto>>
{
    public async Task<Result<AssetDto>> Handle(GetAssetQuery query, CancellationToken ct)
    {
        if (!await AssetAccess.CanReadAsync(db, user, query.Id, ct))
            return await db.Assets.AnyAsync(a => a.Id == query.Id, ct) ? Errors.Forbidden : AssetErrors.NotFound;

        var asset = await db.Assets.AsNoTracking().Where(a => a.Id == query.Id).ToDtos(db, clock.Today()).FirstOrDefaultAsync(ct);
        return asset is null ? AssetErrors.NotFound : asset;
    }
}
