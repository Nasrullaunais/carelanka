using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Staff;

public class WardStaffingRuleInput
{
    [Required]
    public StaffRole RequiredRole { get; set; }

    public Guid? RequiredSkillId { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int MinimumHeadcount { get; set; }
}
