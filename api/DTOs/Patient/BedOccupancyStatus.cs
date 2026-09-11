using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// The answer to "may this bed be taken out of service?", read by Equipment Management before
/// they withdraw a bed for repair.
/// </summary>
/// <remarks>
/// They own the frame and we own the occupant, so only we can answer this — occupancy is the
/// presence of one of our BedAssignment rows, not a column on their bed.
///
/// The rule it exists to enforce: **maintenance never evicts a patient.** If the answer is
/// occupied, Equipment waits for discharge.
/// </remarks>
public class BedOccupancyStatus
{
    [Required]
    public Guid BedId { get; set; }

    /// <summary>True while a live assignment exists. A hold past its expiry does not count.</summary>
    [Required]
    public bool Occupied { get; set; }

    /// <summary>
    /// <c>reserved</c> or <c>occupied</c> for a live claim, and null when there is none.
    /// Never <c>released</c> — a released row is history and claims nothing.
    /// </summary>
    public AssignmentStatus? AssignmentStatus { get; set; }

    /// <summary>When the hold lapses, if the bed is held rather than lived in.</summary>
    public DateTimeOffset? ReservedUntil { get; set; }

    /// <summary>
    /// The direct answer, so Equipment does not have to re-derive it. False while a patient is
    /// in the bed or a live hold stands.
    /// </summary>
    [Required]
    public bool MayTakeOutOfService { get; set; }
}
