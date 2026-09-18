using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Patient;

[ApiController]
[Route("api/bed-suggestions")]
[Tags("Bed Assignment")]
public class BedSuggestionsController : ControllerBase
{
    private readonly IBedSuggestionService _suggestions;

    public BedSuggestionsController(IBedSuggestionService suggestions)
        => _suggestions = suggestions;

    /// <summary>
    /// Asks the Bed and Patient Details Agent who this is and where they go.
    /// </summary>
    /// <remarks>
    /// The agent writes nothing. This starts a workflow and returns its id; the answer arrives at
    /// <c>GET /api/bed-workflows/{workflowId}</c>. Committing a suggestion is
    /// <c>POST /api/admissions/{id}/assign-bed</c> with the workflow id - the same endpoint, row
    /// lock and hard rules a manual pick has always run through. There is no agent-only write path
    /// and no separate approve endpoint.
    /// </remarks>
    [Authorize(Policy = Policies.BedAssigner)]
    [HttpPost(Name = "requestBedSuggestion")]
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
}
