using CareLanka.Api.DTOs.Staff;

namespace CareLanka.Api.Services.Staff;

public interface IWardCoverageService
{
    Task<WardCoverageOverviewResponse> GetWardCoverageAsync(
        DateTimeOffset? at,
        CancellationToken cancellationToken = default);
}
