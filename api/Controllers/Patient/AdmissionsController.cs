using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Common;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AdmissionResponse = CareLanka.Api.DTOs.Patient.Admission;

namespace CareLanka.Api.Controllers.Patient;

[ApiController]
[Route("api/admissions")]
[Tags("Admissions")]
public class AdmissionsController : ControllerBase
{
    private readonly IAdmissionService _admissions;
    private readonly IBedAssignmentService _beds;
    private readonly ICurrentUser _currentUser;

    public AdmissionsController(
        IAdmissionService admissions, IBedAssignmentService beds, ICurrentUser currentUser)
    {
        _admissions = admissions;
        _beds = beds;
        _currentUser = currentUser;
    }

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

    [Authorize(Policy = Policies.PatientDetails)]
    [HttpGet("{id:guid}", Name = "getAdmission")]
    [ProducesResponseType(typeof(AdmissionDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<AdmissionDetail>> GetAdmission(Guid id, CancellationToken ct)
        => Ok(await _admissions.GetDetailAsync(id, ct));

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

    [Authorize(Policy = Policies.PatientRegistrar)]
    [HttpPost("pre-admit", Name = "preAdmitFromDispatch")]
    [ProducesResponseType(typeof(AdmissionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AdmissionResponse>> PreAdmit(
        [FromBody] PreAdmitRequest request, CancellationToken ct)
    {
        var admission = await _admissions.PreAdmitAsync(request, ct);

        return CreatedAtRoute("getAdmission", new { id = admission.Id }, admission);
    }

    [Authorize(Policy = Policies.AdmissionEditor)]
    [HttpPost("{id:guid}/classify", Name = "classifyAdmission")]
    [ProducesResponseType(typeof(AdmissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AdmissionResponse>> ClassifyAdmission(
        Guid id, [FromBody] ClassifyAdmissionRequest request, CancellationToken ct)
        => Ok(await _admissions.ClassifyAsync(id, request, _currentUser.Id, ct));

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

    [Authorize(Policy = Policies.ArrivalConfirmer)]
    [HttpPost("{id:guid}/arrive", Name = "markArrived")]
    [ProducesResponseType(typeof(AdmissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AdmissionResponse>> MarkArrived(Guid id, CancellationToken ct)
        => Ok(await _admissions.MarkArrivedAsync(id, ct));

    [Authorize(Policy = Policies.AdmissionEditor)]
    [HttpPost("{id:guid}/complete", Name = "completeVisit")]
    [ProducesResponseType(typeof(AdmissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AdmissionResponse>> CompleteVisit(Guid id, CancellationToken ct)
        => Ok(await _admissions.CompleteAsync(id, ct));

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
