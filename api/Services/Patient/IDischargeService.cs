using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using DischargeResponse = CareLanka.Api.DTOs.Patient.Discharge;

namespace CareLanka.Api.Services.Patient;

public interface IDischargeService
{
    Task<PagedResult<DischargeCandidate>> ListCandidatesAsync(
        Guid? wardId,
        bool includeDischarged,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<DischargeResponse> UpdateChecklistAsync(
        Guid admissionId, ChecklistUpdateRequest request, CancellationToken cancellationToken = default);

    Task<DischargeResponse> ConfirmAsync(
        Guid admissionId, ConfirmDischargeRequest request, CancellationToken cancellationToken = default);

    Task<DischargeResponse?> FindForAdmissionAsync(
        Guid admissionId, CancellationToken cancellationToken = default);

    Task MarkBillingSettledAsync(
        Guid admissionId, bool settled, CancellationToken cancellationToken = default);
}
