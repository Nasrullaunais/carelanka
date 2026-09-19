using CareLanka.Api.DTOs.Emergency;

namespace CareLanka.Api.Services.Emergency;

public interface IDispatchService
{
    Task<DispatchDetail> GetMyActiveAsync(CancellationToken cancellationToken = default);
    Task<DispatchDetail> DispatchAsync(Guid callId, ManualDispatchRequest request, CancellationToken cancellationToken = default);
    Task<DispatchDetail> AcknowledgeAsync(Guid dispatchId, CancellationToken cancellationToken = default);
    Task<DispatchDetail> DeclineAsync(Guid dispatchId, DeclineDispatchRequest request, CancellationToken cancellationToken = default);
    Task<DispatchDetail> ProgressAsync(Guid dispatchId, UpdateMyDispatchStatusRequest request, CancellationToken cancellationToken = default);
    Task<DispatchDetail> HandoverAsync(Guid dispatchId, RecordHandoverRequest request, CancellationToken cancellationToken = default);
    Task<DispatchDetail> CancelAsync(Guid dispatchId, CancelDispatchRequest request, CancellationToken cancellationToken = default);
    Task CancelForApprovedCancellationRequestAsync(Guid emergencyCallId, CancellationToken cancellationToken = default);
    Task<DispatchDetail> ReassignAsync(Guid dispatchId, ReassignDispatchRequest request, CancellationToken cancellationToken = default);
}
