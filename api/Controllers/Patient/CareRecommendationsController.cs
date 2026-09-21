using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Common;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CareRecommendationResponse = CareLanka.Api.DTOs.Patient.CareRecommendation;

namespace CareLanka.Api.Controllers.Patient;

[ApiController]
[Route("api")]
[Tags("Care Recommendations")]
public class CareRecommendationsController : ControllerBase
{
    private readonly ICareRecommendationService _recommendations;
    private readonly ICurrentUser _currentUser;

    public CareRecommendationsController(
        ICareRecommendationService recommendations, ICurrentUser currentUser)
    {
        _recommendations = recommendations;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Same shape and purpose as /bed-workflows/{workflowId}: plan, completed steps, the
    /// red-flag screen result, validation, status and outcome.
    /// </summary>
    [Authorize(Policy = Policies.CareQueueReader)]
    [HttpGet("care-workflows/{workflowId:guid}", Name = "getCareWorkflow")]
    [ProducesResponseType(typeof(CareWorkflowSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<CareWorkflowSummary>> GetCareWorkflow(
        Guid workflowId, CancellationToken ct)
        => Ok(await _recommendations.GetWorkflowAsync(workflowId, ct));

    /// <summary>
    /// The care draft review queue. Widened to WardNurse on 2026-09-16 - the person who will
    /// walk over and look at an admitted patient is the nurse on shift.
    /// </summary>
    [Authorize(Policy = Policies.CareQueueReader)]
    [HttpGet("care-recommendations", Name = "listCareRecommendations")]
    [ProducesResponseType(typeof(PagedResult<CareRecommendationSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<CareRecommendationSummary>>> ListCareRecommendations(
        [FromQuery] CareRecommendationStatus? status,
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20,
        [FromQuery] SortDirection sortDir = SortDirection.Desc,
        CancellationToken ct = default)
        => Ok(await _recommendations.ListAsync(status, page, pageSize, sortDir, ct));

    /// <summary>
    /// The patient's own text, the agent's draft, red_flag / urgency_flag, and enough of an id
    /// trail (patient_id, admission_id, workflow_id) that a reviewer can pull the medical profile
    /// and the workflow run behind this draft rather than taking it on trust.
    /// </summary>
    [Authorize(Policy = Policies.CareQueueReader)]
    [HttpGet("care-recommendations/{id:guid}", Name = "getCareRecommendation")]
    [ProducesResponseType(typeof(CareRecommendationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<CareRecommendationResponse>> GetCareRecommendation(
        Guid id, CancellationToken ct)
        => Ok(await _recommendations.GetAsync(id, ct));

    /// <summary>
    /// The high-impact human gate. Optionally edit the message before it becomes visible to the
    /// patient - nothing reaches them until this call succeeds.
    /// </summary>
    [Authorize(Policy = Policies.CareRecommendationReviewer)]
    [HttpPost("care-recommendations/{id:guid}/approve", Name = "approveCareRecommendation")]
    [ProducesResponseType(typeof(CareRecommendationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<CareRecommendationResponse>> ApproveCareRecommendation(
        Guid id, [FromBody] ApproveCareRecommendationRequest? request, CancellationToken ct)
        => Ok(await _recommendations.ApproveAsync(id, request, _currentUser.Id, ct));

    /// <summary>
    /// Requires a reason. Staff-facing only - the patient's own view shows a generic
    /// "reviewed, a nurse will follow up" note instead.
    /// </summary>
    [Authorize(Policy = Policies.CareRecommendationReviewer)]
    [HttpPost("care-recommendations/{id:guid}/reject", Name = "rejectCareRecommendation")]
    [ProducesResponseType(typeof(CareRecommendationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<CareRecommendationResponse>> RejectCareRecommendation(
        Guid id, [FromBody] RejectCareRecommendationRequest request, CancellationToken ct)
        => Ok(await _recommendations.RejectAsync(id, request, _currentUser.Id, ct));
}
