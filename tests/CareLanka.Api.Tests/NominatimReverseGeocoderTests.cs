using System.Net;
using System.Text;
using CareLanka.Api.Services.Emergency;
using Microsoft.Extensions.Options;
using Xunit;

namespace CareLanka.Api.Tests;

public sealed class NominatimReverseGeocoderTests
{
    [Fact]
    public async Task The_address_comes_from_the_lookup_reply_and_the_request_names_this_app()
    {
        var handler = new FakeHandler(_ => Reply("""{"display_name":"  Galle Road, Colombo, Sri Lanka "}"""));

        var address = await Geocoder(handler).FindAddressAsync(6.927079m, 79.861244m);

        Assert.Equal("Galle Road, Colombo, Sri Lanka", address);
        Assert.Equal("/reverse?format=jsonv2&zoom=18&lat=6.927079&lon=79.861244", handler.LastRequest!.RequestUri!.PathAndQuery);
        Assert.Contains("CareLanka", handler.LastRequest.Headers.UserAgent.ToString());
    }

    [Theory]
    [InlineData("""{"error":"Unable to geocode"}""")]
    [InlineData("""{"display_name":"   "}""")]
    public async Task A_reply_without_an_address_gives_none(string json)
    {
        Assert.Null(await Geocoder(new FakeHandler(_ => Reply(json))).FindAddressAsync(0.1m, 0.1m));
    }

    [Fact]
    public async Task A_failing_lookup_throws_so_the_caller_can_log_it()
    {
        var geocoder = Geocoder(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)));

        await Assert.ThrowsAsync<HttpRequestException>(() => geocoder.FindAddressAsync(6.9m, 79.8m));
    }

    private static NominatimReverseGeocoder Geocoder(FakeHandler handler) =>
        new(new HttpClient(handler), Options.Create(new EmergencyOptions()));

    private static HttpResponseMessage Reply(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(reply(request));
        }
    }
}
