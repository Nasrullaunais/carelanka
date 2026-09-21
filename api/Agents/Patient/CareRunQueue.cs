using System.Threading.Channels;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// The care agent's own hand-off, separate from <see cref="IAgentRunQueue"/>. Two agents sharing
/// one single-reader channel would mean whichever worker reads first processes a workflow id it
/// does not understand - so each agent gets its own queue and its own background worker.
/// </summary>
public interface ICareRunQueue
{
    void Enqueue(Guid workflowId);

    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken);
}

public sealed class CareRunQueue : ICareRunQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(Guid workflowId) => _channel.Writer.TryWrite(workflowId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
