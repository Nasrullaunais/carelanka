using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.Services.Patient;

public interface IBillingRateService
{
    Task<BillingRateBook> GetBookAsync(CancellationToken cancellationToken = default);

    Task<PriceList> GetPriceListAsync(CancellationToken cancellationToken = default);

    Task<BillingRateBook> UpdateAsync(
        UpdateBillingRatesRequest request, CancellationToken cancellationToken = default);
}
