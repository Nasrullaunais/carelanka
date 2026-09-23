using System.Threading.Channels;

namespace CareLanka.Api.Agents.Emergency;

/// <summary>
/// The dispatch agent's own hand-off, separate from the bed and care agents' queues - each agent
/// gets its own single-reader channel so a worker never reads a workflow id it does not understand.
/// </summary>
public interface IDispatchRunQueue
{
    void Enqueue(Guid proposalId);

    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken);
}

public sealed class DispatchRunQueue : IDispatchRunQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(Guid proposalId) => _channel.Writer.TryWrite(proposalId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
