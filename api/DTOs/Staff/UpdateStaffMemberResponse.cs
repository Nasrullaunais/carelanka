using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public class UpdateStaffMemberResponse
{
    [Required]
    public StaffMemberDto StaffMember { get; set; } = null!;

    [Required]
    public IReadOnlyList<AllocationSummaryDto> AffectedAllocations { get; set; } = Array.Empty<AllocationSummaryDto>();
}
