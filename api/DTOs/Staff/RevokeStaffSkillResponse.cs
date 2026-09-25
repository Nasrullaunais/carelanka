using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public class RevokeStaffSkillResponse
{
    [Required]
    public IReadOnlyList<AllocationSummaryDto> AffectedAllocations { get; set; } = [];
}
