using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Common;
using FieldOps.Domain.Inventory;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Inventory;

public sealed record PartDto(
    Guid Id, string Sku, string Name, string? Description, PartUnit Unit, decimal UnitCost, decimal UnitPrice, decimal ReorderLevel,
    bool IsActive, decimal TotalQuantity, bool IsLow);

public sealed record PartInput(
    string Sku, string Name, string? Description, PartUnit Unit, decimal UnitCost, decimal UnitPrice, decimal ReorderLevel,
    bool IsActive = true);

public sealed class PartInputValidator : AbstractValidator<PartInput>
{
    public PartInputValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Unit).IsInEnum();
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, true);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, true);
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0).PrecisionScale(10, 2, true);
    }
}

public sealed record ListPartsQuery(PageRequest Page, bool IncludeInactive = false, bool LowOnly = false);

public sealed record GetPartQuery(Guid Id);

public sealed record CreatePartCommand(PartInput Input);

public sealed record UpdatePartCommand(Guid Id, PartInput Input);

public sealed class ListPartsHandler(IAppDbContext db) : IQueryHandler<ListPartsQuery, PagedResult<PartDto>>
{
    public Task<PagedResult<PartDto>> Handle(ListPartsQuery query, CancellationToken ct)
    {
        var parts = db.Parts.AsNoTracking();
        if (!query.IncludeInactive) parts = parts.Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Page.Search))
        {
            var pattern = query.Page.Search.ToContainsPattern();
            parts = parts.Where(p =>
                EF.Functions.Like(p.Name.ToLower(), pattern, QueryableExtensions.LikeEscape) ||
                EF.Functions.Like(p.Sku.ToLower(), pattern, QueryableExtensions.LikeEscape));
        }

        var rows = PartReader.Rows(db, parts);
        if (query.LowOnly) rows = rows.Where(r => r.Total < r.Part.ReorderLevel);
        rows = query.Page.Sort switch
        {
            "sku" => rows.OrderBy(r => r.Part.Sku),
            "-name" => rows.OrderByDescending(r => r.Part.Name),
            "stock" => rows.OrderBy(r => r.Total).ThenBy(r => r.Part.Name),
            _ => rows.OrderBy(r => r.Part.Name).ThenBy(r => r.Part.Sku),
        };
        return rows.Select(PartReader.ToDto).ToPagedResultAsync(query.Page, ct);
    }
}

public sealed class GetPartHandler(IAppDbContext db) : IQueryHandler<GetPartQuery, Result<PartDto>>
{
    public async Task<Result<PartDto>> Handle(GetPartQuery query, CancellationToken ct)
    {
        var part = await PartReader.Rows(db, db.Parts.AsNoTracking().Where(p => p.Id == query.Id))
            .Select(PartReader.ToDto).FirstOrDefaultAsync(ct);
        return part is null ? InventoryErrors.PartNotFound : part;
    }
}

/// <summary>US-INV-01: Admin only; SKUs are unique.</summary>
public sealed class CreatePartHandler(IAppDbContext db, GetPartHandler reader) : ICommandHandler<CreatePartCommand, Result<PartDto>>
{
    public async Task<Result<PartDto>> Handle(CreatePartCommand cmd, CancellationToken ct)
    {
        var sku = Part.NormalizeSku(cmd.Input.Sku);
        if (await db.Parts.AnyAsync(p => p.Sku == sku, ct)) return InventoryErrors.DuplicateSku;

        var created = Part.Create(PartReader.Details(cmd.Input));
        if (created.IsFailure) return created.Error;
        db.Parts.Add(created.Value);
        await db.SaveChangesAsync(ct);
        return await reader.Handle(new GetPartQuery(created.Value.Id), ct);
    }
}

public sealed class UpdatePartHandler(IAppDbContext db, GetPartHandler reader) : ICommandHandler<UpdatePartCommand, Result<PartDto>>
{
    public async Task<Result<PartDto>> Handle(UpdatePartCommand cmd, CancellationToken ct)
    {
        var part = await db.Parts.FirstOrDefaultAsync(p => p.Id == cmd.Id, ct);
        if (part is null) return InventoryErrors.PartNotFound;
        var sku = Part.NormalizeSku(cmd.Input.Sku);
        if (await db.Parts.AnyAsync(p => p.Sku == sku && p.Id != cmd.Id, ct)) return InventoryErrors.DuplicateSku;

        var updated = part.Update(PartReader.Details(cmd.Input));
        if (updated.IsFailure) return updated.Error;
        await db.SaveChangesAsync(ct);
        return await reader.Handle(new GetPartQuery(part.Id), ct);
    }
}

internal static class PartReader
{
    public static PartDetails Details(PartInput i) =>
        new(i.Sku, i.Name, i.Description, i.Unit, i.UnitCost, i.UnitPrice, i.ReorderLevel, i.IsActive);

    /// <summary>Each part with its total stock across all locations, still filterable and sortable in SQL.</summary>
    public static IQueryable<PartRow> Rows(IAppDbContext db, IQueryable<Part> parts) =>
        parts.Select(p => new PartRow
        {
            Part = p,
            Total = db.StockLevels.Where(l => l.PartId == p.Id).Sum(l => (decimal?)l.Quantity) ?? 0,
        });

    public static readonly System.Linq.Expressions.Expression<Func<PartRow, PartDto>> ToDto = r => new PartDto(
        r.Part.Id, r.Part.Sku, r.Part.Name, r.Part.Description, r.Part.Unit, r.Part.UnitCost, r.Part.UnitPrice, r.Part.ReorderLevel,
        r.Part.IsActive, r.Total, r.Total < r.Part.ReorderLevel);
}

internal sealed class PartRow
{
    public Part Part { get; init; } = null!;
    public decimal Total { get; init; }
}
