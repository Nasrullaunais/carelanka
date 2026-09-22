namespace CareLanka.Api.Services.Emergency;

public interface IAmbulanceDistanceService
{
    Task<DistanceMeasurement> MeasureAsync(
        IReadOnlyCollection<AmbulanceLocation> ambulances,
        decimal destinationLatitude,
        decimal destinationLongitude,
        CancellationToken cancellationToken = default);
}

public sealed record AmbulanceLocation(Guid Id, decimal? Latitude, decimal? Longitude);

public sealed record AmbulanceTravel(double DistanceKm, int? DriveSeconds);

public sealed record DistanceMeasurement(
    IReadOnlyDictionary<Guid, AmbulanceTravel> ByAmbulance,
    bool IsStraightLine);
