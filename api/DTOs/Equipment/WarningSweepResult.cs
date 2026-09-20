using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

// What one run of the warning sweep changed.
public class WarningSweepResult
{
    /// <summary>New warnings the sweep raised.</summary>
    [Required]
    public int Raised { get; set; }

    /// <summary>Warnings already open whose wording or severity moved on, such as fewer days left.</summary>
    [Required]
    public int Updated { get; set; }

    /// <summary>Warnings closed because the problem is gone: stock back up, batch used, service done.</summary>
    [Required]
    public int Resolved { get; set; }

    /// <summary>Every sweep warning still open or acknowledged after this run.</summary>
    [Required]
    public int StillOpen { get; set; }

    [Required]
    public DateTimeOffset RanAt { get; set; }
}
