using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Emergency;

public class AmbulanceStatusHistory : Entity
{
    public Guid AmbulanceId { get; set; }
    public Ambulance Ambulance { get; set; } = null!;
    public AmbulanceStatus Status { get; set; }
    public DateTimeOffset StartedAt { get; set; }
}
