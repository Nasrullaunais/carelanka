using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class DispatchDetail : DispatchSummary
{
    public Guid AmbulanceId { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public Guid? AcknowledgedByStaffId { get; set; }
    public string? DeclinedReason { get; set; }
    public string? CancellationReason { get; set; }
    public string? ReassignmentReason { get; set; }
    public string? HandoverNotes { get; set; }
    public string? PatientCondition { get; set; }
    public string? SceneAddressLabel { get; set; }
    public string? DestinationLabel { get; set; }
    public CallStatus CallStatus { get; set; }
    public EmergencyCallOutcome? CallOutcome { get; set; }
    public string? CallDetails { get; set; }
    public string? CallerName { get; set; }
    public string? CallerPhone { get; set; }
    public string? PatientName { get; set; }
    public IReadOnlyList<Guid> CrewStaffIds { get; set; } = [];
}
