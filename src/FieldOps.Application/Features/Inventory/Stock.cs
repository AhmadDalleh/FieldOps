using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Inventory;

internal static class Stock
{
    /// <summary>The tracked level of a part at a location, adding an empty one when there is none yet.</summary>
    public static async Task<StockLevel> LevelAsync(IAppDbContext db, Guid partId, Guid locationId, CancellationToken ct)
    {
        var level = await db.StockLevels.FirstOrDefaultAsync(l => l.PartId == partId && l.StockLocationId == locationId, ct);
        if (level is not null) return level;

        level = new StockLevel(partId, locationId);
        db.StockLevels.Add(level);
        return level;
    }

    /// <summary>
    /// Saves stock changes. A clash on a level's <c>xmin</c>, two first receipts racing to create the same level, or the
    /// database's never-negative check all become a 409 the user can retry.
    /// </summary>
    public static async Task<Result> SaveAsync(IAppDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DbUpdateException)
        {
            return InventoryErrors.ConcurrencyConflict;
        }
    }

    public static Task<StockLocation?> ActiveLocationAsync(IAppDbContext db, Guid id, CancellationToken ct) =>
        db.StockLocations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id && l.IsActive, ct);

    public static Task<Part?> ActivePartAsync(IAppDbContext db, Guid id, CancellationToken ct) =>
        db.Parts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.IsActive, ct);
}
