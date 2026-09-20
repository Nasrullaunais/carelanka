using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Emergency;

public class Dispatch : AuditedEntity
{
    public Guid EmergencyCallId { get; set; }
    public EmergencyCall EmergencyCall { get; set; } = null!;
    public Guid AmbulanceId { get; set; }
    public Ambulance Ambulance { get; set; } = null!;
    public Guid? DestinationWardId { get; set; }
    public DispatchStatus Status { get; set; }
    public Guid? SupersededByDispatchId { get; set; }
    public Dispatch? SupersededByDispatch { get; set; }
    public DateTimeOffset DispatchedAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public Guid? AcknowledgedByStaffId { get; set; }
    public string? DeclinedReason { get; set; }
    public string? CancellationReason { get; set; }
    public string? ReassignmentReason { get; set; }
    public string? HandoverNotes { get; set; }
    public string? PatientCondition { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public ICollection<DispatchCrew> Crew { get; set; } = new List<DispatchCrew>();
    public RouteLog? RouteLog { get; set; }
    public uint Version { get; set; }
}
