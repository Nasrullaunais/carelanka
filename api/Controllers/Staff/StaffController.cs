using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Staff;

[ApiController]
[Route("api/staff")]
[Tags("Staff")]
public sealed class StaffController : ControllerBase
{
    private readonly IStaffLookupService _staffLookup;

    public StaffController(IStaffLookupService staffLookup)
    {
        _staffLookup = staffLookup;
    }

    [Authorize(Policy = Policies.AnyStaff)]
    [HttpPost("lookup", Name = "lookupStaff")]
    [Tags("Integration")]
    [ProducesResponseType(typeof(IReadOnlyList<StaffLookupResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<StaffLookupResult>>> LookupStaff(
        [FromBody] LookupStaffRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request?.StaffIds == null)
        {
            return BadRequest();
        }

        var results = await _staffLookup.LookupAsync(request.StaffIds, cancellationToken);
        return Ok(results);
    }

    [Authorize(Policy = Policies.DutyManager)]
    [HttpGet("crew-candidates", Name = "searchAvailableCrew")]
    [ProducesResponseType(typeof(IReadOnlyList<CrewCandidate>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<CrewCandidate>>> SearchAvailableCrew(
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
        => Ok(await _staffLookup.SearchAvailableCrewAsync(search, cancellationToken));
}
