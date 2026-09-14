using CareLanka.Api.Common.Auth;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Emergency;
using CareLanka.Api.Services.Emergency;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Emergency;

[ApiController]
[Route("api/ambulances")]
[Tags("Ambulances")]
public sealed class AmbulancesController : ControllerBase
{
    private readonly IAmbulanceService _ambulances;

    public AmbulancesController(IAmbulanceService ambulances) => _ambulances = ambulances;

    [Authorize(Policy = Policies.EmergencyResponder)]
    [HttpGet(Name = "listAmbulances")]
    [ProducesResponseType(typeof(PagedResult<AmbulanceSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<AmbulanceSummary>>> ListAmbulances(
        [FromQuery] AmbulanceListRequest request,
        CancellationToken cancellationToken = default)
        => Ok(await _ambulances.ListAsync(request, cancellationToken));

    [Authorize(Policy = Policies.EmergencyResponder)]
    [HttpGet("{id:guid}", Name = "getAmbulance")]
    [ProducesResponseType(typeof(AmbulanceDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<AmbulanceDetail>> GetAmbulance(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await _ambulances.GetByIdAsync(id, cancellationToken));

    [Authorize(Policy = Policies.DutyManager)]
    [HttpPost(Name = "createAmbulance")]
    [ProducesResponseType(typeof(Ambulance), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<Ambulance>> CreateAmbulance(
        [FromBody] CreateAmbulanceRequest request,
        CancellationToken cancellationToken)
        => Created((string?)null, await _ambulances.CreateAsync(request, cancellationToken));

    [Authorize(Policy = Policies.EmergencyResponder)]
    [HttpPatch("{id:guid}", Name = "updateAmbulance")]
    [ProducesResponseType(typeof(Ambulance), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<Ambulance>> UpdateAmbulance(
        Guid id,
        [FromBody] UpdateAmbulanceRequest request,
        CancellationToken cancellationToken)
        => Ok(await _ambulances.UpdateAsync(id, request, cancellationToken));

    [Authorize(Policy = Policies.DutyManager)]
    [HttpPost("{id:guid}/retire", Name = "retireAmbulance")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> RetireAmbulance(
        Guid id,
        [FromBody] RetireAmbulanceRequest request,
        CancellationToken cancellationToken)
    {
        await _ambulances.RetireAsync(id, request, cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = Policies.DutyManager)]
    [HttpPost("{id:guid}/reinstate", Name = "reinstateAmbulance")]
    [ProducesResponseType(typeof(Ambulance), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<Ambulance>> ReinstateAmbulance(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await _ambulances.ReinstateAsync(id, cancellationToken));
}
