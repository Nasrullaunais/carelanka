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

public sealed record AmbulanceTravel(double DistanceKm, int? DriveSeconds)
{
    // Rounded up everywhere, so a 5 min 01 s drive is never shown as 5 minutes.
    public int? DriveMinutes => DriveSeconds is { } seconds ? (int)Math.Ceiling(seconds / 60.0) : null;
}

public sealed record DistanceMeasurement(
    IReadOnlyDictionary<Guid, AmbulanceTravel> ByAmbulance,
    bool IsStraightLine);
