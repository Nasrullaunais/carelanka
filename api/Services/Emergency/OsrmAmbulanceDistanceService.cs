using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace CareLanka.Api.Services.Emergency;

public sealed class OsrmAmbulanceDistanceService : IAmbulanceDistanceService
{
    private readonly HttpClient _http;
    private readonly ILogger<OsrmAmbulanceDistanceService> _logger;

    public OsrmAmbulanceDistanceService(
        HttpClient http,
        IOptions<EmergencyOptions> options,
        ILogger<OsrmAmbulanceDistanceService> logger)
    {
        _http = http;
        _logger = logger;
        _http.BaseAddress = new Uri(options.Value.Routing.BaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(options.Value.Routing.TimeoutSeconds);
    }

    public async Task<DistanceMeasurement> MeasureAsync(
        IReadOnlyCollection<AmbulanceLocation> ambulances,
        decimal destinationLatitude,
        decimal destinationLongitude,
        CancellationToken cancellationToken = default)
    {
        var located = ambulances.Where(ambulance => ambulance.Latitude is not null && ambulance.Longitude is not null).ToList();
        if (located.Count == 0)
        {
            return new DistanceMeasurement(new Dictionary<Guid, AmbulanceTravel>(), IsStraightLine: false);
        }

        try
        {
            return await MeasureByRoadAsync(located, destinationLatitude, destinationLongitude, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidDataException
            or System.Text.Json.JsonException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Road routing failed; ranking ambulances by straight-line distance instead.");
            return StraightLineDistance.Measure(ambulances, destinationLatitude, destinationLongitude);
        }
    }

    private async Task<DistanceMeasurement> MeasureByRoadAsync(
        List<AmbulanceLocation> located,
        decimal destinationLatitude,
        decimal destinationLongitude,
        CancellationToken cancellationToken)
    {
        var coordinates = located
            .Select(ambulance => Coordinate(ambulance.Longitude!.Value, ambulance.Latitude!.Value))
            .Append(Coordinate(destinationLongitude, destinationLatitude));
        var sources = string.Join(';', Enumerable.Range(0, located.Count));
        var url = $"table/v1/driving/{string.Join(';', coordinates)}?sources={sources}&destinations={located.Count}&annotations=duration,distance";

        var table = await _http.GetFromJsonAsync<OsrmTable>(url, cancellationToken)
            ?? throw new InvalidDataException("Routing service returned an empty reply.");
        if (table.Code != "Ok" || table.Durations is null || table.Distances is null
            || table.Durations.Length != located.Count || table.Distances.Length != located.Count)
        {
            throw new InvalidDataException($"Routing service returned an unusable reply ({table.Code}).");
        }

        var byAmbulance = new Dictionary<Guid, AmbulanceTravel>();
        for (var index = 0; index < located.Count; index++)
        {
            var seconds = table.Durations[index].FirstOrDefault();
            var meters = table.Distances[index].FirstOrDefault();
            if (seconds is null || meters is null)
            {
                throw new InvalidDataException("Routing service found no road between an ambulance and the scene.");
            }

            byAmbulance[located[index].Id] = new AmbulanceTravel(meters.Value / 1000, (int)Math.Round(seconds.Value));
        }

        return new DistanceMeasurement(byAmbulance, IsStraightLine: false);
    }

    private static string Coordinate(decimal longitude, decimal latitude) =>
        string.Create(CultureInfo.InvariantCulture, $"{longitude},{latitude}");

    private sealed record OsrmTable(
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("durations")] double?[][]? Durations,
        [property: JsonPropertyName("distances")] double?[][]? Distances);
}
