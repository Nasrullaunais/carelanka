using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Staff;

public class WardStaffingRuleDto
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid WardId { get; set; }

    [Required]
    public StaffRole RequiredRole { get; set; }

    public Guid? RequiredSkillId { get; set; }

    public string? RequiredSkillName { get; set; }

    [Required]
    public int MinimumHeadcount { get; set; }
}
