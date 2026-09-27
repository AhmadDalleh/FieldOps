using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Assets;
using FieldOps.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Assets;

public sealed record UpdateAssetCommand(Guid Id, AssetInput Input);

public sealed class UpdateAssetHandler(IAppDbContext db, TimeProvider clock) : ICommandHandler<UpdateAssetCommand, Result<AssetDto>>
{
    public async Task<Result<AssetDto>> Handle(UpdateAssetCommand cmd, CancellationToken ct)
    {
        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == cmd.Id, ct);
        if (asset is null) return AssetErrors.NotFound;

        var details = cmd.Input.ToDetails();
        if (await DuplicateSerialCheck.IsDuplicateAsync(db, details, asset.Id, ct)) return AssetErrors.DuplicateSerial;

        var result = asset.Update(details);
        if (result.IsFailure) return result.Error;

        await db.SaveChangesAsync(ct);
        return await db.Assets.AsNoTracking().Where(a => a.Id == asset.Id).ToDtos(db, clock.Today()).SingleAsync(ct);
    }
}
