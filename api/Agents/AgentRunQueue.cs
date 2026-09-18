using System.Threading.Channels;

namespace CareLanka.Api.Agents;

/// <summary>
/// The hand-off between the request that started an agent and the background worker that runs it.
/// </summary>
/// <remarks>
/// The contract answers 202 with a poll URL rather than blocking, because a run makes several
/// database reads and a network call to a model, and holding a request open for all of it ties a
/// nurse's screen to somebody else's quota.
/// </remarks>
public interface IAgentRunQueue
{
    void Enqueue(Guid workflowId);

    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken);
}

public sealed class AgentRunQueue : IAgentRunQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(Guid workflowId) => _channel.Writer.TryWrite(workflowId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
