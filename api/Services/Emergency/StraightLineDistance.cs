namespace CareLanka.Api.Services.Emergency;

public static class StraightLineDistance
{
    private const double EarthRadiusKm = 6371.0;

    public static DistanceMeasurement Measure(
        IReadOnlyCollection<AmbulanceLocation> ambulances,
        decimal destinationLatitude,
        decimal destinationLongitude)
    {
        var byAmbulance = ambulances
            .Where(ambulance => ambulance.Latitude is not null && ambulance.Longitude is not null)
            .ToDictionary(
                ambulance => ambulance.Id,
                ambulance => new AmbulanceTravel(
                    Between(ambulance.Latitude!.Value, ambulance.Longitude!.Value, destinationLatitude, destinationLongitude),
                    null));

        return new DistanceMeasurement(byAmbulance, IsStraightLine: true);
    }

    private static double Between(decimal fromLatitude, decimal fromLongitude, decimal toLatitude, decimal toLongitude)
    {
        var latitudeDelta = ToRadians((double)(fromLatitude - toLatitude));
        var longitudeDelta = ToRadians((double)(fromLongitude - toLongitude));
        var haversine = Math.Pow(Math.Sin(latitudeDelta / 2), 2)
            + Math.Cos(ToRadians((double)toLatitude)) * Math.Cos(ToRadians((double)fromLatitude))
            * Math.Pow(Math.Sin(longitudeDelta / 2), 2);

        return EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1 - haversine));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
