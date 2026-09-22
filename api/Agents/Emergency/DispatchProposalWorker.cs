namespace CareLanka.Api.Agents.Emergency;

/// <summary>
/// Runs queued dispatch proposals one at a time, each in its own DI scope so it gets its own
/// <c>DbContext</c>.
/// </summary>
public sealed class DispatchProposalWorker : BackgroundService
{
    private readonly IDispatchRunQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<DispatchProposalWorker> _log;

    public DispatchProposalWorker(IDispatchRunQueue queue, IServiceScopeFactory scopes, ILogger<DispatchProposalWorker> log)
    {
        _queue = queue;
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var proposalId in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();

                await scope.ServiceProvider
                    .GetRequiredService<DispatchProposalExecutor>()
                    .ExecuteAsync(proposalId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _log.LogError(exception, "Dispatch proposal {ProposalId} could not be run.", proposalId);
            }
        }
    }
}
