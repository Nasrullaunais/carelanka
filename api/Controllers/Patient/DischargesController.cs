using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DischargeResponse = CareLanka.Api.DTOs.Patient.Discharge;

namespace CareLanka.Api.Controllers.Patient;

/// <summary>Sending a patient home: the checklist that has to be finished first, and the sign-off.</summary>
[ApiController]
[Route("api/discharges")]
[Tags("Discharge")]
public class DischargesController : ControllerBase
{
    private readonly IDischargeService _discharges;

    public DischargesController(IDischargeService discharges)
    {
        _discharges = discharges;
    }

    /// <summary>
    /// Patients whose checklist says they could go home, and the ones with a box or two left.
    /// A plain rule over the checklist rows, not an agent — checking whether three boxes are
    /// ticked is a `WHERE` clause.
    /// </summary>
    /// <remarks>
    /// The list is advisory. Being on it changes nothing until a human confirms.
    ///
    /// `includeDischarged` adds the visits that are already over, as records. They sort below
    /// everyone still in the building, carry `is_discharged` and `discharged_at`, and are never
    /// candidates for anything — without them this screen forgets every patient the moment the
    /// work on them is finished.
    /// </remarks>
    [Authorize(Policy = Policies.DischargeBoard)]
    [HttpGet("candidates", Name = "listDischargeCandidates")]
    [ProducesResponseType(typeof(PagedResult<DischargeCandidate>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<DischargeCandidate>>> ListDischargeCandidates(
        [FromQuery] Guid? wardId,
        [FromQuery] bool includeDischarged = false,
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _discharges.ListCandidatesAsync(wardId, includeDischarged, page, pageSize, ct));

    /// <summary>
    /// Tick discharge checklist items. Any subset; a key left out is not touched.
    /// </summary>
    /// <remarks>
    /// Each item is gated by role, not just by login:
    ///
    /// - `clinical_clearance` — **Doctor only.** This is the wall. Without it nothing flags and
    ///   nothing discharges, and no automated process can ever set it.
    /// - `medication_issued`, `follow_up_recorded`, `transport_arranged` — Ward Nurse.
    /// - `billing_settled` — **refused here.** Settle the bill instead
    ///   (`POST /api/admissions/{id}/bill/settle`), which is what writes it. One fact, one
    ///   place: a paid bill and an unticked box cannot happen.
    ///
    /// Ticking the last mandatory box moves the admission to `ready_for_discharge`; unticking
    /// one moves it back to `admitted`.
    /// </remarks>
    [Authorize(Policy = Policies.DischargeChecklist)]
    [HttpPatch("{admissionId:guid}/checklist", Name = "updateDischargeChecklist")]
    [ProducesResponseType(typeof(DischargeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DischargeResponse>> UpdateDischargeChecklist(
        Guid admissionId, [FromBody] ChecklistUpdateRequest request, CancellationToken ct)
        => Ok(await _discharges.UpdateChecklistAsync(admissionId, request, ct));

    /// <summary>
    /// Confirm the discharge — the second high-impact human gate in this component.
    /// </summary>
    /// <remarks>
    /// It ends the admission, frees the bed for the next patient, and sends someone home. In one
    /// transaction it sets `discharged_at`, releases the bed assignment with
    /// `release_reason: discharged`, and moves the admission to `discharged`.
    ///
    /// Ward Nurse for `outpatient`, `day_case` and `inpatient`; **Duty Manager for `icu` and
    /// `hdu`**, which depends on the admission rather than the route and so is checked in the
    /// service. Refused with 409 if any mandatory checklist item is unticked.
    /// </remarks>
    [Authorize(Policy = Policies.AdmissionEditor)]
    [HttpPost("{admissionId:guid}/confirm", Name = "confirmDischarge")]
    [ProducesResponseType(typeof(DischargeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DischargeResponse>> ConfirmDischarge(
        Guid admissionId, [FromBody] ConfirmDischargeRequest? request, CancellationToken ct)
        => Ok(await _discharges.ConfirmAsync(admissionId, request ?? new ConfirmDischargeRequest(), ct));
}
