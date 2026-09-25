using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Staff;

[ApiController]
[Route("api/leave-requests")]
[Tags("Leave")]
public sealed class LeaveRequestsController : ControllerBase
{
    private readonly ILeaveRequestService _leaveRequestService;

    public LeaveRequestsController(ILeaveRequestService leaveRequestService)
    {
        _leaveRequestService = leaveRequestService;
    }

    [Authorize(Roles = "hospital_administrator,duty_manager")]
    [HttpGet(Name = "listLeaveRequests")]
    [ProducesResponseType(typeof(PagedResult<LeaveRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<LeaveRequestDto>>> ListLeaveRequests(
        [FromQuery] ListLeaveRequestsQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var result = await _leaveRequestService.ListLeaveRequestsAsync(parameters, cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet("{id:guid}", Name = "getLeaveRequest")]
    [ProducesResponseType(typeof(LeaveRequestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<LeaveRequestDetailDto>> GetLeaveRequest(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _leaveRequestService.GetLeaveRequestDetailAsync(id, cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = "hospital_administrator,duty_manager")]
    [HttpPost("{id:guid}/decision", Name = "decideLeaveRequest")]
    [ProducesResponseType(typeof(DecideLeaveResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DecideLeaveResponse>> DecideLeaveRequest(
        [FromRoute] Guid id,
        [FromBody] DecideLeaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _leaveRequestService.DecideLeaveRequestAsync(id, request, cancellationToken);
        return Ok(result);
    }
}
