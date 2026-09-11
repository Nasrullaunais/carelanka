using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Patient;
using Microsoft.EntityFrameworkCore;
using AdmissionFeeRateEntity = CareLanka.Api.Data.Entities.Patient.AdmissionFeeRate;
using BillingRateEntity = CareLanka.Api.Data.Entities.Patient.BillingRate;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// The price grid the hospital administrator edits, and the one place a price is read from.
/// </summary>
/// <remarks>
/// <b>A missing row is filled in from the built-in defaults rather than treated as an error.</b>
/// That is what makes this safe to ship on a database that has never seen the settings screen:
/// the grid is complete from the first request, whether or not anybody has saved anything.
///
/// <b>Editing a rate never touches a bill already raised.</b> The price is copied onto the line
/// when the line is written, so today's change prices tomorrow's bills and leaves every piece of
/// paper a patient has already been handed exactly as it was.
/// </remarks>
public sealed class BillingRateService : IBillingRateService
{
    private readonly CareLankaDbContext _db;

    public BillingRateService(CareLankaDbContext db)
    {
        _db = db;
    }

    public async Task<BillingRateBook> GetBookAsync(CancellationToken ct = default)
    {
        var (rates, fees) = await ReadAsync(ct);

        return ToBook(rates, fees);
    }

    public async Task<PriceList> GetPriceListAsync(CancellationToken ct = default)
    {
        var (rates, fees) = await ReadAsync(ct);

        return new PriceList(fees, rates);
    }

    public async Task<BillingRateBook> UpdateAsync(
        UpdateBillingRatesRequest request, CancellationToken ct = default)
    {
        if (request.Expenses.Count == 0 && request.AdmissionFees.Count == 0)
        {
            throw new BadRequestException(MessageCode.ValidationFailed);
        }

        // An unknown expense key is refused rather than stored. Stored, it would sit in the
        // grid forever priced against nothing, and the screen would show a row no bill can use.
        var unknown = request.Expenses
            .Select(update => update.ExpenseKey.Trim())
            .FirstOrDefault(key => !BillingRateDefaults.ExpenseKeys.Contains(key));

        if (unknown is not null)
        {
            throw new BadRequestException(MessageCode.ValidationFailed);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var existingRates = await _db.BillingRates.ToListAsync(ct);
        var existingFees = await _db.AdmissionFeeRates.ToListAsync(ct);

        foreach (var update in request.Expenses)
        {
            var wardType = update.WardType!.Value;
            var key = update.ExpenseKey.Trim();
            var amount = update.Amount!.Value;

            var row = existingRates.FirstOrDefault(
                rate => rate.WardType == wardType && rate.ExpenseKey == key);

            if (row is null)
            {
                _db.BillingRates.Add(new BillingRateEntity
                {
                    Id = Guid.NewGuid(),
                    WardType = wardType,
                    ExpenseKey = key,
                    Amount = amount
                });
            }
            else
            {
                row.Amount = amount;
            }
        }

        foreach (var update in request.AdmissionFees)
        {
            var category = update.Category!.Value;
            var amount = update.Amount!.Value;

            var row = existingFees.FirstOrDefault(fee => fee.Category == category);

            if (row is null)
            {
                _db.AdmissionFeeRates.Add(new AdmissionFeeRateEntity
                {
                    Id = Guid.NewGuid(),
                    Category = category,
                    Amount = amount
                });
            }
            else
            {
                row.Amount = amount;
            }
        }

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await GetBookAsync(ct);
    }

    /// <summary>
    /// Reads both tables and fills every gap from the defaults, so the caller always gets a
    /// complete grid.
    /// </summary>
    private async Task<(
        IReadOnlyDictionary<(WardType, string), decimal> Rates,
        IReadOnlyDictionary<AdmissionCategory, decimal> Fees)> ReadAsync(CancellationToken ct)
    {
        var saved = await _db.BillingRates.AsNoTracking().ToListAsync(ct);
        var savedFees = await _db.AdmissionFeeRates.AsNoTracking().ToListAsync(ct);

        var rates = BillingRateDefaults.Grid()
            .ToDictionary(cell => (cell.WardType, cell.ExpenseKey), cell => cell.Amount);

        foreach (var row in saved)
        {
            rates[(row.WardType, row.ExpenseKey)] = row.Amount;
        }

        var fees = BillingRateDefaults.Fees()
            .ToDictionary(fee => fee.Category, fee => fee.Amount);

        foreach (var row in savedFees)
        {
            fees[row.Category] = row.Amount;
        }

        return (rates, fees);
    }

    private static BillingRateBook ToBook(
        IReadOnlyDictionary<(WardType, string), decimal> rates,
        IReadOnlyDictionary<AdmissionCategory, decimal> fees)
        => new()
        {
            Currency = BillingRates.Currency,

            Wards = Enum.GetValues<WardType>()
                .Select(wardType => new WardRates
                {
                    WardType = wardType,
                    Expenses = BillingRateDefaults.ExpenseKeys
                        .Select(key => new ExpenseRate
                        {
                            ExpenseKey = key,
                            Amount = rates[(wardType, key)]
                        })
                        .ToList()
                })
                .ToList(),

            AdmissionFees = Enum.GetValues<AdmissionCategory>()
                .Select(category => new AdmissionFee
                {
                    Category = category,
                    Amount = fees[category]
                })
                .ToList()
        };
}
