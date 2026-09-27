using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Application.Features.Technicians;
using FieldOps.Domain.Common;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Reports;

/// <param name="AverageMinutes">From the first start to completion, holds included; null without jobs.</param>
/// <param name="WorkHours">Logged Work time on those jobs, travel excluded.</param>
public sealed record TechnicianReportRow(
    Guid TechnicianId, string Name, string EmployeeCode, int CompletedJobs, int? AverageMinutes, decimal WorkHours);

public sealed record TechnicianReport(
    DateOnly From, DateOnly To, IReadOnlyList<TechnicianReportRow> Rows, int CompletedJobs, int? AverageMinutes, decimal WorkHours)
{
    public CsvFile ToCsv(ReportPeriod period) => new(period.FileName("technicians"), Csv.Write(
        ["Technician", "Employee code", "Completed jobs", "Average duration (min)", "Work hours"],
        [
            .. Rows.Select(r => (IReadOnlyList<object?>)[r.Name, r.EmployeeCode, r.CompletedJobs, r.AverageMinutes, r.WorkHours]),
            ["Total", null, CompletedJobs, AverageMinutes, WorkHours],
        ]));
}

public sealed record TechnicianReportQuery(DateOnly? From = null, DateOnly? To = null);

/// <summary>US-RPT-01: jobs completed per technician in the period (by completion time, Dubai days).</summary>
public sealed class TechnicianReportHandler(IAppDbContext db, TechnicianReader technicians, TimeProvider clock)
    : IQueryHandler<TechnicianReportQuery, Result<(ReportPeriod Period, TechnicianReport Report)>>
{
    public async Task<Result<(ReportPeriod Period, TechnicianReport Report)>> Handle(TechnicianReportQuery query, CancellationToken ct)
    {
        var resolved = ReportPeriod.Resolve(query.From, query.To, clock);
        if (resolved.IsFailure) return resolved.Error;
        var period = resolved.Value;
        var (start, end) = period.Instants();

        var jobs = await db.WorkOrders.AsNoTracking()
            .Where(w => w.CompletedAt >= start && w.CompletedAt < end && w.AssignedTechnicianId != null
                && w.Status != WorkOrderStatus.Cancelled)
            .Select(w => new { w.Id, TechnicianId = w.AssignedTechnicianId!.Value, w.StartedAt, CompletedAt = w.CompletedAt!.Value })
            .ToListAsync(ct);
        var jobIds = jobs.Select(j => j.Id).ToList();

        var workMinutes = await db.TimeEntries.AsNoTracking()
            .Where(t => jobIds.Contains(t.WorkOrderId) && t.Type == TimeEntryType.Work && t.DurationMinutes != null)
            .GroupBy(t => t.TechnicianId)
            .Select(g => new { TechnicianId = g.Key, Minutes = g.Sum(t => t.DurationMinutes!.Value) })
            .ToDictionaryAsync(x => x.TechnicianId, x => x.Minutes, ct);

        var byTechnician = jobs.ToLookup(j => j.TechnicianId);
        var withJobs = byTechnician.Select(g => g.Key).ToList();
        var people = await technicians.ReadAsync(db.Technicians.Where(t => t.IsActive || withJobs.Contains(t.Id)), ct);

        var rows = people
            .Select(t =>
            {
                var done = byTechnician[t.Id].ToList();
                return new TechnicianReportRow(t.Id, t.FullName, t.EmployeeCode, done.Count,
                    Average(done.Select(j => j.CompletedAt - (j.StartedAt ?? j.CompletedAt))), Hours(workMinutes.GetValueOrDefault(t.Id)));
            })
            .OrderByDescending(r => r.CompletedJobs)
            .ThenBy(r => r.Name)
            .ToList();

        var report = new TechnicianReport(period.From, period.To, rows, jobs.Count,
            Average(jobs.Select(j => j.CompletedAt - (j.StartedAt ?? j.CompletedAt))), Hours(workMinutes.Values.Sum()));
        return (period, report);
    }

    private static int? Average(IEnumerable<TimeSpan> durations)
    {
        var list = durations.ToList();
        return list.Count == 0 ? null : (int)Math.Round(list.Average(d => d.TotalMinutes), MidpointRounding.AwayFromZero);
    }

    private static decimal Hours(int minutes) => Math.Round(minutes / 60m, 2, MidpointRounding.AwayFromZero);
}
