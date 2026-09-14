namespace CareLanka.Api.Services.Emergency;

/// <summary>Persists one dispatcher alert per assigned run after the 30-second acknowledgement window.</summary>
public sealed class UnacknowledgedDispatchAlertWorker(IServiceScopeFactory scopes,
    ILogger<UnacknowledgedDispatchAlertWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = scopes.CreateScope();
            var count = await scope.ServiceProvider.GetRequiredService<IDispatchService>()
                .RaiseUnacknowledgedAlertsAsync(stoppingToken);
            if (count > 0) logger.LogWarning("Raised {Count} unacknowledged dispatch alerts", count);
        }
    }
}
