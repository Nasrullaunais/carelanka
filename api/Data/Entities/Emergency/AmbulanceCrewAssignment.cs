namespace CareLanka.Api.Data.Entities.Emergency;

public class AmbulanceCrewAssignment : Entity
{
    public Guid AmbulanceId { get; set; }

    public Ambulance Ambulance { get; set; } = null!;

    public Guid StaffMemberId { get; set; }

    public DateTimeOffset AssignedAt { get; set; }

    public DateTimeOffset? UnassignedAt { get; set; }

    public Guid AssignedByStaffId { get; set; }

    public Guid? UnassignedByStaffId { get; set; }
}
