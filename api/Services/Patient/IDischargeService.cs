using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using DischargeResponse = CareLanka.Api.DTOs.Patient.Discharge;

namespace CareLanka.Api.Services.Patient;

/// <summary>Step 7: the checklist that has to be finished before anybody goes home.</summary>
public interface IDischargeService
{
    /// <summary>
    /// Patients who could go home, and the ones who nearly could. A plain rule over the
    /// checklist rows, not an agent.
    /// </summary>
    Task<PagedResult<DischargeCandidate>> ListCandidatesAsync(
        Guid? wardId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Tick or untick boxes. Each key is gated on the caller's role.</summary>
    Task<DischargeResponse> UpdateChecklistAsync(
        Guid admissionId, ChecklistUpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// End the visit: stamp <c>discharged_at</c>, give the bed back, move to <c>discharged</c>.
    /// One transaction, and the second high-impact human gate in this component.
    /// </summary>
    Task<DischargeResponse> ConfirmAsync(
        Guid admissionId, ConfirmDischargeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// The discharge record for this admission, or null when nobody has touched the checklist
    /// yet. <c>Find</c>, not <c>Get</c>: on an admission detail a missing one is the ordinary
    /// state, not a 404.
    /// </summary>
    Task<DischargeResponse?> FindForAdmissionAsync(
        Guid admissionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the <c>billing_settled</c> box. Called by <c>BillingService</c> and by nothing
    /// else - settling the bill is the only thing that ticks it.
    /// </summary>
    /// <remarks>
    /// **Does not save.** It joins the caller's transaction, so the money and the tick land
    /// together or neither lands. Re-evaluates the flag, so settling the last outstanding item
    /// moves the admission to <c>ready_for_discharge</c> exactly as ticking any other box does.
    /// </remarks>
    Task MarkBillingSettledAsync(
        Guid admissionId, bool settled, CancellationToken cancellationToken = default);
}
