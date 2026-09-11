using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.Services.Patient;

public interface IBillingRateService
{
    /// <summary>The whole grid, for the settings screen and for anyone showing a suggested price.</summary>
    Task<BillingRateBook> GetBookAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The prices in force, read once so every line of one bill is priced from the same grid.
    /// </summary>
    Task<PriceList> GetPriceListAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves the cells that changed and returns the grid as it now stands.</summary>
    Task<BillingRateBook> UpdateAsync(
        UpdateBillingRatesRequest request, CancellationToken cancellationToken = default);
}
