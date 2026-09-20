using Microsoft.Extensions.Options;

namespace CareLanka.Api.Services.Emergency;

public sealed class PreAdmissionWorker(
    IServiceScopeFactory scopes,
    IOptions<EmergencyOptions> options,
    ILogger<PreAdmissionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PreAdmission.PollSeconds));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<PreAdmissionProcessor>().SendDueAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Pre-admission pass failed; it will run again shortly.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
