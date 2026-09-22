using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Emergency;
using CareLanka.Api.Services.Emergency;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Emergency;

[ApiController]
[Route("api")]
[Tags("Cancellation Review")]
[Authorize(Policy = Policies.DutyManager)]
public sealed class EmergencyCancellationRequestsController(IEmergencyCallService calls) : ControllerBase
{
    [HttpGet("emergency-cancellation-requests", Name = "listEmergencyCancellationRequests")]
    [ProducesResponseType(typeof(PagedResult<EmergencyCancellationRequest>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<EmergencyCancellationRequest>>> List(
        [FromQuery] CancellationRequestListRequest request, CancellationToken ct)
        => Ok(await calls.ListCancellationRequestsAsync(request, ct));

    [HttpPost("emergency-calls/{id:guid}/cancellation-request/approve", Name = "approveEmergencyCancellationRequest")]
    [ProducesResponseType(typeof(EmergencyCancellationRequest), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<EmergencyCancellationRequest>> Approve(Guid id, ReviewCancellationRequest request, CancellationToken ct)
        => Ok(await calls.ApproveCancellationRequestAsync(id, request, ct));

    [HttpPost("emergency-calls/{id:guid}/cancellation-request/reject", Name = "rejectEmergencyCancellationRequest")]
    [ProducesResponseType(typeof(EmergencyCancellationRequest), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<EmergencyCancellationRequest>> Reject(Guid id, ReviewCancellationRequest request, CancellationToken ct)
        => Ok(await calls.RejectCancellationRequestAsync(id, request, ct));
}
