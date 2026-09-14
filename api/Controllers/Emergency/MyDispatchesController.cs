using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Emergency;
using CareLanka.Api.Services.Emergency;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Emergency;

[ApiController]
[Route("api/me/dispatches")]
[Tags("My Run")]
[Authorize(Policy = Policies.AmbulanceCrew)]
public sealed class MyDispatchesController(IDispatchService dispatches) : ControllerBase
{
    [HttpGet("active", Name = "getMyActiveDispatch")]
    [ProducesResponseType(typeof(DispatchDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<DispatchDetail>> Active(CancellationToken ct) => Ok(await dispatches.GetMyActiveAsync(ct));

    [HttpPost("{id:guid}/acknowledge", Name = "acknowledgeMyDispatch")]
    [ProducesResponseType(typeof(DispatchDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DispatchDetail>> Acknowledge(Guid id, CancellationToken ct) => Ok(await dispatches.AcknowledgeAsync(id, ct));

    [HttpPost("{id:guid}/decline", Name = "declineMyDispatch")]
    [ProducesResponseType(typeof(DispatchDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DispatchDetail>> Decline(Guid id, DeclineDispatchRequest request, CancellationToken ct) => Ok(await dispatches.DeclineAsync(id, request, ct));

    [HttpPost("{id:guid}/status", Name = "updateMyDispatchStatus")]
    [ProducesResponseType(typeof(DispatchDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DispatchDetail>> Progress(Guid id, UpdateMyDispatchStatusRequest request, CancellationToken ct) => Ok(await dispatches.ProgressAsync(id, request, ct));

    [HttpPost("{id:guid}/handover", Name = "recordHandover")]
    [ProducesResponseType(typeof(DispatchDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DispatchDetail>> Handover(Guid id, CancellationToken ct) => Ok(await dispatches.HandoverAsync(id, ct));
}
