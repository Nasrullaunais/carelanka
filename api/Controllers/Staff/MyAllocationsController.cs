using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Staff;

[ApiController]
[Route("api/me/allocations")]
[Tags("My Roster")]
[Authorize(Policy = Policies.AnyStaff)]
public sealed class MyAllocationsController : ControllerBase
{
    private readonly IMyRosterService _myRosterService;

    public MyAllocationsController(IMyRosterService myRosterService)
    {
        _myRosterService = myRosterService;
    }

    [HttpPost("{id:guid}/clock-in", Name = "clockIn")]
    [ProducesResponseType(typeof(AllocationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AllocationDto>> ClockIn(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _myRosterService.ClockInAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/clock-out", Name = "clockOut")]
    [ProducesResponseType(typeof(AllocationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AllocationDto>> ClockOut(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _myRosterService.ClockOutAsync(id, cancellationToken);
        return Ok(result);
    }
}
