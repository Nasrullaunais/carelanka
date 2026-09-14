namespace CareLanka.Api.DTOs.Emergency;

public sealed class AmbulanceCrewAssignment
{
    public Guid Id { get; set; }

    public Guid AmbulanceId { get; set; }

    public Guid StaffMemberId { get; set; }

    public string? FullName { get; set; }

    public DateTimeOffset AssignedAt { get; set; }

    public Guid AssignedByStaffId { get; set; }

    public DateTimeOffset? UnassignedAt { get; set; }

    public Guid? UnassignedByStaffId { get; set; }
}
