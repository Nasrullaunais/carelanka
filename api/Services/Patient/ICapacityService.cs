using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.Services.Patient;

public interface ICapacityService
{
    Task<WardCapacitySummary> GetWardCapacityAsync(CancellationToken cancellationToken = default);

    Task<WardOccupancy> GetWardOccupancyAsync(Guid wardId, CancellationToken cancellationToken = default);
}
