using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Common;
using FieldOps.Domain.Inventory;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Inventory;

public sealed record StockLocationDto(Guid Id, string Name, StockLocationType Type, Guid? TechnicianId, bool IsActive);

public sealed record StockAt(Guid LocationId, decimal Quantity);

public sealed record StockRow(
    Guid PartId, string Sku, string Name, PartUnit Unit, decimal ReorderLevel, decimal Total, bool IsLow, IReadOnlyList<StockAt> Levels);

public sealed record ReceiveInput(Guid PartId, Guid LocationId, decimal Quantity);

public sealed record TransferInput(Guid PartId, Guid FromLocationId, Guid ToLocationId, decimal Quantity);

public sealed record AdjustInput(Guid PartId, Guid LocationId, decimal NewQuantity, string? Reason);

public sealed class ReceiveInputValidator : AbstractValidator<ReceiveInput>
{
    public ReceiveInputValidator()
    {
        RuleFor(x => x.PartId).NotEmpty();
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).PrecisionScale(10, 2, true);
    }
}

public sealed class TransferInputValidator : AbstractValidator<TransferInput>
{
    public TransferInputValidator()
    {
        RuleFor(x => x.PartId).NotEmpty();
        RuleFor(x => x.FromLocationId).NotEmpty();
        RuleFor(x => x.ToLocationId).NotEmpty().NotEqual(x => x.FromLocationId).WithMessage("Choose two different locations.");
        RuleFor(x => x.Quantity).GreaterThan(0).PrecisionScale(10, 2, true);
    }
}

public sealed class AdjustInputValidator : AbstractValidator<AdjustInput>
{
    public AdjustInputValidator()
    {
        RuleFor(x => x.PartId).NotEmpty();
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.NewQuantity).GreaterThanOrEqualTo(0).PrecisionScale(10, 2, true);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed record ListStockLocationsQuery;

public sealed record GetStockQuery(Guid? LocationId = null, bool LowOnly = false);

public sealed record ReceiveStockCommand(ReceiveInput Input);

public sealed record TransferStockCommand(TransferInput Input);

public sealed record AdjustStockCommand(AdjustInput Input);

public sealed class ListStockLocationsHandler(IAppDbContext db) : IQueryHandler<ListStockLocationsQuery, IReadOnlyList<StockLocationDto>>
{
    public async Task<IReadOnlyList<StockLocationDto>> Handle(ListStockLocationsQuery query, CancellationToken ct) =>
        await db.StockLocations.AsNoTracking()
            .OrderBy(l => l.Type == StockLocationType.Van).ThenBy(l => l.Name) // the enum is stored as text, so sort explicitly
            .Select(l => new StockLocationDto(l.Id, l.Name, l.Type, l.TechnicianId, l.IsActive))
            .ToListAsync(ct);
}

/// <summary>US-INV-02: stock of every active part per location, flagging parts whose total is below the reorder level.</summary>
public sealed class GetStockHandler(IAppDbContext db) : IQueryHandler<GetStockQuery, IReadOnlyList<StockRow>>
{
    public async Task<IReadOnlyList<StockRow>> Handle(GetStockQuery query, CancellationToken ct)
    {
        var parts = await db.Parts.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(ct);
        var levels = await db.StockLevels.AsNoTracking().Where(l => l.Quantity != 0).ToListAsync(ct);
        var byPart = levels.ToLookup(l => l.PartId);

        var rows = parts.Select(p =>
        {
            var total = byPart[p.Id].Sum(l => l.Quantity);
            var shown = byPart[p.Id].Where(l => query.LocationId is null || l.StockLocationId == query.LocationId)
                .Select(l => new StockAt(l.StockLocationId, l.Quantity)).ToList();
            return new StockRow(p.Id, p.Sku, p.Name, p.Unit, p.ReorderLevel, total, p.IsLow(total), shown);
        });
        if (query.LowOnly) rows = rows.Where(r => r.IsLow);
        return rows.ToList();
    }
}

/// <summary>US-INV-03: stock arrives at a warehouse.</summary>
public sealed class ReceiveStockHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<ReceiveStockCommand, Result<StockRow>>
{
    public async Task<Result<StockRow>> Handle(ReceiveStockCommand cmd, CancellationToken ct)
    {
        var input = cmd.Input;
        if (await Stock.ActivePartAsync(db, input.PartId, ct) is null) return InventoryErrors.PartNotAvailable;
        var location = await Stock.ActiveLocationAsync(db, input.LocationId, ct);
        if (location is null) return InventoryErrors.LocationNotFound;
        if (location.Type != StockLocationType.Warehouse) return InventoryErrors.ReceiveIntoWarehouse;

        var level = await Stock.LevelAsync(db, input.PartId, input.LocationId, ct);
        var movement = StockLedger.Receive(level, input.Quantity, user.UserId, clock.GetUtcNow());
        if (movement.IsFailure) return movement.Error;
        db.StockMovements.Add(movement.Value);
        return await SaveAndReadAsync(db, input.PartId, ct);
    }

