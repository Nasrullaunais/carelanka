using CareLanka.Api.DTOs.Emergency;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;

namespace CareLanka.Api.Services.Emergency;

public interface IAmbulanceService
{
    Task<PagedResult<AmbulanceSummary>> ListAsync(
        AmbulanceStatus? status,
        string? search,
        decimal? nearToLatitude,
        decimal? nearToLongitude,
        bool includeRetired,
        int page,
        int pageSize,
        AmbulanceSortField sortBy,
        string sortDir,
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
}
