using CareLanka.Api.DTOs.Emergency;
using CareLanka.Api.DTOs.Common;

namespace CareLanka.Api.Services.Emergency;

public interface IAmbulanceService
{
    Task<Ambulance> GetMyAssignmentAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<AmbulanceSummary>> ListAsync(
        AmbulanceListRequest request,
        CancellationToken cancellationToken = default);

    Task<AmbulanceDetail> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Ambulance> CreateAsync(
        CreateAmbulanceRequest request,
        CancellationToken cancellationToken = default);

    Task<Ambulance> UpdateAsync(
        Guid id,
        UpdateAmbulanceRequest request,
        CancellationToken cancellationToken = default);

    Task RetireAsync(
        Guid id,
        RetireAmbulanceRequest request,
        CancellationToken cancellationToken = default);

    Task<Ambulance> ReinstateAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task ReportLocationAsync(Guid id, ReportAmbulanceLocationRequest request,
        CancellationToken cancellationToken = default);
}
