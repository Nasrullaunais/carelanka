using System.Net;
using System.Text;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Services.Emergency;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CareLanka.Api.Tests;

public sealed class NominatimAddressSearchTests
{
    [Fact]
    public async Task Matches_are_limited_to_Sri_Lanka_and_carry_an_honest_accuracy()
    {
        var handler = new FakeHandler(_ => Reply("""
            [
              {"display_name":"Ward Place, Colombo 07","lat":"6.9125","lon":"79.8667",
               "boundingbox":["6.9120","6.9130","79.8662","79.8672"]},
              {"display_name":"Colombo","lat":"6.9271","lon":"79.8612",
               "boundingbox":["6.8","7.0","79.8","80.0"]}
            ]
            """));

        var results = await Service(handler).SearchAsync("  ward place colombo ");

        Assert.Equal(2, results.Count);
        Assert.Equal("Ward Place, Colombo 07", results[0].Label);
        Assert.Equal(6.9125m, results[0].Latitude);
        Assert.InRange(results[0].ApproximateAccuracyMetres, 50, 100);
        Assert.Equal(5000, results[1].ApproximateAccuracyMetres);
        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("countrycodes=lk", query);
        Assert.Contains("limit=5", query);
        Assert.Contains("q=ward%20place%20colombo", query);
        Assert.Equal("CareLanka-university-project", handler.LastRequest.Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task A_tiny_box_is_never_claimed_to_be_more_precise_than_ten_metres()
    {
        var handler = new FakeHandler(_ => Reply("""
            [{"display_name":"Gate","lat":"6.9","lon":"79.8","boundingbox":["6.9","6.9","79.8","79.8"]}]
            """));

        var result = Assert.Single(await Service(handler).SearchAsync("gate"));

        Assert.Equal(10, result.ApproximateAccuracyMetres);
    }

    [Fact]
    public async Task Places_without_usable_coordinates_are_skipped()
    {
        var handler = new FakeHandler(_ => Reply("""
            [{"display_name":"No point","lat":"abc","lon":"79.8"},{"display_name":"","lat":"6.9","lon":"79.8"}]
            """));

        Assert.Empty(await Service(handler).SearchAsync("nothing"));
    }

    public static TheoryData<string, Func<HttpRequestMessage, HttpResponseMessage>> Failures => new()
    {
        { "server error", _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) },
        { "rate limited", _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests) },
        { "timeout", _ => throw new TaskCanceledException("timed out") },
        { "not json", _ => Reply("<html>oops</html>") }
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task A_failing_address_service_is_reported_as_unavailable(
        string reason, Func<HttpRequestMessage, HttpResponseMessage> reply)
    {
        var exception = await Record.ExceptionAsync(() => Service(new FakeHandler(reply)).SearchAsync("colombo"));

        Assert.IsType<AddressSearchUnavailableException>(exception);
        Assert.NotNull(reason);
    }

    [Fact]
    public async Task A_caller_who_gives_up_is_not_mistaken_for_an_outage()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Service(new FakeHandler(_ => Reply("[]"))).SearchAsync("colombo", cancelled.Token));
    }

    private static NominatimAddressSearch Service(FakeHandler handler) =>
        new(new HttpClient(handler), Options.Create(new EmergencyOptions()), NullLogger<NominatimAddressSearch>.Instance);

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
