using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class ListDispatchProposalsRequest
{
    public DispatchProposalStatus? Status { get; set; }
    public Guid? EmergencyCallId { get; set; }
    public bool? IsDiversion { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
