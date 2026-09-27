using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public class ShiftSummaryDto : ShiftDto
{
    [Required]
    public ShiftCoverageDto Coverage { get; set; } = new();
}
