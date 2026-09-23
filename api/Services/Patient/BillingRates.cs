using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Patient;

public static class BillingRates
{
    public const string Currency = "LKR";

    public const decimal ConsultationFee = 1_500m;

    private static readonly IReadOnlyDictionary<AdmissionCategory, decimal> AdmissionFees =
        new Dictionary<AdmissionCategory, decimal>
        {
            [AdmissionCategory.Icu] = 7_500m,
            [AdmissionCategory.General] = 3_000m,
            [AdmissionCategory.Surgical] = 4_500m,
            [AdmissionCategory.Maternity] = 4_000m,
            [AdmissionCategory.Emergency] = 5_000m
        };

    private static readonly IReadOnlyDictionary<WardType, decimal> BedDayRates =
        new Dictionary<WardType, decimal>
        {
            [WardType.Icu] = 25_000m,
            [WardType.Hdu] = 15_000m,
            [WardType.Isolation] = 12_000m,
            [WardType.Surgical] = 12_000m,
            [WardType.Emergency] = 10_000m,
            [WardType.Maternity] = 9_000m,
            [WardType.Pediatric] = 8_000m,
            [WardType.MentalHealth] = 7_000m,
            [WardType.General] = 6_000m
        };

    public static decimal AdmissionFee(AdmissionCategory category)
        => AdmissionFees.TryGetValue(category, out var fee) ? fee : 0m;

    public static decimal BedDay(WardType? wardType)
        => wardType is { } type && BedDayRates.TryGetValue(type, out var rate)
            ? rate
            : BedDayRates[WardType.General];

    public static int BillableDays(DateTimeOffset from, DateTimeOffset to)
    {
        var hours = (to - from).TotalHours;

        return hours <= 24 ? 1 : (int)Math.Ceiling(hours / 24d);
    }
}
