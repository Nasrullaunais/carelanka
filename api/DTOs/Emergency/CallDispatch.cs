namespace CareLanka.Api.DTOs.Emergency;

// How a run went. The scene and caller belong to the call, so they are not repeated here.
public class CallDispatch : DispatchSummary
{
    public Guid AmbulanceId { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public Guid? AcknowledgedByStaffId { get; set; }
    public string? DeclinedReason { get; set; }
    public string? CancellationReason { get; set; }
    public string? ReassignmentReason { get; set; }
    public Guid? SupersededByDispatchId { get; set; }
    public string? HandoverNotes { get; set; }
    public string? PatientCondition { get; set; }
}
