using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.Services.Patient;

public interface IBedOccupancyService
{
    Task<BedOccupancyStatus> GetStatusAsync(Guid bedId, CancellationToken cancellationToken = default);
}
