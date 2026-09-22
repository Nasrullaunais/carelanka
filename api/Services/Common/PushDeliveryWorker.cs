using Microsoft.Extensions.Options;

namespace CareLanka.Api.Services.Common;

public sealed class PushDeliveryWorker(
    IServiceScopeFactory scopes,
    IOptions<PushOptions> options,
    ILogger<PushDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PollSeconds));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<PushDeliveryProcessor>().DeliverDueAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Push delivery pass failed; it will run again shortly.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
