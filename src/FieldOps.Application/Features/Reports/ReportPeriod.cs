using FieldOps.Application.Common;
using FieldOps.Domain.Common;

namespace FieldOps.Application.Features.Reports;

/// <summary>An inclusive range of Dubai dates; by default the month so far.</summary>
public sealed record ReportPeriod(DateOnly From, DateOnly To)
{
    public const int MaxDays = 366;

    public static Result<ReportPeriod> Resolve(DateOnly? from, DateOnly? to, TimeProvider clock)
    {
        var end = to ?? clock.Today();
        var start = from ?? new DateOnly(end.Year, end.Month, 1);
        if (start > end) return ReportErrors.FromAfterTo;
        if (end.DayNumber - start.DayNumber + 1 > MaxDays) return ReportErrors.RangeTooLong;
        return new ReportPeriod(start, end);
    }

    /// <summary>The UTC instants covering the period: from the start of <see cref="From"/> to the end of <see cref="To"/>.</summary>
    public (DateTimeOffset Start, DateTimeOffset End) Instants() =>
        (BusinessCalendar.DayRange(From).From, BusinessCalendar.DayRange(To).To);

    public string FileName(string report) => $"{report}-{From:yyyy-MM-dd}-to-{To:yyyy-MM-dd}.csv";
}

public static class ReportErrors
{
    public static readonly Error FromAfterTo =
        Error.Validation("Report.FromAfterTo", "The start date must be on or before the end date.");

    public static readonly Error RangeTooLong =
        Error.Validation("Report.RangeTooLong", $"A report covers at most {ReportPeriod.MaxDays} days.");
}

/// <summary>A report as a CSV download.</summary>
public sealed record CsvFile(string FileName, byte[] Content);
