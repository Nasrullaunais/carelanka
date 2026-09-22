namespace CareLanka.Api.DTOs.Emergency;

public sealed class ReassignDispatchRequest
{
    public Guid? ReplacementAmbulanceId { get; set; }
    public string? Reason { get; set; }
}
