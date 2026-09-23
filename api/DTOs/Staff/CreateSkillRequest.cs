using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public class CreateSkillRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
}
