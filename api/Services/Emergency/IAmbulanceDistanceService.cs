namespace CareLanka.Api.Services.Emergency;

public interface IAmbulanceDistanceService
{
    Task<IReadOnlyDictionary<Guid, double?>> MeasureAsync(
        IReadOnlyCollection<AmbulanceLocation> ambulances,
        decimal destinationLatitude,
        decimal destinationLongitude,
        CancellationToken cancellationToken = default);
}

public sealed record AmbulanceLocation(Guid Id, decimal? Latitude, decimal? Longitude);
