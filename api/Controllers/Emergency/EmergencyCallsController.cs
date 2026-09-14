using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Emergency;
using CareLanka.Api.Services.Emergency;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Emergency;

[ApiController]
[Route("api/emergency-calls")]
[Tags("Calls")]
public sealed class EmergencyCallsController : ControllerBase
{
    private readonly IEmergencyCallService _calls;

    public EmergencyCallsController(IEmergencyCallService calls) => _calls = calls;

    [Authorize]
    [HttpPost(Name = "createEmergencyCall")]
    [ProducesResponseType(typeof(EmergencyCallDetail), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<EmergencyCallDetail>> Create(
        [FromBody] CreateEmergencyCallRequest request,
        CancellationToken cancellationToken)
        => Created((string?)null, await _calls.CreateAsync(request, cancellationToken));

    [Authorize(Policy = Policies.DutyManager)]
    [HttpGet(Name = "listEmergencyCalls")]
    [ProducesResponseType(typeof(PagedResult<EmergencyCallSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<EmergencyCallSummary>>> List(
        [FromQuery] EmergencyCallListRequest request,
        CancellationToken cancellationToken)
        => Ok(await _calls.ListAsync(request, cancellationToken));

    [Authorize]
    [HttpGet("{id:guid}", Name = "getEmergencyCall")]
    [ProducesResponseType(typeof(EmergencyCallDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<EmergencyCallDetail>> Get(
        Guid id,
        CancellationToken cancellationToken)
        => Ok(await _calls.GetByIdAsync(id, cancellationToken));

    [Authorize(Policy = Policies.DutyManager)]
    [HttpPatch("{id:guid}", Name = "updateEmergencyCall")]
    [ProducesResponseType(typeof(EmergencyCallDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<EmergencyCallDetail>> Update(
        Guid id,
        [FromBody] UpdateEmergencyCallRequest request,
        CancellationToken cancellationToken)
        => Ok(await _calls.UpdateAsync(id, request, cancellationToken));

    [Authorize(Policy = Policies.DutyManager)]
    [HttpPost("{id:guid}/dispatch", Name = "dispatchEmergencyCall")]
    [ProducesResponseType(typeof(DispatchDetail), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DispatchDetail>> Dispatch(
        Guid id, [FromBody] ManualDispatchRequest request, [FromServices] IDispatchService dispatches,
        CancellationToken cancellationToken)
        => Created((string?)null, await dispatches.DispatchAsync(id, request, cancellationToken));
}
