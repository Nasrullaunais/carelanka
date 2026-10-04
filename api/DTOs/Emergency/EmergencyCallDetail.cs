namespace CareLanka.Api.DTOs.Emergency;

public sealed class EmergencyCallDetail : EmergencyCall
{
    public string? PatientName { get; set; }
    public IReadOnlyList<DispatchDetail> Dispatches { get; set; } = [];
    public DispatchProposalSummary? LatestProposal { get; set; }
}
