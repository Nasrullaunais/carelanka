using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Staff;

public class StaffSummaryDto
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public StaffRole Role { get; set; }

    public string? Department { get; set; }

    [Required]
    public bool IsActive { get; set; }

    [Required]
    public int SkillCount { get; set; }
}
