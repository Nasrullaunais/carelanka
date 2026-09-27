using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public class ShiftDetailDto : ShiftDto
{
    [Required]
    public ShiftCoverageDto Coverage { get; set; } = new();

    [Required]
    public List<AllocationDto> Allocations { get; set; } = new();

    public Guid? OpenProposalId { get; set; }
}
