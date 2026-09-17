namespace CareLanka.Api.DTOs.Emergency;

public sealed class DispatchDetail : DispatchSummary
{
    public Guid AmbulanceId { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public Guid? AcknowledgedByStaffId { get; set; }
    public string? DeclinedReason { get; set; }
    public IReadOnlyList<Guid> CrewStaffIds { get; set; } = [];
}
