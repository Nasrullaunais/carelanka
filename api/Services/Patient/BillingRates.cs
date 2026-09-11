using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// What the hospital charges, in Sri Lankan rupees. Two tables and nothing else: a one-off fee
/// for opening the visit, and a price per day for a bed.
/// </summary>
/// <remarks>
/// **These numbers are invented.** No real price list was given to us, and saying so here is
/// better than a table that looks authoritative and is not.
///
/// A static table rather than a <c>billing_rates</c> table on purpose. Nothing in the project
/// changes a price, and a table would be a migration, a role, a screen and a set of tests for a
/// number that is edited once a year in a real hospital and never here.
///
/// The rate is **copied onto the line** when the line is written - see
/// <see cref="Data.Entities.Patient.BillLineItem"/>. Change a number in this file and every
/// bill already raised stays exactly as the patient was charged; only bills prepared afterwards
/// use the new figure. That is the whole reason the price is a column and not a lookup.
/// </remarks>
public static class BillingRates
{
    /// <summary>Fixed. This component does not convert currency and does not pretend to.</summary>
    public const string Currency = "LKR";

    /// <summary>
    /// The one-off charge for opening a visit, by care level. Priced off
    /// <c>Admission.Category</c>, which is a fact a clinician recorded and signed for.
    /// </summary>
    private static readonly IReadOnlyDictionary<AdmissionCategory, decimal> AdmissionFees =
        new Dictionary<AdmissionCategory, decimal>
        {
            [AdmissionCategory.Icu] = 7_500m,
            [AdmissionCategory.Hdu] = 5_000m,
            [AdmissionCategory.Inpatient] = 3_000m,
            [AdmissionCategory.DayCase] = 2_500m,
            [AdmissionCategory.Outpatient] = 1_500m
        };

    /// <summary>
    /// A bed for one day, by the type of ward it stands in. The ward is what sets the price -
    /// an intensive care bed costs what it does because of the staffing around it, not because
    /// of the frame.
    /// </summary>
    private static readonly IReadOnlyDictionary<WardType, decimal> BedDayRates =
        new Dictionary<WardType, decimal>
        {
            [WardType.Icu] = 25_000m,
            [WardType.Hdu] = 15_000m,
            [WardType.Isolation] = 12_000m,
            [WardType.Maternity] = 9_000m,
            [WardType.Pediatric] = 8_000m,
            [WardType.General] = 6_000m
        };

    public static decimal AdmissionFee(AdmissionCategory category)
        => AdmissionFees.TryGetValue(category, out var fee) ? fee : 0m;

    /// <summary>
    /// The day rate for a ward type, or the general-ward rate when the ward behind a past stay
    /// has been retired and its type can no longer be read. Cheapest of the six, so a gap in
    /// our own data never overcharges a patient.
    /// </summary>
    public static decimal BedDay(WardType? wardType)
        => wardType is { } type && BedDayRates.TryGetValue(type, out var rate)
            ? rate
            : BedDayRates[WardType.General];

    /// <summary>
    /// How many days a stay is charged as: part of a day counts as a day, and every stay counts
    /// as at least one.
    /// </summary>
    /// <remarks>
    /// So a three-hour day case pays for one day, and a stay of twenty-five hours pays for two.
    /// Rounding up is what a hospital does and it is the only rule here anybody could dispute,
    /// which is why it is one method with its name on it rather than an expression inside a
    /// loop.
    /// </remarks>
    public static int BillableDays(DateTimeOffset from, DateTimeOffset to)
    {
        var hours = (to - from).TotalHours;

        return hours <= 24 ? 1 : (int)Math.Ceiling(hours / 24d);
    }
}
