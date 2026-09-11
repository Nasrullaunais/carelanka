using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// The price grid a fresh database starts with, and the fallback when a cell has no row.
/// </summary>
/// <remarks>
/// <b>These numbers are invented.</b> No real price list was given to us. They fill the grid so
/// the administrator is editing something rather than staring at 72 empty boxes, and every one
/// of them is editable.
///
/// <b>Every ward starts at the same price for everything except the bed.</b> That is deliberate
/// and it is the honest default: a bed in intensive care genuinely costs more because of the
/// staffing around it, but we have no reason to claim a meal does. The grid exists so the
/// administrator <i>can</i> price them apart, not so we can pretend to know that they differ.
/// </remarks>
public static class BillingRateDefaults
{
    /// <summary>
    /// Every expense the grid prices, in the order the settings screen shows them.
    /// </summary>
    /// <remarks>
    /// <c>bed_day</c> is first because it is the one the system works out by itself; the rest
    /// are typed by reception and these are the suggestions in their boxes. The keys match
    /// <c>web-ui/src/types/billing.ts</c> — a key that does not match is a price nobody sees.
    ///
    /// <c>other</c> is deliberately absent. It has no price by definition: it is the row for
    /// whatever did not fit, and a suggested price for "something else" is a made-up number
    /// pointing at nothing.
    /// </remarks>
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

    /// <summary>The bed itself, priced per day. The one expense the system fills in alone.</summary>
    public const string BedDayKey = "bed_day";

    /// <summary>
    /// What each expense costs by default, in every ward. <c>0</c> means "no suggestion" —
    /// medicine is priced off a pharmacy slip that is different every time, so the desk types
    /// the figure and an invented one would only be in the way.
    /// </summary>
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

    /// <summary>The default price of one expense in one ward.</summary>
    public static decimal Amount(WardType wardType, string expenseKey)
        => expenseKey == BedDayKey
            ? BillingRates.BedDay(wardType)
            : ExpenseDefaults.TryGetValue(expenseKey, out var amount) ? amount : 0m;

    /// <summary>Every cell of the grid: one entry per ward type, per expense.</summary>
    public static IEnumerable<(WardType WardType, string ExpenseKey, decimal Amount)> Grid()
        => from wardType in Enum.GetValues<WardType>()
           from expenseKey in ExpenseKeys
           select (wardType, expenseKey, Amount(wardType, expenseKey));

    /// <summary>Every admission fee: one per care level.</summary>
    public static IEnumerable<(AdmissionCategory Category, decimal Amount)> Fees()
        => Enum.GetValues<AdmissionCategory>()
            .Select(category => (category, BillingRates.AdmissionFee(category)));
}
