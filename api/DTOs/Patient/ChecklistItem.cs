using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

public class ChecklistItem
{
    [Required]
    public bool Ticked { get; set; }

    public Guid? TickedByStaffId { get; set; }

    public string? TickedByStaffName { get; set; }

    public DateTimeOffset? TickedAt { get; set; }

    [Required]
    public bool Mandatory { get; set; }
}
