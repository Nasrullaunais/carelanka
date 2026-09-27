using System.Threading.Channels;

namespace CareLanka.Api.Agents.Equipment;

/// <summary>
/// This agent's own hand-off, separate from every other agent's queue - same reasoning as
/// <c>ICareRunQueue</c>. Two agents sharing one single-reader channel would mean whichever
/// worker reads first processes a workflow id it does not understand.
/// </summary>
public interface IReorderRunQueue
{
    void Enqueue(Guid workflowId);

    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken);
}

public sealed class ReorderRunQueue : IReorderRunQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(Guid workflowId) => _channel.Writer.TryWrite(workflowId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
