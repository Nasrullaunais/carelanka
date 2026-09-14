namespace CareLanka.Api.DTOs.Emergency;

public sealed class EmergencyCallDetail : EmergencyCall
{
    public IReadOnlyList<DispatchSummary> Dispatches { get; set; } = [];
    public Guid? OpenProposalId { get; set; }
}
