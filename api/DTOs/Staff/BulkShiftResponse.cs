using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public class BulkShiftResponse
{
    [Required]
    public int Created { get; set; }

    [Required]
    public int Skipped { get; set; }

    [Required]
    public List<ShiftSummaryDto> Shifts { get; set; } = new();
}
