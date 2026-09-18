using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Patient;

[ApiController]
[Route("api/bed-workflows")]
[Tags("Bed Assignment")]
public class BedWorkflowsController : ControllerBase
{
    private readonly IBedSuggestionService _suggestions;

    public BedWorkflowsController(IBedSuggestionService suggestions)
        => _suggestions = suggestions;

    /// <summary>
    /// The agent's execution summary: plan, steps, tool calls with timings, validation results,
    /// errors, status - and the answer itself.
    /// </summary>
    /// <remarks>
    /// <c>rationale</c> is a short summary written for the screen. The model's raw internal
    /// reasoning is not stored anywhere.
    /// </remarks>
    [Authorize(Policy = Policies.BedAssigner)]
    [HttpGet("{workflowId:guid}", Name = "getBedWorkflow")]
    [ProducesResponseType(typeof(BedWorkflowSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<BedWorkflowSummary>> GetBedWorkflow(
        Guid workflowId, CancellationToken ct)
        => Ok(await _suggestions.GetAsync(workflowId, ct));
}
