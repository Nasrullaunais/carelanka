using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Patient;

[ApiController]
[Route("api")]
[Tags("Bed Assignment")]
public class BedSuggestionsController : ControllerBase
{
    private readonly IBedSuggestionService _suggestions;

    public BedSuggestionsController(IBedSuggestionService suggestions)
        => _suggestions = suggestions;

    /// <summary>
    /// Ask who this patient is and where they go, from a row on the patients board or from an NIC
    /// or patient code off the hospital slip. The run holds no bed and changes nothing; committing
    /// a suggestion is the ordinary assign-bed endpoint, with the workflow id on the body.
    /// </summary>
    [Authorize(Policy = Policies.BedAssigner)]
    [HttpPost("bed-suggestions", Name = "requestBedSuggestion")]
    [ProducesResponseType(typeof(BedWorkflowAccepted), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<BedWorkflowAccepted>> RequestBedSuggestion(
        [FromBody] BedSuggestionRequest request, CancellationToken ct)
    {
        var accepted = await _suggestions.StartAsync(request, ct);

        return Accepted(accepted.PollUrl, accepted);
    }

    /// <summary>
    /// The run itself: plan, steps with timings, tool calls, validation, errors and retries, plus
    /// the answer. The model's raw reasoning is not stored - only the sentence shown on screen.
    /// </summary>
    [Authorize(Policy = Policies.BedAssigner)]
    [HttpGet("bed-workflows/{workflowId:guid}", Name = "getBedWorkflow")]
    [ProducesResponseType(typeof(BedWorkflowSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<BedWorkflowSummary>> GetBedWorkflow(
        Guid workflowId, CancellationToken ct)
        => Ok(await _suggestions.GetAsync(workflowId, ct));
}
