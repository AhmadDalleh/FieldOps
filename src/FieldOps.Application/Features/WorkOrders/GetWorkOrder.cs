using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.WorkOrders;

public sealed record GetWorkOrderQuery(Guid Id);

public sealed class GetWorkOrderHandler(IAppDbContext db, ICurrentUser user, WorkOrderReader reader)
    : IQueryHandler<GetWorkOrderQuery, Result<WorkOrderDto>>
{
    public async Task<Result<WorkOrderDto>> Handle(GetWorkOrderQuery query, CancellationToken ct)
    {
        var found = await db.WorkOrders.AsNoTracking().FindAccessibleAsync(query.Id, user, ct);
        if (found.IsFailure) return found.Error;
        return await reader.ReadAsync(query.Id, ct);
    }
}
