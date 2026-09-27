namespace FieldOps.Application.Common;

/// <summary>The company works in Dubai time, so "today" for dates such as warranty expiry is the Dubai date.</summary>
public static class BusinessCalendar
{
    public static readonly TimeZoneInfo TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Dubai");

    public static DateOnly Today(this TimeProvider clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZone).DateTime);
}
