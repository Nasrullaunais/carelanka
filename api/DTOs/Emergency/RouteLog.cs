namespace CareLanka.Api.DTOs.Emergency;

public sealed class RouteLog
{
    public Guid DispatchId { get; set; }
    public decimal OriginLatitude { get; set; }
    public decimal OriginLongitude { get; set; }
    public decimal DestinationLatitude { get; set; }
    public decimal DestinationLongitude { get; set; }
    public double PlannedDistanceKm { get; set; }
    public int PlannedDurationMinutes { get; set; }
    public DateTimeOffset? DepartedAt { get; set; }
    public DateTimeOffset? ArrivedAt { get; set; }
    public string? MapsApiReference { get; set; }
}
