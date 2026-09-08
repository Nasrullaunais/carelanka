using System.Reflection;
using CareLanka.Api.Data;
using CareLanka.Api.DTOs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Controllers.Common;

/// <summary>Unauthenticated liveness probe.</summary>
[ApiController]
[Route("api/health")]
[Tags("Health")]
public class HealthController : ControllerBase
{
    private readonly CareLankaDbContext _db;
    private readonly ILogger<HealthController> _logger;

    public HealthController(CareLankaDbContext db, ILogger<HealthController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Liveness and database connectivity. An API that answers "up" while PostgreSQL is unreachable is worse than one that says nothing, because it stops anyone from looking.</summary>
    [AllowAnonymous]
    [HttpGet(Name = "getHealth")]
    [ProducesResponseType(typeof(HealthStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthStatus), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthStatus>> GetHealth(CancellationToken ct)
    {
        bool databaseUp;

        try
        {
            databaseUp = await _db.Database.CanConnectAsync(ct);
        }
        catch (Exception ex)
        {
            // The one justified catch in a controller: an unreachable database is the answer
            // this endpoint exists to give, not an error to hand to the exception handler.
            _logger.LogError(ex, "Health check could not reach PostgreSQL");
            databaseUp = false;
        }

        var status = new HealthStatus
        {
            Status = databaseUp ? "healthy" : "degraded",
            Database = databaseUp ? "up" : "down",
            Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0",
            CheckedAt = DateTimeOffset.UtcNow
        };

        return databaseUp
            ? Ok(status)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, status);
    }
}
