using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public class GrantStaffSkillRequest
{
    [Required]
    public Guid SkillId { get; set; }

    public DateOnly? ValidFrom { get; set; }

    public DateOnly? ExpiresAt { get; set; }
}
