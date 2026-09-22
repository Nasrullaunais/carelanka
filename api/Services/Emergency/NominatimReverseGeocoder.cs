using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace CareLanka.Api.Services.Emergency;

public sealed class NominatimReverseGeocoder : IReverseGeocoder
{
    private readonly HttpClient _http;

    public NominatimReverseGeocoder(HttpClient http, IOptions<EmergencyOptions> options)
    {
        var geocoding = options.Value.Geocoding;
        _http = http;
        _http.BaseAddress = new Uri(geocoding.BaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(geocoding.TimeoutSeconds);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(geocoding.UserAgent);
    }

    public async Task<string?> FindAddressAsync(decimal latitude, decimal longitude, CancellationToken cancellationToken = default)
    {
        var url = string.Create(CultureInfo.InvariantCulture, $"reverse?format=jsonv2&zoom=18&lat={latitude}&lon={longitude}");
        var reply = await _http.GetFromJsonAsync<NominatimReply>(url, cancellationToken);
        return string.IsNullOrWhiteSpace(reply?.DisplayName) ? null : reply.DisplayName.Trim();
    }

    private sealed record NominatimReply([property: JsonPropertyName("display_name")] string? DisplayName);
}
