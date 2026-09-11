using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>One tickable box on the discharge checklist.</summary>
public class ChecklistItem
{
    /// <summary>Derived from <c>ticked_at</c>, never stored separately, so the two cannot disagree.</summary>
    [Required]
    public bool Ticked { get; set; }

    public Guid? TickedByStaffId { get; set; }

    /// <summary>Their name, for a screen. Sent beside the id, never instead of it.</summary>
    public string? TickedByStaffName { get; set; }

    public DateTimeOffset? TickedAt { get; set; }

    /// <summary>A non-mandatory item can stay unticked without blocking the discharge.</summary>
    [Required]
    public bool Mandatory { get; set; }
}
