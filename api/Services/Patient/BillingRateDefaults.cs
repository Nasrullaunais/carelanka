using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Patient;

public static class BillingRateDefaults
{
    public static readonly IReadOnlyList<string> ExpenseKeys = new[]
    {
        BedDayKey,
        "food",
        "medicine",
        "therapy",
        "tests",
        "transport",
        "take_home_medicine"
    };

    public const string BedDayKey = "bed_day";

    private static readonly IReadOnlyDictionary<string, decimal> ExpenseDefaults =
        new Dictionary<string, decimal>
        {
            ["food"] = 1_200m,
            ["medicine"] = 0m,
            ["therapy"] = 4_500m,
            ["tests"] = 3_500m,
            ["transport"] = 3_500m,
            ["take_home_medicine"] = 0m
        };

    public static decimal Amount(WardType wardType, string expenseKey)
        => expenseKey == BedDayKey
            ? BillingRates.BedDay(wardType)
            : ExpenseDefaults.TryGetValue(expenseKey, out var amount) ? amount : 0m;

    public static IEnumerable<(WardType WardType, string ExpenseKey, decimal Amount)> Grid()
        => from wardType in Enum.GetValues<WardType>()
           from expenseKey in ExpenseKeys
           select (wardType, expenseKey, Amount(wardType, expenseKey));

    public static IEnumerable<(AdmissionCategory Category, decimal Amount)> Fees()
        => Enum.GetValues<AdmissionCategory>()
            .Select(category => (category, BillingRates.AdmissionFee(category)));
}
