using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Emergency;
using CareLanka.Api.Services.Emergency;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CareLanka.Api.Controllers.Emergency;

[ApiController]
[Route("api/ambulances")]
[Tags("Ambulances")]
public sealed class AmbulanceCrewController : ControllerBase
{
    private readonly IAmbulanceCrewService _crew;

    public AmbulanceCrewController(IAmbulanceCrewService crew) => _crew = crew;

    [Authorize(Policy = Policies.EmergencyResponder)]
    [HttpGet("{id:guid}/crew", Name = "getCurrentAmbulanceCrew")]
    [ProducesResponseType(typeof(IReadOnlyList<AmbulanceCrewAssignment>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<AmbulanceCrewAssignment>>> ListCurrent(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await _crew.ListCurrentAsync(id, cancellationToken));

    [Authorize(Policy = Policies.DutyManager)]
    [HttpPost("{id:guid}/crew", Name = "assignCurrentAmbulanceCrew")]
    [ProducesResponseType(typeof(AmbulanceCrewAssignment), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AmbulanceCrewAssignment>> Assign(
        Guid id,
        [FromBody, BindRequired] AssignAmbulanceCrewRequest request,
        CancellationToken cancellationToken)
        => Created((string?)null, await _crew.AssignAsync(id, request, cancellationToken));

    [Authorize(Policy = Policies.DutyManager)]
    [HttpDelete("{ambulanceId:guid}/crew/{staffMemberId:guid}", Name = "unassignCurrentAmbulanceCrew")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Unassign(
        Guid ambulanceId,
        Guid staffMemberId,
        CancellationToken cancellationToken)
    {
        await _crew.UnassignAsync(ambulanceId, staffMemberId, cancellationToken);
        return NoContent();
    }
}
