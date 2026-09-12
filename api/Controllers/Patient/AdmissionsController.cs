using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AdmissionResponse = CareLanka.Api.DTOs.Patient.Admission;

namespace CareLanka.Api.Controllers.Patient;

/// <summary>Hospital visits. One row per stay, created before the patient arrives when an ambulance is bringing them.</summary>
[ApiController]
[Route("api/admissions")]
[Tags("Admissions")]
public class AdmissionsController : ControllerBase
{
    private readonly IAdmissionService _admissions;
    private readonly IBedAssignmentService _beds;

    public AdmissionsController(IAdmissionService admissions, IBedAssignmentService beds)
    {
        _admissions = admissions;
        _beds = beds;
    }

    /// <summary>
    /// List admissions. With no `status` the answer is the live worklist, not the archive.
    /// `search` matches the patient's code, name or NIC.
    /// </summary>
    [Authorize(Policy = Policies.PatientDetails)]
    [HttpGet(Name = "listAdmissions")]
    [ProducesResponseType(typeof(PagedResult<AdmissionSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<AdmissionSummary>>> ListAdmissions(
        [FromQuery(Name = "status")] AdmissionStatus[]? status,
        [FromQuery] AdmissionCategory? category,
        [FromQuery] AdmissionSource? source,
        [FromQuery] bool? detailsComplete,
        [FromQuery] string? search,
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20,
        [FromQuery] AdmissionSortField sortBy = AdmissionSortField.CreatedAt,
        [FromQuery] SortDirection sortDir = SortDirection.Desc,
        CancellationToken ct = default)
        => Ok(await _admissions.ListAsync(
            status, category, source, detailsComplete, search, page, pageSize, sortBy, sortDir, ct));

    /// <summary>
    /// Get one admission with its bed history. Nothing is deleted or overwritten, so rejected
    /// and expired assignments stay on the list — this is the audit trail.
    /// </summary>
    [Authorize(Policy = Policies.PatientDetails)]
    [HttpGet("{id:guid}", Name = "getAdmission")]
    [ProducesResponseType(typeof(AdmissionDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<AdmissionDetail>> GetAdmission(Guid id, CancellationToken ct)
        => Ok(await _admissions.GetDetailAsync(id, ct));

    /// <summary>
    /// Start an admission. Creates a visit in status `awaiting_bed`. The care level and the
    /// clinician who chose it are both required — that pair is the proof a human decided it.
    /// </summary>
    [Authorize(Policy = Policies.PatientRegistrar)]
    [HttpPost(Name = "createAdmission")]
    [ProducesResponseType(typeof(AdmissionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AdmissionResponse>> CreateAdmission(
        [FromBody] CreateAdmissionRequest request, CancellationToken ct)
    {
        var admission = await _admissions.CreateAsync(request, ct);

        return CreatedAtRoute("getAdmission", new { id = admission.Id }, admission);
    }

    /// <summary>
    /// Fill in details that were missing at registration. A field left out is left alone, and
    /// completeness is recalculated here rather than trusted from the caller.
    /// </summary>
    /// <remarks>
    /// Completeness is deliberately not part of `status`: a patient can be admitted and still
    /// have paperwork outstanding, and one field cannot express both without ambiguity.
    /// </remarks>
    [Authorize(Policy = Policies.AdmissionEditor)]
    [HttpPatch("{id:guid}/details", Name = "completeAdmissionDetails")]
    [ProducesResponseType(typeof(AdmissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AdmissionResponse>> CompleteAdmissionDetails(
        Guid id, [FromBody] CompleteDetailsRequest request, CancellationToken ct)
        => Ok(await _admissions.CompleteDetailsAsync(id, request, ct));

    /// <summary>
    /// Mark the patient as physically present in the bed. Moves `bed_reserved` to `admitted`,
    /// sets `admitted_at`, and turns the hold on the bed into an occupancy so it can no longer
    /// expire.
    /// </summary>
    /// <remarks>
    /// Rejected with 409 from any other status: a patient cannot arrive into a bed that was
    /// never approved. The ward nurse is at the bedside, which is why this is theirs and not
    /// the duty manager's.
    /// </remarks>
    [Authorize(Policy = Policies.WardNurse)]
    [HttpPost("{id:guid}/arrive", Name = "markArrived")]
    [ProducesResponseType(typeof(AdmissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AdmissionResponse>> MarkArrived(Guid id, CancellationToken ct)
        => Ok(await _admissions.MarkArrivedAsync(id, ct));

    /// <summary>
    /// Finish a visit that never needed a bed - the scan or test is done and the patient has
    /// gone home. Moves `admitted` to `discharged`.
    /// </summary>
    /// <remarks>
    /// **Only for a visit that needs no bed** - an `outpatient`. A visit at any other care
    /// level is refused with 409 and `cl_pat_020`: that is a discharge, and a discharge has a
    /// checklist, a summary note, an approver and a bed to give back. This endpoint does none
    /// of those, so it says so rather than half-doing it.
    ///
    /// The ward nurse or doctor who did the test is the one who knows it is finished, which is
    /// why this is theirs and not the duty manager's.
    /// </remarks>
    [Authorize(Policy = Policies.AdmissionEditor)]
    [HttpPost("{id:guid}/complete", Name = "completeVisit")]
    [ProducesResponseType(typeof(AdmissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AdmissionResponse>> CompleteVisit(Guid id, CancellationToken ct)
        => Ok(await _admissions.CompleteAsync(id, ct));

    /// <summary>
    /// Cancel an admission, with a reason, and release any bed it was holding.
    /// </summary>
    /// <remarks>
    /// Always a human act, never automatic, which is why the reason is mandatory and why this
    /// is the duty manager's. A hold expiring frees a bed by itself because that is cheap and
    /// reversible; declaring that a patient is not coming is neither. Refused with 409 once
    /// they are `admitted` - discharge them instead.
    /// </remarks>
    [Authorize(Policy = Policies.DutyManager)]
    [HttpPost("{id:guid}/cancel", Name = "cancelAdmission")]
    [ProducesResponseType(typeof(AdmissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AdmissionResponse>> CancelAdmission(
        Guid id, [FromBody] CancelAdmissionRequest request, CancellationToken ct)
        => Ok(await _admissions.CancelAsync(id, request, ct));

    /// <summary>
    /// Assign a bed by hand, bypassing the agent. Places a 30-minute hold and moves the
    /// admission to `bed_reserved`.
    /// </summary>
    /// <remarks>
    /// **The manual path must always work.** If the only way to admit a patient were through
    /// the AI, the hospital would stop the moment the AI stopped - and it is what makes a human
    /// genuinely in control rather than only able to say yes or no.
    ///
    /// The same hard rules and the same row-locked re-check as the agent's own path. A human
    /// may overrule the agent's ranking; nobody may put an ICU patient in a general bed without
    /// it being recorded as a downgrade.
    ///
    /// Reception and a ward nurse may assign a bed that matches the patient's care level. Every
    /// bed off that path - intensive care, high dependency, a step down, or a step up into a
    /// ward more acute than the patient needs - is the duty manager's, and that depends on which
    /// bed was chosen rather than on the route, so choosing one is a 403 from the service and
    /// not a 401 from a policy.
    /// </remarks>
    [Authorize(Policy = Policies.BedAssigner)]
    [HttpPost("{id:guid}/assign-bed", Name = "assignBedManually")]
    [ProducesResponseType(typeof(BedAssignment), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<BedAssignment>> AssignBedManually(
        Guid id, [FromBody] AssignBedRequest request, CancellationToken ct)
        => Ok(await _beds.AssignManuallyAsync(id, request, ct));

    /// <summary>Move a patient into a different bed because the first one was a mistake.</summary>
    /// <remarks>
    /// Beds get mis-clicked. Without this the only ways out are cancelling a live visit or
    /// leaving a patient recorded in a bed somebody else is standing next to, and a ward board
    /// that is known to be wrong stops being read at all.
    ///
    /// <b>A correction, not a transfer.</b> The bed it replaces is written off and charged for
    /// nothing, which is right for a mis-click and wrong for a patient who genuinely spent a
    /// night in one ward and moved to another.
    ///
    /// The same permission and the same hard rules as assigning a bed in the first place: a
    /// ward nurse correcting a bed still cannot correct it into intensive care.
    /// </remarks>
    [Authorize(Policy = Policies.BedAssigner)]
    [HttpPost("{id:guid}/correct-bed", Name = "correctBed")]
    [ProducesResponseType(typeof(BedAssignment), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<BedAssignment>> CorrectBed(
        Guid id, [FromBody] CorrectBedRequest request, CancellationToken ct)
        => Ok(await _beds.CorrectBedAsync(id, request, ct));
}
