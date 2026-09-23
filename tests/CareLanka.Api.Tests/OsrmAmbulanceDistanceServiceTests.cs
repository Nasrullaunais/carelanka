using System.Net;
using System.Text;
using CareLanka.Api.Services.Emergency;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CareLanka.Api.Tests;

public sealed class OsrmAmbulanceDistanceServiceTests
{
    private static readonly Guid Near = Guid.NewGuid();
    private static readonly Guid Far = Guid.NewGuid();
    private static readonly Guid NoPosition = Guid.NewGuid();

    private static readonly AmbulanceLocation[] Fleet =
    [
        new(Far, 6.95m, 79.90m),
        new(Near, 6.92m, 79.87m),
        new(NoPosition, null, null)
    ];

    [Fact]
    public async Task Road_times_come_from_the_routing_service_and_skip_ambulances_without_a_position()
    {
        var handler = new FakeHandler(_ => Reply("""{"code":"Ok","durations":[[900],[240]],"distances":[[8200],[1900]]}"""));

        var result = await Service(handler).MeasureAsync(Fleet, 6.9186m, 79.8686m);

        Assert.False(result.IsStraightLine);
        Assert.Equal(new AmbulanceTravel(8.2, 900), result.ByAmbulance[Far]);
        Assert.Equal(new AmbulanceTravel(1.9, 240), result.ByAmbulance[Near]);
        Assert.DoesNotContain(NoPosition, result.ByAmbulance.Keys);
        Assert.Equal("CareLanka/1.0", handler.LastRequest!.Headers.UserAgent.ToString());
        Assert.Equal(
            "table/v1/driving/79.90,6.95;79.87,6.92;79.8686,6.9186?sources=0;1&destinations=2&annotations=duration,distance",
            handler.LastRequest!.RequestUri!.PathAndQuery.TrimStart('/'));
    }

    [Fact]
    public async Task Nothing_is_sent_when_no_ambulance_has_a_position()
    {
        var handler = new FakeHandler(_ => throw new InvalidOperationException("must not be called"));

        var result = await Service(handler).MeasureAsync([new AmbulanceLocation(NoPosition, null, null)], 6.9m, 79.8m);

        Assert.Empty(result.ByAmbulance);
        Assert.False(result.IsStraightLine);
    }

    public static TheoryData<string, Func<HttpRequestMessage, HttpResponseMessage>> Failures => new()
    {
        { "server error", _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) },
        { "quota exceeded", _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests) },
        { "timeout", _ => throw new TaskCanceledException("timed out") },
        { "not json", _ => Reply("<html>oops</html>") },
        { "error code", _ => Reply("""{"code":"NoRoute","message":"no route"}""") },
        { "wrong matrix size", _ => Reply("""{"code":"Ok","durations":[[1]],"distances":[[1]]}""") },
        { "unreachable ambulance", _ => Reply("""{"code":"Ok","durations":[[null],[240]],"distances":[[null],[1900]]}""") }
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task When_the_routing_service_fails_every_ambulance_is_ranked_by_straight_line(
        string reason, Func<HttpRequestMessage, HttpResponseMessage> reply)
    {
        var result = await Service(new FakeHandler(reply)).MeasureAsync(Fleet, 6.9186m, 79.8686m);

        Assert.True(result.IsStraightLine, reason);
        Assert.All(result.ByAmbulance.Values, travel => Assert.Null(travel.DriveSeconds));
        Assert.True(result.ByAmbulance[Near].DistanceKm < result.ByAmbulance[Far].DistanceKm);
        Assert.DoesNotContain(NoPosition, result.ByAmbulance.Keys);
    }

    [Fact]
    public async Task A_caller_who_gives_up_is_not_mistaken_for_a_routing_failure()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var handler = new FakeHandler(_ => Reply("""{"code":"Ok","durations":[[1],[1]],"distances":[[1],[1]]}"""));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Service(handler).MeasureAsync(Fleet, 6.9m, 79.8m, cancelled.Token));
    }

    private static OsrmAmbulanceDistanceService Service(FakeHandler handler) =>
        new(new HttpClient(handler), Options.Create(new EmergencyOptions()), NullLogger<OsrmAmbulanceDistanceService>.Instance);

    private static HttpResponseMessage Reply(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(reply(request));
        }
    }
}
