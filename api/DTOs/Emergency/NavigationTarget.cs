namespace CareLanka.Api.DTOs.Emergency;

public sealed class NavigationTarget
{
    public Guid DispatchId { get; set; }
    public NavigationWaypoint WaypointType { get; set; }
    public double DestinationLatitude { get; set; }
    public double DestinationLongitude { get; set; }
    public string? DestinationLabel { get; set; }
    public string GoogleMapsUrl { get; set; } = string.Empty;
}
