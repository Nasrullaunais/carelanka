namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// Runs queued care-agent workflows one at a time, each in its own DI scope. Same shape as
/// <c>BedAgentWorker</c>.
/// </summary>
public sealed class CareAgentWorker : BackgroundService
{
    private readonly ICareRunQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<CareAgentWorker> _log;

    public CareAgentWorker(ICareRunQueue queue, IServiceScopeFactory scopes, ILogger<CareAgentWorker> log)
    {
        _queue = queue;
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var workflowId in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();

                await scope.ServiceProvider
                    .GetRequiredService<CareAgentExecutor>()
                    .ExecuteAsync(workflowId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _log.LogError(
                    exception, "Care agent workflow {WorkflowId} could not be run.", workflowId);
            }
        }
    }
}
