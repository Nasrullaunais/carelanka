using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public class CreateAllocationRequest
{
    [Required]
    public Guid ShiftId { get; set; }

    [Required]
    public Guid StaffMemberId { get; set; }

    public bool Override { get; set; } = false;

    [MaxLength(500)]
    public string? OverrideReason { get; set; }
}