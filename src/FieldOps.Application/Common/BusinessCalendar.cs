namespace FieldOps.Application.Common;

/// <summary>The company works in Dubai time, so "today" for dates such as warranty expiry is the Dubai date.</summary>
public static class BusinessCalendar
{
    public static readonly TimeZoneInfo TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Dubai");

    public static DateOnly Today(this TimeProvider clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZone).DateTime);

    /// <summary>The UTC instants where the given Dubai calendar day starts (inclusive) and ends (exclusive).</summary>
    public static (DateTimeOffset From, DateTimeOffset To) DayRange(DateOnly day)
    {
        var start = day.ToDateTime(TimeOnly.MinValue);
        var from = new DateTimeOffset(start, TimeZone.GetUtcOffset(start)).ToUniversalTime();
        var nextStart = start.AddDays(1);
        var to = new DateTimeOffset(nextStart, TimeZone.GetUtcOffset(nextStart)).ToUniversalTime();
        return (from, to);
    }
}
