namespace CareLanka.Api.Services.Emergency;

public sealed class EmergencyOptions
{
    public const string SectionName = "Emergency";

    public int MinimumReadyCrew { get; set; } = 2;

    public int LocationMaxAgeMinutes { get; set; } = 5;

    public int AcknowledgementTimeoutSeconds { get; set; } = 30;

    public HospitalEntranceOptions HospitalEntrance { get; set; } = new();

    public RoutingOptions Routing { get; set; } = new();

    public GeocodingOptions Geocoding { get; set; } = new();
}

public sealed class GeocodingOptions
{
    public string BaseUrl { get; set; } = "https://nominatim.openstreetmap.org/";

    public string UserAgent { get; set; } = "CareLanka-university-project";

    public int TimeoutSeconds { get; set; } = 5;
}

public sealed class RoutingOptions
{
    public string BaseUrl { get; set; } = "https://router.project-osrm.org/";

    public int TimeoutSeconds { get; set; } = 3;
}

public sealed class HospitalEntranceOptions
{
    public string Label { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }
}
