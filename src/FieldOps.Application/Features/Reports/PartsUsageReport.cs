using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Common;
using FieldOps.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Reports;

/// <param name="Cost">At the part's current unit cost: the cost at the time of use is not recorded.</param>
/// <param name="Price">At the price charged when the part was used.</param>
/// <param name="MarginPercent">Margin as a share of price; null when nothing was charged.</param>
public sealed record PartsUsageRow(
    Guid PartId, string Sku, string Name, PartUnit Unit, decimal Quantity, int Jobs, decimal Cost, decimal Price, decimal Margin,
    decimal? MarginPercent);

public sealed record PartsUsageReport(
    DateOnly From, DateOnly To, IReadOnlyList<PartsUsageRow> Rows, decimal Cost, decimal Price, decimal Margin, decimal? MarginPercent)
{
    public CsvFile ToCsv(ReportPeriod period) => new(period.FileName("parts-usage"), Csv.Write(
        ["SKU", "Part", "Unit", "Quantity", "Jobs", "Cost", "Price", "Margin", "Margin %"],
        [
            .. Rows.Select(r => (IReadOnlyList<object?>)[r.Sku, r.Name, r.Unit, r.Quantity, r.Jobs, r.Cost, r.Price, r.Margin, r.MarginPercent]),
            ["Total", null, null, null, null, Cost, Price, Margin, MarginPercent],
        ]));
}

public sealed record PartsUsageReportQuery(DateOnly? From = null, DateOnly? To = null);

/// <summary>US-RPT-03: parts used on jobs in the period (by when they were taken from the van); returned parts drop out.</summary>
public sealed class PartsUsageReportHandler(IAppDbContext db, TimeProvider clock)
    : IQueryHandler<PartsUsageReportQuery, Result<(ReportPeriod Period, PartsUsageReport Report)>>
{
    public async Task<Result<(ReportPeriod Period, PartsUsageReport Report)>> Handle(PartsUsageReportQuery query, CancellationToken ct)
    {
        var resolved = ReportPeriod.Resolve(query.From, query.To, clock);
        if (resolved.IsFailure) return resolved.Error;
        var period = resolved.Value;
        var (start, end) = period.Instants();

        var used = await (
                from u in db.WorkOrderParts.AsNoTracking()
                join m in db.StockMovements on u.StockMovementId equals m.Id
                join p in db.Parts on u.PartId equals p.Id
                where m.CreatedAt >= start && m.CreatedAt < end
                select new { p.Id, p.Sku, p.Name, p.Unit, p.UnitCost, u.WorkOrderId, u.Quantity, u.UnitPrice })
            .ToListAsync(ct);

        var rows = used
            .GroupBy(u => (u.Id, u.Sku, u.Name, u.Unit, u.UnitCost))
            .Select(g =>
            {
                var quantity = g.Sum(u => u.Quantity);
                var cost = Money(quantity * g.Key.UnitCost);
                var price = Money(g.Sum(u => u.Quantity * u.UnitPrice));
                return new PartsUsageRow(g.Key.Id, g.Key.Sku, g.Key.Name, g.Key.Unit, quantity, g.Select(u => u.WorkOrderId).Distinct().Count(),
                    cost, price, price - cost, Percent(price - cost, price));
            })
            .OrderByDescending(r => r.Price)
            .ThenBy(r => r.Sku)
            .ToList();

        var totalCost = rows.Sum(r => r.Cost);
        var totalPrice = rows.Sum(r => r.Price);
        return (period, new PartsUsageReport(period.From, period.To, rows, totalCost, totalPrice, totalPrice - totalCost,
            Percent(totalPrice - totalCost, totalPrice)));
    }

    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal? Percent(decimal margin, decimal price) =>
        price == 0 ? null : Math.Round(margin / price * 100, 1, MidpointRounding.AwayFromZero);
}
