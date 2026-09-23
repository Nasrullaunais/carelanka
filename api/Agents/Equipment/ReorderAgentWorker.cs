namespace CareLanka.Api.Agents.Equipment;

/// <summary>
/// Runs queued reorder-suggestion workflows one at a time, each in its own DI scope. Same shape
/// as <c>CareAgentWorker</c>.
/// </summary>
public sealed class ReorderAgentWorker : BackgroundService
{
    private readonly IReorderRunQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ReorderAgentWorker> _log;

    public ReorderAgentWorker(
        IReorderRunQueue queue, IServiceScopeFactory scopes, ILogger<ReorderAgentWorker> log)
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
                    .GetRequiredService<ReorderAgentExecutor>()
                    .ExecuteAsync(workflowId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _log.LogError(
                    exception, "Reorder agent workflow {WorkflowId} could not be run.", workflowId);
            }
        }
    }
}
