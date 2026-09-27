using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Inventory;

public sealed record StockMovementDto(
    Guid Id, DateTimeOffset CreatedAt, StockMovementType Type, Guid PartId, string Sku, string PartName, decimal Quantity,
    string? FromLocation, string? ToLocation, Guid? WorkOrderId, string? WorkOrderNumber, string? Reason, string CreatedByName);

public sealed record ListMovementsQuery(
    PageRequest Page, Guid? PartId = null, StockMovementType? Type = null, Guid? LocationId = null, DateOnly? From = null,
    DateOnly? To = null);

/// <summary>US-INV-06: newest first, filtered by part, type, location (either side) and Dubai dates.</summary>
public sealed class ListMovementsHandler(IAppDbContext db, IIdentityService identity)
    : IQueryHandler<ListMovementsQuery, PagedResult<StockMovementDto>>
{
    public async Task<PagedResult<StockMovementDto>> Handle(ListMovementsQuery query, CancellationToken ct)
    {
        var movements = db.StockMovements.AsNoTracking();
        if (query.PartId is { } partId) movements = movements.Where(m => m.PartId == partId);
        if (query.Type is { } type) movements = movements.Where(m => m.Type == type);
        if (query.LocationId is { } location) movements = movements.Where(m => m.FromLocationId == location || m.ToLocationId == location);
        if (query.From is { } from)
        {
            var start = BusinessCalendar.DayRange(from).From;
            movements = movements.Where(m => m.CreatedAt >= start);
        }
        if (query.To is { } to)
        {
            var end = BusinessCalendar.DayRange(to).To;
            movements = movements.Where(m => m.CreatedAt < end);
        }

        var page = await movements.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).ToPagedResultAsync(query.Page, ct);

        var partIds = page.Items.Select(m => m.PartId).Distinct().ToList();
        var parts = await db.Parts.AsNoTracking().Where(p => partIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var locations = await db.StockLocations.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.Name, ct);
        var workOrderIds = page.Items.Where(m => m.WorkOrderId != null).Select(m => m.WorkOrderId!.Value).Distinct().ToList();
        var numbers = await db.WorkOrders.AsNoTracking().Where(w => workOrderIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => w.Number, ct);
        var people = await identity.FindByIdsAsync(page.Items.Select(m => m.CreatedBy).Distinct(), ct);

        string? Location(Guid? id) => id is { } x && locations.TryGetValue(x, out var name) ? name : null;
        var items = page.Items.Select(m => new StockMovementDto(m.Id, m.CreatedAt, m.Type, m.PartId, parts[m.PartId].Sku,
                parts[m.PartId].Name, m.Quantity, Location(m.FromLocationId), Location(m.ToLocationId), m.WorkOrderId,
                m.WorkOrderId is { } w ? numbers.GetValueOrDefault(w) : null, m.Reason,
                people.TryGetValue(m.CreatedBy, out var p) ? p.FullName : ""))
            .ToList();
        return new PagedResult<StockMovementDto>(items, page.Page, page.PageSize, page.TotalCount);
    }
}
