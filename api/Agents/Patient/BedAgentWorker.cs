namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// Runs queued bed-agent workflows one at a time, each in its own DI scope so it gets its own
/// <c>DbContext</c>.
/// </summary>
/// <remarks>
/// It never lets an exception escape. A run that throws has to end as a workflow row marked
/// <c>failed</c> with the reason on it - a run that simply stops leaves the row at
/// <c>running</c> forever, and a nurse polling it waits for an answer that is never coming.
/// </remarks>
public sealed class BedAgentWorker : BackgroundService
{
    private readonly IAgentRunQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<BedAgentWorker> _log;

    public BedAgentWorker(
        IAgentRunQueue queue, IServiceScopeFactory scopes, ILogger<BedAgentWorker> log)
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
                    .GetRequiredService<BedAgentExecutor>()
                    .ExecuteAsync(workflowId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _log.LogError(
                    exception, "Bed agent workflow {WorkflowId} could not be run.", workflowId);
            }
        }
    }
}
