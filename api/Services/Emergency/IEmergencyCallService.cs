using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Emergency;

namespace CareLanka.Api.Services.Emergency;

public interface IEmergencyCallService
{
    Task<EmergencyCallDetail> CreateAsync(
        CreateEmergencyCallRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<EmergencyCallSummary>> ListAsync(
        EmergencyCallListRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<MyEmergencyCallSummary>> ListMineAsync(
        MyEmergencyCallListRequest request,
        CancellationToken cancellationToken = default);

    Task<EmergencyCallDetail> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<EmergencyCallDetail> UpdateAsync(
        Guid id,
        UpdateEmergencyCallRequest request,
        CancellationToken cancellationToken = default);

    Task<MyCallTracking> TrackMineAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MyEmergencyCallSummary> CancelMineAsync(Guid id, RequestCancellationRequest request, CancellationToken cancellationToken = default);
    Task<EmergencyCancellationRequest> RequestCancellationAsync(Guid id, RequestCancellationRequest request, CancellationToken cancellationToken = default);
}
