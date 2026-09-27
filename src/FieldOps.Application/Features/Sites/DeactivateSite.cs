using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Customers;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Sites;

public sealed record DeactivateSiteCommand(Guid Id);

public sealed class DeactivateSiteHandler(IAppDbContext db) : ICommandHandler<DeactivateSiteCommand, Result>
{
    public async Task<Result> Handle(DeactivateSiteCommand cmd, CancellationToken ct)
    {
        var site = await db.Sites.FirstOrDefaultAsync(s => s.Id == cmd.Id, ct);
        if (site is null) return CustomerErrors.SiteNotFound;

        if (await db.WorkOrders.AnyAsync(w => w.SiteId == cmd.Id && !WorkOrder.ClosedStatuses.Contains(w.Status), ct))
            return CustomerErrors.SiteHasOpenWorkOrders;

        site.Deactivate();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
