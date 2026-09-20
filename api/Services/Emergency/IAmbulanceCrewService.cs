using CrewAssignment = CareLanka.Api.DTOs.Emergency.AmbulanceCrewAssignment;
using CareLanka.Api.DTOs.Emergency;

namespace CareLanka.Api.Services.Emergency;

public interface IAmbulanceCrewService
{
    Task<IReadOnlyList<CrewAssignment>> ListCurrentAsync(
        Guid ambulanceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CrewAssignment>> ListCurrentForFleetAsync(
        Guid ambulanceId,
        CancellationToken cancellationToken = default);

    Task<CrewAssignment> AssignAsync(
        Guid ambulanceId,
        AssignAmbulanceCrewRequest request,
        CancellationToken cancellationToken = default);

    Task UnassignAsync(
        Guid ambulanceId,
        Guid staffMemberId,
        CancellationToken cancellationToken = default);
}
