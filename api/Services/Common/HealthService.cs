using System.Reflection;
using CareLanka.Api.Data;
using CareLanka.Api.DTOs.Common;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Common;

public sealed class HealthService : IHealthService
{
    private readonly CareLankaDbContext _db;
    private readonly ILogger<HealthService> _logger;

    public HealthService(CareLankaDbContext db, ILogger<HealthService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<HealthStatus> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        bool databaseUp;

        try
        {
            databaseUp = await _db.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Health check could not reach PostgreSQL");
            databaseUp = false;
        }

        return new HealthStatus
        {
            Status = databaseUp ? "healthy" : "degraded",
            Database = databaseUp ? "up" : "down",
            Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0",
            CheckedAt = DateTimeOffset.UtcNow
        };
    }
}
