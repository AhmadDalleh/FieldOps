using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.WorkOrders;

public sealed record CreateWorkOrderCommand(CreateWorkOrderInput Input);

public sealed class CreateWorkOrderHandler(
    IAppDbContext db, INumberSequence numbers, ICurrentUser user, TimeProvider clock, WorkOrderReader reader)
    : ICommandHandler<CreateWorkOrderCommand, Result<WorkOrderDto>>
{
    public const string SequenceName = "WorkOrder";

    public async Task<Result<WorkOrderDto>> Handle(CreateWorkOrderCommand cmd, CancellationToken ct)
    {
        var input = cmd.Input;
        if (!await db.Customers.AnyAsync(c => c.Id == input.CustomerId && c.IsActive, ct))
            return WorkOrderErrors.CustomerNotAvailable;
        if (!await db.Sites.AnyAsync(s => s.Id == input.SiteId && s.CustomerId == input.CustomerId && s.IsActive, ct))
            return WorkOrderErrors.SiteNotOfCustomer;
        if (input.AssetId is { } assetId && !await db.Assets.AnyAsync(a => a.Id == assetId && a.SiteId == input.SiteId, ct))
            return WorkOrderErrors.AssetNotOfSite;

        var template = await db.ChecklistTemplates.AsNoTracking()
            .Include(t => t.Items)
            .Where(t => t.IsActive && t.WorkOrderType == input.Type)
            .OrderBy(t => t.Name)
            .FirstOrDefaultAsync(ct);
        var tasks = template?.Items.OrderBy(i => i.SortOrder).Select(i => i.Description) ?? [];

        // The number comes from the same transaction as the insert, so a failed save does not use it up.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var next = await numbers.NextAsync(SequenceName, ct);
        var details = WorkOrderInputRules.ToDetails(input.Title, input.Description, input.Type, input.Priority, input.DueBy, input.AssetId);
        var workOrder = WorkOrder.Create($"WO-{next:000000}", input.CustomerId, input.SiteId, details, tasks, user.UserId, clock.GetUtcNow());
        db.WorkOrders.Add(workOrder);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await reader.ReadAsync(workOrder.Id, ct);
    }
}
