using System.Globalization;
using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Invoicing;
using Microsoft.EntityFrameworkCore;
using FieldOps.Application.Common;

namespace FieldOps.Application.Features.Reports;

public enum RevenueGrouping { Month, Customer }

/// <param name="Key">`2026-10` for a month, the customer id for a customer.</param>
/// <param name="Outstanding">Issued and not yet paid.</param>
public sealed record RevenueRow(
    string Key, string Label, int Invoices, decimal Subtotal, decimal Vat, decimal Total, decimal Paid, decimal Outstanding);

public sealed record RevenueReport(DateOnly From, DateOnly To, RevenueGrouping GroupBy, IReadOnlyList<RevenueRow> Rows, RevenueRow Totals)
{
    public CsvFile ToCsv(ReportPeriod period) => new(period.FileName($"revenue-by-{GroupBy.ToString().ToLowerInvariant()}"), Csv.Write(
        [GroupBy == RevenueGrouping.Month ? "Month" : "Customer", "Invoices", "Subtotal", "VAT", "Total", "Paid", "Outstanding"],
        Rows.Append(Totals).Select(r => (IReadOnlyList<object?>)[r.Label, r.Invoices, r.Subtotal, r.Vat, r.Total, r.Paid, r.Outstanding])));
}

public sealed record RevenueReportQuery(DateOnly? From = null, DateOnly? To = null, RevenueGrouping GroupBy = RevenueGrouping.Month);

/// <summary>US-RPT-02: issued and paid invoices by issue date; drafts and voided invoices are not revenue.</summary>
public sealed class RevenueReportHandler(IAppDbContext db, TimeProvider clock)
    : IQueryHandler<RevenueReportQuery, Result<(ReportPeriod Period, RevenueReport Report)>>
{
    public async Task<Result<(ReportPeriod Period, RevenueReport Report)>> Handle(RevenueReportQuery query, CancellationToken ct)
    {
        var resolved = ReportPeriod.Resolve(query.From, query.To, clock);
        if (resolved.IsFailure) return resolved.Error;
        var period = resolved.Value;

        var invoices = await (
                from i in db.Invoices.AsNoTracking()
                join c in db.Customers on i.CustomerId equals c.Id
                where (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Paid)
                    && i.IssueDate >= period.From && i.IssueDate <= period.To
                select new { i.CustomerId, CustomerName = c.Name, IssueDate = i.IssueDate!.Value, i.Subtotal, i.VatAmount, i.Total, i.Status })
            .ToListAsync(ct);

        var rows = query.GroupBy == RevenueGrouping.Month
            ? invoices
                .GroupBy(i => new DateOnly(i.IssueDate.Year, i.IssueDate.Month, 1))
                .OrderBy(g => g.Key)
                .Select(g => Row(g.Key.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                    g.Key.ToString("MMMM yyyy", CultureInfo.InvariantCulture), g.Select(i => (i.Subtotal, i.VatAmount, i.Total, i.Status))))
                .ToList()
            : invoices
                .GroupBy(i => (i.CustomerId, i.CustomerName))
                .Select(g => Row(g.Key.CustomerId.ToString(), g.Key.CustomerName, g.Select(i => (i.Subtotal, i.VatAmount, i.Total, i.Status))))
                .OrderByDescending(r => r.Total)
                .ThenBy(r => r.Label)
                .ToList();

        var totals = Row("total", "Total", invoices.Select(i => (i.Subtotal, i.VatAmount, i.Total, i.Status)));
        return (period, new RevenueReport(period.From, period.To, query.GroupBy, rows, totals));
    }

    private static RevenueRow Row(string key, string label, IEnumerable<(decimal Subtotal, decimal Vat, decimal Total, InvoiceStatus Status)> invoices)
    {
        var list = invoices.ToList();
        var total = list.Sum(i => i.Total);
        var paid = list.Where(i => i.Status == InvoiceStatus.Paid).Sum(i => i.Total);
        return new RevenueRow(key, label, list.Count, list.Sum(i => i.Subtotal), list.Sum(i => i.Vat), total, paid, total - paid);
    }
}
