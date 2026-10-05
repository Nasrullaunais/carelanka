using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Emergency;
using CareLanka.Api.Services.Emergency;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Emergency;

[ApiController]
[Route("api/reports/emergency")]
[Tags("Reports")]
[Authorize(Policy = Policies.DutyManager)]
public sealed class EmergencyReportsController(IEmergencyReportService reports) : ControllerBase
{
    [HttpGet("response-times", Name = "getEmergencyResponseTimeReport")]
    [ProducesResponseType(typeof(ResponseTimeReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<ResponseTimeReport>> ResponseTimes(
        [FromQuery] ResponseTimeReportRequest request, CancellationToken ct)
        => Ok(await reports.GetResponseTimesAsync(request.From!.Value, request.To!.Value, request.Priority, ct));

    [HttpGet("fleet-utilisation", Name = "getFleetUtilisationReport")]
    [ProducesResponseType(typeof(FleetUtilisationReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<FleetUtilisationReport>> FleetUtilisation(
        [FromQuery] EmergencyReportRequest request, CancellationToken ct)
        => Ok(await reports.GetFleetUtilisationAsync(request.From!.Value, request.To!.Value, ct));

    [HttpGet("agent-performance", Name = "getEmergencyAgentPerformanceReport")]
    [ProducesResponseType(typeof(EmergencyAgentPerformanceReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<EmergencyAgentPerformanceReport>> AgentPerformance(
        [FromQuery] EmergencyReportRequest request, CancellationToken ct)
        => Ok(await reports.GetAgentPerformanceAsync(request.From!.Value, request.To!.Value, ct));
}
