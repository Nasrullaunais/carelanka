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
    public string? SceneDetails { get; set; }
    public decimal SceneLatitude { get; set; }
    public decimal SceneLongitude { get; set; }
    public decimal SceneLocationAccuracyMetres { get; set; }
    public string? CallerName { get; set; }
    public string? CallerPhone { get; set; }
    public bool PatientIsCaller { get; set; }
    public IReadOnlyList<Guid> CrewStaffIds { get; set; } = [];
}
