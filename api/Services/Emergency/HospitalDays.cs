using CareLanka.Api.Services.Equipment;

namespace CareLanka.Api.Services.Emergency;

// Date filters and "today" follow the hospital's calendar (Sri Lanka, UTC+5:30), not UTC's.
public static class HospitalDays
{
    public static DateTimeOffset StartOf(DateOnly day)
    {
        var midnight = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(midnight, HospitalTime.Zone.GetUtcOffset(midnight)).ToUniversalTime();
    }

    public static DateTimeOffset EndOf(DateOnly day) => StartOf(day.AddDays(1));

    public static DateTimeOffset StartOfToday(DateTimeOffset now) => StartOf(HospitalTime.Today(now));
}
