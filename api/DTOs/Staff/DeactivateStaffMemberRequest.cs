using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public class DeactivateStaffMemberRequest
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    public DateOnly? EffectiveDate { get; set; }
}
