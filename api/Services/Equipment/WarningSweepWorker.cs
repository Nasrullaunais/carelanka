using Microsoft.Extensions.Options;

namespace CareLanka.Api.Services.Equipment;

/// <summary>Runs the deterministic warning sweep on a timer, so warnings appear without anybody
/// pressing Run check. The interval is Equipment:WarningSweepIntervalMinutes; 0 turns it off.</summary>
public sealed class WarningSweepWorker(
    IServiceScopeFactory scopes,
    IOptions<EquipmentOptions> options,
    ILogger<WarningSweepWorker> logger) : BackgroundService
{
    // Long enough for startup and migrations to settle before the first sweep.
    private static readonly TimeSpan FirstRunDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = options.Value.WarningSweepIntervalMinutes;

        if (minutes <= 0)
        {
            logger.LogInformation("The warning sweep timer is off; only Run check will sweep");
            return;
        }

        await Task.Delay(FirstRunDelay, stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));

        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<IWarningService>()
                    .SweepAsync(stoppingToken);

                logger.LogInformation(
                    "Warning sweep: {Raised} raised, {Updated} updated, {Resolved} resolved, {Open} open",
                    result.Raised, result.Updated, result.Resolved, result.StillOpen);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // One failed sweep must not stop the next one.
                logger.LogError(exception, "The warning sweep failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
