using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Application.Features.WorkOrders;
using FieldOps.Domain.Assets;
using FieldOps.Domain.Common;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Assets;

public sealed record AssetHistoryItem(
    Guid WorkOrderId,
    string WorkOrderNumber,
    DateTimeOffset Date,
    WorkOrderType Type,
    string? TechnicianName,
    WorkOrderStatus Status,
    string? CompletionNotes);

public sealed record GetAssetHistoryQuery(Guid AssetId);

public sealed class GetAssetHistoryHandler(IAppDbContext db, ICurrentUser user, WorkOrderReader reader)
    : IQueryHandler<GetAssetHistoryQuery, Result<IReadOnlyList<AssetHistoryItem>>>
{
    public async Task<Result<IReadOnlyList<AssetHistoryItem>>> Handle(GetAssetHistoryQuery query, CancellationToken ct)
    {
        if (!await db.Assets.AnyAsync(a => a.Id == query.AssetId, ct)) return AssetErrors.NotFound;
        if (!await AssetAccess.CanReadAsync(db, user, query.AssetId, ct)) return Errors.Forbidden;

        // Newest first: completed jobs by completion time, the rest by when they were raised (US-AST-02 AC1).
        var rows = await db.WorkOrders.AsNoTracking()
            .Where(w => w.AssetId == query.AssetId)
            .Select(w => new { w.Id, w.Number, Date = w.CompletedAt ?? w.CreatedAt, w.Type, w.AssignedTechnicianId, w.Status, w.CompletionNotes })
            .OrderByDescending(w => w.Date)
            .ToListAsync(ct);
        var technicians = await reader.TechniciansAsync(rows.Select(r => r.AssignedTechnicianId), ct);

        return rows
            .Select(r => new AssetHistoryItem(r.Id, r.Number, r.Date, r.Type,
                r.AssignedTechnicianId is { } t && technicians.TryGetValue(t, out var tech) ? tech.Name : null,
                r.Status, r.CompletionNotes))
            .ToList();
    }
}

internal static class AssetAccess
{
    /// <summary>Office reads any asset; a technician only assets on work orders assigned to them (US-AST-02 AC2).</summary>
    public static async Task<bool> CanReadAsync(IAppDbContext db, ICurrentUser user, Guid assetId, CancellationToken ct) =>
        user.IsOffice() ||
        (user.TechnicianId is { } own && await db.WorkOrders.AnyAsync(w => w.AssetId == assetId && w.AssignedTechnicianId == own, ct));
}
