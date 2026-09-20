namespace CareLanka.Api.Services.Equipment;

// The hospital's calendar day, not UTC's: midnight UTC is 5:30am in Sri Lanka, so "today" by UTC
// is wrong for the first five and a half hours of every morning.
public static class HospitalTime
{
    // The Windows name is the fallback for a Windows host without ICU time zone data.
    public static readonly TimeZoneInfo Zone =
        TimeZoneInfo.TryFindSystemTimeZoneById("Asia/Colombo", out var colombo)
            ? colombo
            : TimeZoneInfo.FindSystemTimeZoneById("Sri Lanka Standard Time");

    public static DateOnly Today(DateTimeOffset now)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Zone).DateTime);
}
