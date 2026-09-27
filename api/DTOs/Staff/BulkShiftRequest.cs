using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public class BulkShiftRequest
{
    [Required]
    public Guid WardId { get; set; }

    [Required]
    public DateOnly From { get; set; }

    [Required]
    public DateOnly To { get; set; }

    public List<string>? Weekdays { get; set; }

    [Required]
    [MinLength(1)]
    public List<BulkShiftPatternItem> Patterns { get; set; } = new();
}
