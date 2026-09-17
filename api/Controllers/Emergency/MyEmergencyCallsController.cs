using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Emergency;
using CareLanka.Api.Services.Emergency;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Emergency;

[ApiController]
[Route("api/me/emergency-calls")]
[Tags("My Calls")]
[Authorize(Policy = Policies.PatientOnly)]
public sealed class MyEmergencyCallsController : ControllerBase
{
    private readonly IEmergencyCallService _calls;

    public MyEmergencyCallsController(IEmergencyCallService calls) => _calls = calls;

    [HttpGet(Name = "getMyEmergencyCalls")]
    [ProducesResponseType(typeof(PagedResult<MyEmergencyCallSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<PagedResult<MyEmergencyCallSummary>>> List(
        [FromQuery] MyEmergencyCallListRequest request,
        CancellationToken cancellationToken)
        => Ok(await _calls.ListMineAsync(request, cancellationToken));

    [HttpGet("{id:guid}/tracking", Name = "trackMyEmergencyCall")]
    [ProducesResponseType(typeof(MyCallTracking), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<MyCallTracking>> Track(Guid id, CancellationToken ct) => Ok(await _calls.TrackMineAsync(id, ct));

    [HttpPost("{id:guid}/cancel", Name = "cancelMyEmergencyCall")]
    [ProducesResponseType(typeof(MyEmergencyCallSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<MyEmergencyCallSummary>> Cancel(Guid id, RequestCancellationRequest request, CancellationToken ct) => Ok(await _calls.CancelMineAsync(id, request, ct));

    [HttpPost("{id:guid}/cancellation-request", Name = "requestMyEmergencyCallCancellation")]
    [ProducesResponseType(typeof(EmergencyCancellationRequest), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<EmergencyCancellationRequest>> RequestCancellation(Guid id, RequestCancellationRequest request, CancellationToken ct)
        => Created((string?)null, await _calls.RequestCancellationAsync(id, request, ct));
}
