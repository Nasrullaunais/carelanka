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
}
