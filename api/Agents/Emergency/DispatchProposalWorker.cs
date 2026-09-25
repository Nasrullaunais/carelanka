using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

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

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.WhenAll(ConsumeAsync(stoppingToken), RecoverAsync(stoppingToken));

    // The channel is a wake-up mechanism; persisted pending proposals survive restarts
    // and transient processing failures. Execution checks status, so duplicates are harmless.
    private async Task RecoverAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
                var pending = await db.DispatchProposals.AsNoTracking()
                    .Where(proposal => proposal.Status == DispatchProposalStatus.Pending)
                    .OrderBy(proposal => proposal.CreatedAt)
                    .Select(proposal => proposal.Id).Take(100).ToListAsync(stoppingToken);
                foreach (var id in pending) _queue.Enqueue(id);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _log.LogError(exception, "Pending dispatch proposals could not be recovered; retrying shortly.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
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
