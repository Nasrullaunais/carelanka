using CareLanka.Api.DTOs.Common;

namespace CareLanka.Api.Services.Common;

public interface IHealthService
{
    Task<HealthStatus> GetHealthAsync(CancellationToken cancellationToken = default);
}
