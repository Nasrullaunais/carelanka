using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Patient;

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

    public static PriceList Defaults { get; } = new(
        BillingRateDefaults.Fees().ToDictionary(fee => fee.Category, fee => fee.Amount),
        BillingRateDefaults.Grid().ToDictionary(
            cell => (cell.WardType, cell.ExpenseKey), cell => cell.Amount));

    public decimal AdmissionFee(AdmissionCategory category)
        => _fees.TryGetValue(category, out var amount)
            ? amount
            : BillingRates.AdmissionFee(category);

    public decimal BedDay(WardType? wardType)
        => Expense(wardType ?? WardType.General, BillingRateDefaults.BedDayKey);

    public decimal Expense(WardType wardType, string expenseKey)
        => _rates.TryGetValue((wardType, expenseKey), out var amount)
            ? amount
            : BillingRateDefaults.Amount(wardType, expenseKey);
}
