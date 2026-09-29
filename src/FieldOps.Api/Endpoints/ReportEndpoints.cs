using FieldOps.Api.Common;
using FieldOps.Application.Features.Dashboard;
using FieldOps.Application.Features.Reports;
using FieldOps.Domain.Common;

namespace FieldOps.Api.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dashboard/today", async (GetDashboardHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(new GetDashboardQuery(), ct)))
            .WithTags("Dashboard")
            .RequireAuthorization(Policies.OfficeStaff);

        var group = app.MapGroup("/api/reports").WithTags("Reports").RequireAuthorization(Policies.AdminOnly);

        group.MapGet("/technicians", async (DateOnly? from, DateOnly? to, string? format, TechnicianReportHandler handler,
                CancellationToken ct) =>
            Respond(format, await handler.Handle(new TechnicianReportQuery(from, to), ct), r => r.Report.ToCsv(r.Period)));

        group.MapGet("/revenue", async (DateOnly? from, DateOnly? to, string? groupBy, string? format, RevenueReportHandler handler,
            CancellationToken ct) =>
        {
            if (!Enum.TryParse<RevenueGrouping>(groupBy ?? nameof(RevenueGrouping.Month), ignoreCase: true, out var grouping)
                || !Enum.IsDefined(grouping))
                return Invalid("groupBy", "Group by month or customer.");
            return Respond(format, await handler.Handle(new RevenueReportQuery(from, to, grouping), ct), r => r.Report.ToCsv(r.Period));
        });

        group.MapGet("/parts-usage", async (DateOnly? from, DateOnly? to, string? format, PartsUsageReportHandler handler,
                CancellationToken ct) =>
            Respond(format, await handler.Handle(new PartsUsageReportQuery(from, to), ct), r => r.Report.ToCsv(r.Period)));

        return app;
    }

    /// <summary>JSON by default; `format=csv` downloads the same report (docs/05-api.md).</summary>
    private static IResult Respond<TReport>(string? format, Result<(ReportPeriod Period, TReport Report)> result,
        Func<(ReportPeriod Period, TReport Report), CsvFile> toCsv)
    {
        var csv = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase);
        if (!csv && format is not null && !string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
            return Invalid("format", "Use json or csv.");
        if (result.IsFailure) return result.Error.ToProblem();
        if (!csv) return Results.Ok(result.Value.Report);
        var file = toCsv(result.Value);
        return Results.File(file.Content, "text/csv; charset=utf-8", file.FileName);
    }

    private static IResult Invalid(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
