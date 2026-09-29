using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Application.Features.Inventory;
using FieldOps.Application.Features.Notifications;
using FieldOps.Domain.Common;
using FieldOps.Domain.Inventory;
using FieldOps.Domain.WorkOrders;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.WorkOrders;

public sealed record WorkOrderPartDto(
    Guid Id, Guid PartId, string Sku, string Name, PartUnit Unit, decimal Quantity, decimal UnitPrice, decimal LineTotal,
    string LocationName, bool CanRemove);

public sealed record UsePartInput(Guid PartId, decimal Quantity);

public sealed class UsePartInputValidator : AbstractValidator<UsePartInput>
{
    public UsePartInputValidator()
    {
        RuleFor(x => x.PartId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).PrecisionScale(10, 2, true);
    }
}

public sealed record ListWorkOrderPartsQuery(Guid WorkOrderId);

public sealed record AddWorkOrderPartCommand(Guid WorkOrderId, UsePartInput Input);

public sealed record RemoveWorkOrderPartCommand(Guid WorkOrderId, Guid LineId);

public sealed class ListWorkOrderPartsHandler(IAppDbContext db, ICurrentUser user)
    : IQueryHandler<ListWorkOrderPartsQuery, Result<IReadOnlyList<WorkOrderPartDto>>>
{
    public async Task<Result<IReadOnlyList<WorkOrderPartDto>>> Handle(ListWorkOrderPartsQuery query, CancellationToken ct)
    {
        var found = await db.WorkOrders.AsNoTracking().FindAccessibleAsync(query.WorkOrderId, user, ct);
        if (found.IsFailure) return found.Error;
        return Result.Success(await WorkOrderPartReader.ReadAsync(db, found.Value, ct));
    }
}

/// <summary>
/// US-TAPP-07: takes parts from the assigned technician's van. The level's <c>xmin</c> makes two people taking the last
/// units at once end with one success and one 409, and stock never goes negative (docs/07-flows.md §5).
/// </summary>
public sealed class AddWorkOrderPartHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock, Notifier notifier)
    : ICommandHandler<AddWorkOrderPartCommand, Result<IReadOnlyList<WorkOrderPartDto>>>
{
    public async Task<Result<IReadOnlyList<WorkOrderPartDto>>> Handle(AddWorkOrderPartCommand cmd, CancellationToken ct)
    {
        var found = await db.WorkOrders.AsNoTracking().FindAccessibleAsync(cmd.WorkOrderId, user, ct);
        if (found.IsFailure) return found.Error;
        var workOrder = found.Value;

        var van = await db.StockLocations.AsNoTracking()
            .FirstOrDefaultAsync(l => l.TechnicianId != null && l.TechnicianId == workOrder.AssignedTechnicianId, ct);
        if (van is null) return InventoryErrors.NoVan;
        var part = await db.Parts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == cmd.Input.PartId, ct);
        if (part is null) return InventoryErrors.PartNotAvailable;

        var level = await db.StockLevels.FirstOrDefaultAsync(l => l.PartId == part.Id && l.StockLocationId == van.Id, ct)
            ?? new StockLevel(part.Id, van.Id);
        var used = WorkOrderPart.Use(workOrder, part, level, cmd.Input.Quantity, user.UserId, clock.GetUtcNow());
        if (used.IsFailure) return used.Error;

        db.StockMovements.Add(used.Value.Movement);
        db.WorkOrderParts.Add(used.Value.Line);

        // US-NOT-02: tell admins when this use takes the part below its reorder level.
        var elsewhere = await db.StockLevels.AsNoTracking()
            .Where(l => l.PartId == part.Id && l.StockLocationId != van.Id).SumAsync(l => l.Quantity, ct);
        var total = elsewhere + level.Quantity;
        if (part.IsLow(total) && !part.IsLow(total + cmd.Input.Quantity))
            await notifier.LowStockAsync(part.Sku, part.Name, total, part.ReorderLevel, ct);
        var saved = await Stock.SaveAsync(db, ct);
        if (saved.IsFailure) return saved.Error;
        return Result.Success(await WorkOrderPartReader.ReadAsync(db, workOrder, ct));
    }
}

/// <summary>US-TAPP-07 AC4: removing a line before completion returns the parts to where they came from.</summary>
public sealed class RemoveWorkOrderPartHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<RemoveWorkOrderPartCommand, Result<IReadOnlyList<WorkOrderPartDto>>>
{
    public async Task<Result<IReadOnlyList<WorkOrderPartDto>>> Handle(RemoveWorkOrderPartCommand cmd, CancellationToken ct)
    {
        var found = await db.WorkOrders.AsNoTracking().FindAccessibleAsync(cmd.WorkOrderId, user, ct);
        if (found.IsFailure) return found.Error;
        var line = await db.WorkOrderParts.FirstOrDefaultAsync(p => p.Id == cmd.LineId && p.WorkOrderId == cmd.WorkOrderId, ct);
        if (line is null) return WorkOrderErrors.PartLineNotFound;

        var level = await Stock.LevelAsync(db, line.PartId, line.StockLocationId, ct);
        var returned = line.ReturnTo(level, found.Value, user.UserId, clock.GetUtcNow());
        if (returned.IsFailure) return returned.Error;

        db.StockMovements.Add(returned.Value);
        db.WorkOrderParts.Remove(line);
        var saved = await Stock.SaveAsync(db, ct);
        if (saved.IsFailure) return saved.Error;
        return Result.Success(await WorkOrderPartReader.ReadAsync(db, found.Value, ct));
    }
}

internal static class WorkOrderPartReader
{
    public static async Task<IReadOnlyList<WorkOrderPartDto>> ReadAsync(IAppDbContext db, WorkOrder workOrder, CancellationToken ct)
    {
        var rows = await db.WorkOrderParts.AsNoTracking().Where(l => l.WorkOrderId == workOrder.Id)
            .Join(db.Parts, l => l.PartId, p => p.Id, (l, p) => new { l, p })
            .Join(db.StockLocations, x => x.l.StockLocationId, s => s.Id, (x, s) => new { x.l, x.p, Location = s.Name })
            .OrderBy(x => x.l.Id)
            .ToListAsync(ct);
        return rows.Select(x => new WorkOrderPartDto(x.l.Id, x.p.Id, x.p.Sku, x.p.Name, x.p.Unit, x.l.Quantity, x.l.UnitPrice,
                x.l.LineTotal, x.Location, workOrder.IsOpen))
            .ToList();
    }
}
