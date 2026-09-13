namespace CareLanka.Api.Services.Emergency.Stubs;

// STUB: replace with the maps provider integration (STUBS.md row 4).
public sealed class StubAmbulanceDistanceService : IAmbulanceDistanceService
{
    public Task<IReadOnlyDictionary<Guid, double?>> MeasureAsync(
        IReadOnlyCollection<AmbulanceLocation> ambulances,
        decimal destinationLatitude,
        decimal destinationLongitude,
        CancellationToken cancellationToken = default)
    {
        var distances = ambulances.ToDictionary(
            ambulance => ambulance.Id,
            ambulance => DistanceFrom(ambulance, destinationLatitude, destinationLongitude));

        return Task.FromResult<IReadOnlyDictionary<Guid, double?>>(distances);
    }

    private static double? DistanceFrom(
        AmbulanceLocation ambulance,
        decimal latitude,
        decimal longitude)
    {
        if (ambulance.Latitude is null || ambulance.Longitude is null)
        {
            return null;
        }

        const double earthRadiusKm = 6371.0;
        var latitudeDelta = DegreesToRadians((double)(ambulance.Latitude.Value - latitude));
        var longitudeDelta = DegreesToRadians((double)(ambulance.Longitude.Value - longitude));
        var originLatitude = DegreesToRadians((double)latitude);
        var ambulanceLatitude = DegreesToRadians((double)ambulance.Latitude.Value);
        var haversine = Math.Pow(Math.Sin(latitudeDelta / 2), 2)
            + Math.Cos(originLatitude) * Math.Cos(ambulanceLatitude)
            * Math.Pow(Math.Sin(longitudeDelta / 2), 2);

        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1 - haversine));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
}
