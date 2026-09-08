using CareLanka.Api.DTOs.Common;
using CareLanka.Api.Services.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Common;

/// <summary>Unauthenticated liveness probe.</summary>
[ApiController]
[Route("api/health")]
[Tags("Health")]
public class HealthController : ControllerBase
{
    private readonly IHealthService _health;

    public HealthController(IHealthService health) => _health = health;

    /// <summary>Liveness and database connectivity. An API that answers "up" while PostgreSQL is unreachable is worse than one that says nothing, because it stops anyone from looking.</summary>
    [AllowAnonymous]
    [HttpGet(Name = "getHealth")]
    [ProducesResponseType(typeof(HealthStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthStatus), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthStatus>> GetHealth(CancellationToken ct)
    {
        var status = await _health.GetHealthAsync(ct);

        return status.Database == "up"
            ? Ok(status)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, status);
    }
}
