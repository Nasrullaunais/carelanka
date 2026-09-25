using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public class LeaveRequestDetailDto : LeaveRequestDto
{
    [Required]
    public IReadOnlyList<AffectedShiftDto> AffectedShifts { get; set; } = Array.Empty<AffectedShiftDto>();
}
