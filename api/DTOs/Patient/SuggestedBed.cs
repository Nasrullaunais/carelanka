using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// One selectable bed. The best pick and every alternative use this same shape, on purpose:
/// alternatives used to be a bed id and a reason it lost, which a nurse could read and could not
/// act on. Every entry here is committed the same way, with the same one button.
/// </summary>
public class SuggestedBed
{
    [Required]
    public Guid BedId { get; set; }

    [Required]
    public string WardName { get; set; } = null!;

    [Required]
    public string BedNumber { get; set; } = null!;

    [Required]
    public bool IsDowngrade { get; set; }

    /// <summary>
    /// True for a downgrade or a ward more acute than the patient's care level. The button is
    /// hidden for a ward nurse - the real check runs again at <c>assign-bed</c>.
    /// </summary>
    [Required]
    public bool RequiresDutyManager { get; set; }

    public List<string> RulesSatisfied { get; set; } = [];

    /// <summary>Short human-readable summary. Not the model's raw reasoning.</summary>
    public string? Rationale { get; set; }
}
