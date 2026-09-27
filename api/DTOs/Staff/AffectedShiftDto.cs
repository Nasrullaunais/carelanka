using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public class AffectedShiftDto
{
    [Required]
    public ShiftSummaryDto Shift { get; set; } = null!;

    [Required]
    public ShiftCoverageDto CoverageIfApproved { get; set; } = null!;
}
