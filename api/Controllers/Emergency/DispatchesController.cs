using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Emergency;
using CareLanka.Api.Services.Emergency;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Emergency;

[ApiController]
[Route("api/dispatches")]
[Tags("Dispatches")]
public sealed class DispatchesController(IDispatchService dispatches) : ControllerBase
{
    [Authorize(Policy = Policies.DutyManager)]
    [HttpPost("{id:guid}/cancel", Name = "cancelDispatch")]
    [ProducesResponseType(typeof(DispatchDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DispatchDetail>> Cancel(Guid id, CancelDispatchRequest request, CancellationToken ct)
        => Ok(await dispatches.CancelAsync(id, request, ct));

    [Authorize(Policy = Policies.DutyManager)]
    [HttpPost("{id:guid}/reassign", Name = "reassignDispatch")]
    [ProducesResponseType(typeof(DispatchDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DispatchDetail>> Reassign(Guid id, ReassignDispatchRequest request, CancellationToken ct)
        => Ok(await dispatches.ReassignAsync(id, request, ct));
}
