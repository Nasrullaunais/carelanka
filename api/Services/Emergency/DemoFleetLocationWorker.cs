using Microsoft.Extensions.Options;

namespace CareLanka.Api.Services.Emergency;

public sealed class DemoFleetLocationWorker(
    IServiceScopeFactory scopes,
    IOptions<EmergencyOptions> options,
    ILogger<DemoFleetLocationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var fleet = options.Value.DemoFleet;
        if (!fleet.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(fleet.IntervalMinutes));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<DemoFleetLocationProcessor>().RefreshAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Demo fleet location refresh failed; it will run again shortly.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
