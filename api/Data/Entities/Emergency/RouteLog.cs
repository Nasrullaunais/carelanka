namespace CareLanka.Api.Data.Entities.Emergency;

public class RouteLog : AuditedEntity
{
    public Guid DispatchId { get; set; }
    public Dispatch Dispatch { get; set; } = null!;
    public decimal OriginLatitude { get; set; }
    public decimal OriginLongitude { get; set; }
    public decimal DestinationLatitude { get; set; }
    public decimal DestinationLongitude { get; set; }
    public decimal PlannedDistanceKm { get; set; }
    public int PlannedDurationMinutes { get; set; }
    public DateTimeOffset? DepartedAt { get; set; }
    public DateTimeOffset? ArrivedAt { get; set; }
    public string? MapsApiReference { get; set; }
}
