using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Staff;

public class CreateRosterProposalRequest
{
    [Required]
    public Guid ShiftId { get; set; }

    [MaxLength(500)]
    public string? Objective { get; set; }

    public bool AllowCascadingSwap { get; set; } = true;
}
