using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Technicians;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.WorkOrders;

public sealed record UpdateWorkOrderCommand(Guid Id, UpdateWorkOrderInput Input);

public sealed class UpdateWorkOrderHandler(IAppDbContext db, ICurrentUser user, WorkOrderReader reader)
    : ICommandHandler<UpdateWorkOrderCommand, Result<WorkOrderDto>>
{
    public async Task<Result<WorkOrderDto>> Handle(UpdateWorkOrderCommand cmd, CancellationToken ct)
    {
        var found = await db.WorkOrders.FindAccessibleAsync(cmd.Id, user, ct);
        if (found.IsFailure) return found.Error;
        var workOrder = found.Value;
        var input = cmd.Input;

        if (input.AssetId is { } assetId && !await db.Assets.AnyAsync(a => a.Id == assetId && a.SiteId == workOrder.SiteId, ct))
            return WorkOrderErrors.AssetNotOfSite;

        if (input.RequiredSkillId is { } skillId && !await db.Skills.AnyAsync(s => s.Id == skillId, ct))
            return TechnicianErrors.SkillNotFound;

        // Compare against the version the client loaded, not the one just read (US-WO-04 AC2).
        if (workOrder.Version != input.Version) return WorkOrderErrors.ConcurrencyConflict;
        db.Entry(workOrder).Property(w => w.Version).OriginalValue = input.Version;

        var result = workOrder.Update(WorkOrderInputRules.ToDetails(
            input.Title, input.Description, input.Type, input.Priority, input.DueBy, input.AssetId, input.RequiredSkillId));
        if (result.IsFailure) return result.Error;

        var saved = await db.SaveAsync(ct);
        if (saved.IsFailure) return saved.Error;
        return await reader.ReadAsync(workOrder.Id, ct);
    }
}
