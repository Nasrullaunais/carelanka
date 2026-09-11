using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// The prices in force right now, read once and then asked many times while one bill is built.
/// </summary>
/// <remarks>
/// A snapshot, on purpose. Preparing a bill writes several lines, and every one of them must be
/// priced from the same grid — an administrator saving a new rate halfway through would
/// otherwise put two different prices on one piece of paper.
///
/// <b>A missing cell falls back to the built-in default rather than failing.</b> The rates are
/// editable now, so a row can be deleted, and a pricing screen that can leave a patient's bill
/// unpriceable is worse than one nobody can edit.
/// </remarks>
public sealed class PriceList
{
    private readonly IReadOnlyDictionary<AdmissionCategory, decimal> _fees;
    private readonly IReadOnlyDictionary<(WardType WardType, string ExpenseKey), decimal> _rates;

    public PriceList(
        IReadOnlyDictionary<AdmissionCategory, decimal> fees,
        IReadOnlyDictionary<(WardType, string), decimal> rates)
    {
        _fees = fees;
        _rates = rates;
    }

    /// <summary>The built-in grid, for tests and for a database with no rates rows at all.</summary>
    public static PriceList Defaults { get; } = new(
        BillingRateDefaults.Fees().ToDictionary(fee => fee.Category, fee => fee.Amount),
        BillingRateDefaults.Grid().ToDictionary(
            cell => (cell.WardType, cell.ExpenseKey), cell => cell.Amount));

    public decimal AdmissionFee(AdmissionCategory category)
        => _fees.TryGetValue(category, out var amount)
            ? amount
            : BillingRates.AdmissionFee(category);

    /// <summary>
    /// A bed for one day in this kind of ward, or the general-ward price when the ward behind a
    /// past stay has been retired and its type can no longer be read. Cheapest of the lot, so a
    /// gap in our own data never overcharges a patient.
    /// </summary>
    public decimal BedDay(WardType? wardType)
        => Expense(wardType ?? WardType.General, BillingRateDefaults.BedDayKey);

    public decimal Expense(WardType wardType, string expenseKey)
        => _rates.TryGetValue((wardType, expenseKey), out var amount)
            ? amount
            : BillingRateDefaults.Amount(wardType, expenseKey);
}
