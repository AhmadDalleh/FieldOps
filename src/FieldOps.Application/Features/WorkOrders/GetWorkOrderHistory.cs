using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.WorkOrders;

public sealed record GetWorkOrderHistoryQuery(Guid Id);

public sealed class GetWorkOrderHistoryHandler(IAppDbContext db, ICurrentUser user, IIdentityService identity)
    : IQueryHandler<GetWorkOrderHistoryQuery, Result<IReadOnlyList<WorkOrderHistoryItem>>>
{
    public async Task<Result<IReadOnlyList<WorkOrderHistoryItem>>> Handle(GetWorkOrderHistoryQuery query, CancellationToken ct)
    {
        var found = await db.WorkOrders.AsNoTracking().FindAccessibleAsync(query.Id, user, ct);
        if (found.IsFailure) return found.Error;

        var rows = await db.WorkOrderStatusHistory.AsNoTracking()
            .Where(h => h.WorkOrderId == query.Id)
            .OrderBy(h => h.ChangedAt).ThenBy(h => h.Id)
            .ToListAsync(ct);
        var users = await identity.FindByIdsAsync(rows.Select(r => r.ChangedBy), ct);

        return rows
            .Select(r => new WorkOrderHistoryItem(r.FromStatus, r.ToStatus,
                users.TryGetValue(r.ChangedBy, out var u) ? u.FullName : "", r.ChangedAt, r.Note, r.Latitude, r.Longitude))
            .ToList();
    }
}
