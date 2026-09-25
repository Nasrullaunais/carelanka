using CareLanka.Api.Common.Auth;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Staff;

[ApiController]
[Route("api/me/leave-requests")]
[Tags("My Roster")]
[Authorize(Policy = Policies.AnyStaff)]
public sealed class MyLeaveRequestsController : ControllerBase
{
    private readonly ILeaveRequestService _leaveRequestService;

    public MyLeaveRequestsController(ILeaveRequestService leaveRequestService)
    {
        _leaveRequestService = leaveRequestService;
    }

    [HttpGet(Name = "getMyLeaveRequests")]
    [ProducesResponseType(typeof(IReadOnlyList<LeaveRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<LeaveRequestDto>>> GetMyLeaveRequests(
        [FromQuery] LeaveStatus? status,
        CancellationToken cancellationToken = default)
    {
        var result = await _leaveRequestService.GetMyLeaveRequestsAsync(status, cancellationToken);
        return Ok(result);
    }

    [HttpPost(Name = "createMyLeaveRequest")]
    [ProducesResponseType(typeof(LeaveRequestDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<LeaveRequestDetailDto>> CreateMyLeaveRequest(
        [FromBody] CreateLeaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _leaveRequestService.CreateLeaveRequestAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpDelete("{id:guid}", Name = "withdrawMyLeaveRequest")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> WithdrawMyLeaveRequest(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        await _leaveRequestService.WithdrawLeaveRequestAsync(id, cancellationToken);
        return NoContent();
    }
}
