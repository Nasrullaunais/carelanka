namespace CareLanka.Api.Services.Common;

public sealed class NotificationScheduleWorker(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<NotificationScheduleWorker> logger) : BackgroundService
{
    private static readonly TimeSpan ReminderInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromDays(1);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.WhenAll(
            RunEveryAsync("Appointment reminders", ReminderInterval,
                (services, ct) => services.GetRequiredService<IAppointmentReminderService>().SendDueAsync(ct),
                stoppingToken),
            RunEveryAsync("Maintenance due alerts", MaintenanceInterval,
                (services, ct) => services.GetRequiredService<IMaintenanceDueService>().SendDueAsync(ct),
                stoppingToken),
            RunEveryAsync("Inbox clean-up", CleanupInterval,
                (services, ct) => services.GetRequiredService<IInboxCleanupService>().DeleteExpiredAsync(ct),
                stoppingToken));

    private async Task RunEveryAsync(
        string job,
        TimeSpan interval,
        Func<IServiceProvider, CancellationToken, Task<int>> run,
        CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval, clock);
        try
        {
            do
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var count = await run(scope.ServiceProvider, stoppingToken);
                    logger.LogInformation("{Job}: {Count} handled", job, count);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning(exception, "{Job} failed; it will run again on schedule.", job);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
