using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Staff;

public class ShiftDto
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid WardId { get; set; }

    [Required]
    public string WardName { get; set; } = string.Empty;

    [Required]
    public DateOnly Date { get; set; }

    [Required]
    public string StartTime { get; set; } = string.Empty;

    [Required]
    public string EndTime { get; set; } = string.Empty;

    [Required]
    public bool CrossesMidnight { get; set; }

    [Required]
    public StaffRole RequiredRole { get; set; }

    public Guid? RequiredSkillId { get; set; }

    public string? RequiredSkillName { get; set; }

    [Required]
    public int HeadcountNeeded { get; set; }

    [Required]
    public int MinimumHeadcount { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }
}
