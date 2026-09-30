namespace CareLanka.Api.DTOs.Emergency;

public sealed class EmergencyCallDetail : EmergencyCall
{
    public IReadOnlyList<CallDispatch> Dispatches { get; set; } = [];
    public DispatchProposalSummary? LatestProposal { get; set; }
}
