using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public class EndAllocationResponse
{
    [Required]
    public AllocationDto Allocation { get; set; } = null!;

    [Required]
    public ShiftCoverageDto ShiftCoverage { get; set; } = null!;

    public Guid? RosterProposalId { get; set; }
}