using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Staff;

public class BulkShiftPatternItem
{
    [Required]
    public string StartTime { get; set; } = string.Empty;

    [Required]
    public string EndTime { get; set; } = string.Empty;

    [Required]
    public StaffRole RequiredRole { get; set; }

    public Guid? RequiredSkillId { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int HeadcountNeeded { get; set; }

    [Range(1, int.MaxValue)]
    public int? MinimumHeadcount { get; set; }
}