    internal static async Task<Result<StockRow>> SaveAndReadAsync(IAppDbContext db, Guid partId, CancellationToken ct)
    {
        var saved = await Stock.SaveAsync(db, ct);
        if (saved.IsFailure) return saved.Error;
        var rows = await new GetStockHandler(db).Handle(new GetStockQuery(), ct);
        return rows.Single(r => r.PartId == partId);
    }
}

/// <summary>US-INV-04: moves stock between locations in one transaction; the source must hold enough.</summary>
public sealed class TransferStockHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<TransferStockCommand, Result<StockRow>>
{
    public async Task<Result<StockRow>> Handle(TransferStockCommand cmd, CancellationToken ct)
    {
        var input = cmd.Input;
        if (await Stock.ActivePartAsync(db, input.PartId, ct) is null) return InventoryErrors.PartNotAvailable;
        if (await Stock.ActiveLocationAsync(db, input.FromLocationId, ct) is null
            || await Stock.ActiveLocationAsync(db, input.ToLocationId, ct) is null)
            return InventoryErrors.LocationNotFound;

        var from = await Stock.LevelAsync(db, input.PartId, input.FromLocationId, ct);
        var to = await Stock.LevelAsync(db, input.PartId, input.ToLocationId, ct);
        var movement = StockLedger.Transfer(from, to, input.Quantity, user.UserId, clock.GetUtcNow());
        if (movement.IsFailure) return movement.Error;
        db.StockMovements.Add(movement.Value);
        return await ReceiveStockHandler.SaveAndReadAsync(db, input.PartId, ct);
    }
}

/// <summary>US-INV-05: Admin sets the counted quantity with a reason.</summary>
public sealed class AdjustStockHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<AdjustStockCommand, Result<StockRow>>
{
    public async Task<Result<StockRow>> Handle(AdjustStockCommand cmd, CancellationToken ct)
    {
        var input = cmd.Input;
        if (!await db.Parts.AnyAsync(p => p.Id == input.PartId, ct)) return InventoryErrors.PartNotFound;
        if (await Stock.ActiveLocationAsync(db, input.LocationId, ct) is null) return InventoryErrors.LocationNotFound;

        var level = await Stock.LevelAsync(db, input.PartId, input.LocationId, ct);
        var movement = StockLedger.Adjust(level, input.NewQuantity, input.Reason, user.UserId, clock.GetUtcNow());
        if (movement.IsFailure) return movement.Error;
        db.StockMovements.Add(movement.Value);
        var saved = await Stock.SaveAsync(db, ct);
        if (saved.IsFailure) return saved.Error;

        var part = await db.Parts.AsNoTracking().SingleAsync(p => p.Id == input.PartId, ct);
        var levels = await db.StockLevels.AsNoTracking().Where(l => l.PartId == part.Id && l.Quantity != 0).ToListAsync(ct);
        var total = levels.Sum(l => l.Quantity);
        return new StockRow(part.Id, part.Sku, part.Name, part.Unit, part.ReorderLevel, total, part.IsLow(total),
            levels.Select(l => new StockAt(l.StockLocationId, l.Quantity)).ToList());
    }
}
