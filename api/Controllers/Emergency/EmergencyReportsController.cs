using CareLanka.Api.Common.Auth;
using CareLanka.Api.Data.Enums;
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
    public async Task<ActionResult<ResponseTimeReport>> ResponseTimes(DateOnly from, DateOnly to,
        CallPriority? priority, CancellationToken ct) => Ok(await reports.GetResponseTimesAsync(from, to, priority, ct));

    [HttpGet("fleet-utilisation", Name = "getFleetUtilisationReport")]
    [ProducesResponseType(typeof(FleetUtilisationReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<FleetUtilisationReport>> FleetUtilisation(DateOnly from, DateOnly to,
        CancellationToken ct) => Ok(await reports.GetFleetUtilisationAsync(from, to, ct));

    [HttpGet("agent-performance", Name = "getEmergencyAgentPerformanceReport")]
    [ProducesResponseType(typeof(EmergencyAgentPerformanceReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<EmergencyAgentPerformanceReport>> AgentPerformance(DateOnly from, DateOnly to,
        CancellationToken ct) => Ok(await reports.GetAgentPerformanceAsync(from, to, ct));
}
