using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Staff;

[ApiController]
[Route("api/coverage/wards")]
[Tags("Integration")]
[Authorize(Policy = Policies.AnyStaff)]
public sealed class WardCoverageController : ControllerBase
{
    private readonly IWardCoverageService _wardCoverageService;

    public WardCoverageController(IWardCoverageService wardCoverageService)
    {
        _wardCoverageService = wardCoverageService;
    }

    [HttpGet(Name = "getWardCoverage")]
    [ProducesResponseType(typeof(WardCoverageOverviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<WardCoverageOverviewResponse>> GetWardCoverage(
        [FromQuery] DateTimeOffset? at,
        CancellationToken cancellationToken = default)
    {
        var result = await _wardCoverageService.GetWardCoverageAsync(at, cancellationToken);
        return Ok(result);
    }
}
