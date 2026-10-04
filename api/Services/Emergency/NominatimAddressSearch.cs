using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.DTOs.Emergency;
using Microsoft.Extensions.Options;

namespace CareLanka.Api.Services.Emergency;

public sealed class NominatimAddressSearch : IAddressSearch
{
    private const int MaxResults = 5;
    private const double MinAccuracyMetres = 10;
    private const double MaxAccuracyMetres = 5000;

    private readonly HttpClient _http;
    private readonly ILogger<NominatimAddressSearch> _logger;

    public NominatimAddressSearch(HttpClient http, IOptions<EmergencyOptions> options, ILogger<NominatimAddressSearch> logger)
    {
        var geocoding = options.Value.Geocoding;
        _http = http;
        _logger = logger;
        _http.BaseAddress = new Uri(geocoding.BaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(geocoding.TimeoutSeconds);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(geocoding.UserAgent);
    }

    public async Task<IReadOnlyList<AddressSuggestion>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var url = $"search?format=jsonv2&countrycodes=lk&limit={MaxResults}&q={Uri.EscapeDataString(query.Trim())}";
        List<NominatimPlace>? places;
        try
        {
            places = await _http.GetFromJsonAsync<List<NominatimPlace>>(url, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException
            && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Address search failed for a scene lookup.");
            throw new AddressSearchUnavailableException();
        }

        return (places ?? [])
            .Select(ToSuggestion)
            .OfType<AddressSuggestion>()
            .ToList();
    }

    private static AddressSuggestion? ToSuggestion(NominatimPlace place)
    {
        if (string.IsNullOrWhiteSpace(place.DisplayName)
            || !decimal.TryParse(place.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
            || !decimal.TryParse(place.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
        {
            return null;
        }

        return new AddressSuggestion
        {
            Label = place.DisplayName.Trim(),
            Latitude = Math.Round(latitude, 6),
            Longitude = Math.Round(longitude, 6),
            ApproximateAccuracyMetres = (decimal)Math.Round(AccuracyFromBox(place.BoundingBox))
        };
    }

    // A street or building comes back with a small box, a whole town with a large one: half its diagonal is the honest error.
    private static double AccuracyFromBox(string[]? box)
    {
        if (box is not { Length: 4 }
            || !double.TryParse(box[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var south)
            || !double.TryParse(box[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var north)
            || !double.TryParse(box[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var west)
            || !double.TryParse(box[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var east))
        {
            return MaxAccuracyMetres;
        }

        var diagonalKm = StraightLineDistance.Kilometres((decimal)south, (decimal)west, (decimal)north, (decimal)east);
        return Math.Clamp(diagonalKm * 1000 / 2, MinAccuracyMetres, MaxAccuracyMetres);
    }

    private sealed record NominatimPlace(
        [property: JsonPropertyName("display_name")] string? DisplayName,
        [property: JsonPropertyName("lat")] string? Latitude,
        [property: JsonPropertyName("lon")] string? Longitude,
        [property: JsonPropertyName("boundingbox")] string[]? BoundingBox);
}
