using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Emergency;

public sealed record PreAdmissionRequest(
    Guid DispatchId,
    Guid? CallerUserId,
    bool PatientIsCaller,
    Guid? PatientId,
    DateTimeOffset ExpectedArrival,
    AdmissionUrgency Urgency);

public enum PreAdmissionOutcome
{
    Created,
    AlreadyExists,
    Rejected,
    Unavailable
}

public interface IPreAdmissionGateway
{
    Task<PreAdmissionOutcome> SendAsync(PreAdmissionRequest request, CancellationToken cancellationToken = default);
}
