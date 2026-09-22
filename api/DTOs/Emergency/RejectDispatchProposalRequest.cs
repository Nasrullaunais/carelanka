using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class RejectDispatchProposalRequest
{
    public DispatchRejectionReason Reason { get; set; }
    public string? Notes { get; set; }
}
