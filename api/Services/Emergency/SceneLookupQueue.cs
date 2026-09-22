using System.Threading.Channels;

namespace CareLanka.Api.Services.Emergency;

public abstract record SceneLookupJob;

public sealed record AddressLookupJob(Guid CallId) : SceneLookupJob;

public sealed record RoutePlanJob(Guid DispatchId, decimal OriginLatitude, decimal OriginLongitude) : SceneLookupJob;

public interface ISceneLookupQueue
{
    void Enqueue(SceneLookupJob job);
}

public sealed class SceneLookupQueue : ISceneLookupQueue
{
    private readonly Channel<SceneLookupJob> _channel = Channel.CreateUnbounded<SceneLookupJob>(
        new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<SceneLookupJob> Reader => _channel.Reader;

    public void Enqueue(SceneLookupJob job) => _channel.Writer.TryWrite(job);
}
