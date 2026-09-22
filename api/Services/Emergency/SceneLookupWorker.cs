namespace CareLanka.Api.Services.Emergency;

public sealed class SceneLookupWorker(
    SceneLookupQueue queue,
    IServiceScopeFactory scopes,
    ILogger<SceneLookupWorker> logger) : BackgroundService
{
    // The free lookup services ask for no more than one request per second.
    private static readonly TimeSpan GapBetweenJobs = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<SceneLookupProcessor>().ProcessAsync(job, stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Scene lookup {Job} failed; the call or dispatch stays as it was.", job);
            }

            await Task.Delay(GapBetweenJobs, stoppingToken);
        }
    }
}
