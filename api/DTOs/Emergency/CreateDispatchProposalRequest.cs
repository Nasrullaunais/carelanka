namespace CareLanka.Api.DTOs.Emergency;

public sealed class CreateDispatchProposalRequest
{
    public Guid EmergencyCallId { get; set; }
    public bool AllowDiversion { get; set; } = true;
    public IReadOnlyList<Guid>? ExcludeAmbulanceIds { get; set; }
}
