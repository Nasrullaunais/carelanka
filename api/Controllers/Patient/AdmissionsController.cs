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

    public AdmissionsController(IAdmissionService admissions) => _admissions = admissions;

    /// <summary>
    /// List admissions. With no `status` the answer is the live worklist, not the archive.
    /// `search` matches the patient's name or NIC.
    /// </summary>
    [Authorize(Policy = Policies.AdmissionReader)]
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
    [Authorize(Policy = Policies.AdmissionReader)]
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
    [Authorize(Policy = Policies.AdmissionReader)]
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
}
