using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Emergency;

public class Ambulance : SoftDeletableEntity
{
    public string RegistrationNumber { get; set; } = null!;
    public decimal? CurrentLatitude { get; set; }
    public decimal? CurrentLongitude { get; set; }
    public DateTimeOffset? LocationUpdatedAt { get; set; }
    public AmbulanceStatus Status { get; set; }
    public string? OutOfServiceReason { get; set; }
    public ICollection<Dispatch> Dispatches { get; set; } = new List<Dispatch>();
    public ICollection<AmbulanceStatusHistory> StatusHistory { get; set; } = new List<AmbulanceStatusHistory>();
    public ICollection<AmbulanceCrewAssignment> CrewAssignments { get; set; } =
        new List<AmbulanceCrewAssignment>();
}
