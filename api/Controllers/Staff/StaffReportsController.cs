using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Staff;

[ApiController]
[Route("api/reports")]
[Tags("Reports")]
public sealed class StaffReportsController : ControllerBase
{
    private readonly IStaffReportsService _reportsService;

    public StaffReportsController(IStaffReportsService reportsService)
    {
        _reportsService = reportsService;
    }

    [Authorize(Roles = "hospital_administrator,duty_manager")]
    [HttpGet("coverage", Name = "getCoverageReport")]
    [ProducesResponseType(typeof(CoverageReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<CoverageReport>> GetCoverageReport(
        [FromQuery] CoverageReportParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var result = await _reportsService.GetCoverageReportAsync(parameters, cancellationToken);
        return Ok(result);
    }
}
