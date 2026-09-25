using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public class StaffMemberDetailDto : StaffMemberDto
{
    [Required]
    public IReadOnlyList<StaffSkillDto> Skills { get; set; } = Array.Empty<StaffSkillDto>();

    [Required]
    public IReadOnlyList<AllocationSummaryDto> UpcomingAllocations { get; set; } = Array.Empty<AllocationSummaryDto>();

    public double? LeaveBalanceDays { get; set; }
}
