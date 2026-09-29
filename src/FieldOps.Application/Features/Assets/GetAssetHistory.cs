using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Assets;
using FieldOps.Domain.Common;
using FieldOps.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Assets;

public sealed record AssetHistoryItem(
    Guid WorkOrderId,
    string WorkOrderNumber,
    DateTimeOffset Date,
    string Type,
    string? TechnicianName,
    string Status,
    string? CompletionNotes);

public sealed record GetAssetHistoryQuery(Guid AssetId);

public sealed class GetAssetHistoryHandler(IAppDbContext db, ICurrentUser user)
    : IQueryHandler<GetAssetHistoryQuery, Result<IReadOnlyList<AssetHistoryItem>>>
{
    public async Task<Result<IReadOnlyList<AssetHistoryItem>>> Handle(GetAssetHistoryQuery query, CancellationToken ct)
    {
        if (!await db.Assets.AnyAsync(a => a.Id == query.AssetId, ct)) return AssetErrors.NotFound;

        // TODO(P4): a technician may see the history of assets on work orders assigned to them (US-AST-02 AC2).
        // Until work orders exist no technician has such an assignment.
        if (user.Role == Role.Technician) return Errors.Forbidden;

        // TODO(P4): list the asset's work orders, newest first (US-AST-02 AC1).
        return Array.Empty<AssetHistoryItem>();
    }
}
