using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Common;
using FieldOps.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Me;

public sealed record VanStockItem(Guid PartId, string Sku, string Name, PartUnit Unit, decimal Quantity, decimal UnitPrice);

public sealed record VanStockQuery;

/// <summary>US-TAPP-07 AC1: only parts the technician's van actually holds.</summary>
public sealed class VanStockHandler(IAppDbContext db, ICurrentUser user) : IQueryHandler<VanStockQuery, Result<IReadOnlyList<VanStockItem>>>
{
    public async Task<Result<IReadOnlyList<VanStockItem>>> Handle(VanStockQuery query, CancellationToken ct)
    {
        if (user.TechnicianId is not { } technicianId) return Errors.Forbidden;

        var items = await db.StockLevels.AsNoTracking()
            .Where(l => l.Quantity > 0)
            .Join(db.StockLocations.Where(s => s.TechnicianId == technicianId), l => l.StockLocationId, s => s.Id, (l, s) => l)
            .Join(db.Parts.Where(p => p.IsActive), l => l.PartId, p => p.Id, (l, p) => new { l.Quantity, p })
            .OrderBy(x => x.p.Name)
            .Select(x => new VanStockItem(x.p.Id, x.p.Sku, x.p.Name, x.p.Unit, x.Quantity, x.p.UnitPrice))
            .ToListAsync(ct);
        return items;
    }
}
