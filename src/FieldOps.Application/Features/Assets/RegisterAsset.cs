using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Assets;
using FieldOps.Domain.Common;
using FieldOps.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Assets;

public sealed record RegisterAssetCommand(Guid SiteId, AssetInput Input);

public sealed class RegisterAssetHandler(IAppDbContext db, TimeProvider clock) : ICommandHandler<RegisterAssetCommand, Result<AssetDto>>
{
    public async Task<Result<AssetDto>> Handle(RegisterAssetCommand cmd, CancellationToken ct)
    {
        var site = await (
            from s in db.Sites
            join c in db.Customers on s.CustomerId equals c.Id
            where s.Id == cmd.SiteId
            select new { SiteActive = s.IsActive, CustomerActive = c.IsActive }).FirstOrDefaultAsync(ct);
        if (site is null) return CustomerErrors.SiteNotFound;
        if (!site.SiteActive || !site.CustomerActive) return AssetErrors.SiteNotAvailable;

        var details = cmd.Input.ToDetails();
        if (await DuplicateSerialCheck.IsDuplicateAsync(db, details, null, ct)) return AssetErrors.DuplicateSerial;

        var asset = Asset.Create(cmd.SiteId, details);
        if (asset.IsFailure) return asset.Error;

        db.Assets.Add(asset.Value);
        await db.SaveChangesAsync(ct);
        return await db.Assets.AsNoTracking().Where(a => a.Id == asset.Value.Id).ToDtos(db, clock.Today()).SingleAsync(ct);
    }
}
