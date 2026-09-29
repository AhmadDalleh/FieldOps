using FieldOps.Application.Abstractions;
using FieldOps.Domain.Assets;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Assets;

internal static class DuplicateSerialCheck
{
    /// <summary>True when another asset has the same manufacturer and serial number (case-insensitive).</summary>
    public static Task<bool> IsDuplicateAsync(IAppDbContext db, AssetDetails details, Guid? exceptAssetId, CancellationToken ct)
    {
        if (details.SerialNumber is null) return Task.FromResult(false);

        var serial = details.SerialNumber.ToLower();
        var manufacturer = details.Manufacturer?.ToLower();
        return db.Assets.AnyAsync(a =>
            a.Id != exceptAssetId &&
            a.SerialNumber != null && a.SerialNumber.ToLower() == serial &&
            (manufacturer == null ? a.Manufacturer == null : a.Manufacturer != null && a.Manufacturer.ToLower() == manufacturer), ct);
    }
}
