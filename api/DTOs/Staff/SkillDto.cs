using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public class SkillDto
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public int StaffCount { get; set; }
}
