using CareLanka.Api.DTOs.Emergency;

namespace CareLanka.Api.Services.Emergency;

public interface IFleetMapService
{
    Task<FleetMap> GetAsync(CancellationToken cancellationToken = default);
}
