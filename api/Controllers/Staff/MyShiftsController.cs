using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Staff;

[ApiController]
[Route("api/me/shifts")]
[Tags("My Roster")]
[Authorize(Policy = Policies.AnyStaff)]
public sealed class MyShiftsController : ControllerBase
{
    private readonly IMyRosterService _myRosterService;

    public MyShiftsController(IMyRosterService myRosterService)
    {
        _myRosterService = myRosterService;
    }

    [HttpGet(Name = "getMyShifts")]
    [ProducesResponseType(typeof(IReadOnlyList<MyShiftDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<MyShiftDto>>> GetMyShifts(
        [FromQuery] GetMyShiftsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var result = await _myRosterService.GetMyShiftsAsync(parameters, cancellationToken);
        return Ok(result);
    }
}
