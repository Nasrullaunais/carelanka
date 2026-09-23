using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public class StaffSkillDto
{
    [Required]
    public Guid SkillId { get; set; }

    [Required]
    public string SkillName { get; set; } = string.Empty;

    public DateOnly? ValidFrom { get; set; }

    public DateOnly? ExpiresAt { get; set; }

    [Required]
    public bool IsValid { get; set; }

    [Required]
    public DateTimeOffset GrantedAt { get; set; }
}
