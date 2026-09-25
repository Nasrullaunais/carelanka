using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public class DecideLeaveResponse
{
    [Required]
    public LeaveRequestDto LeaveRequest { get; set; } = null!;

    [Required]
    public IReadOnlyList<AllocationSummaryDto> ReleasedAllocations { get; set; } = Array.Empty<AllocationSummaryDto>();

    [Required]
    public IReadOnlyList<Guid> RosterProposalIds { get; set; } = Array.Empty<Guid>();
}
