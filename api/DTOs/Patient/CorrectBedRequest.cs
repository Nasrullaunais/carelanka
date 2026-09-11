using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Move a patient out of the bed they were put in by mistake and into the right one.
/// </summary>
/// <remarks>
/// <b>This is for a wrong bed, not for a patient who moves.</b> The old assignment is closed as
/// <c>corrected</c> and bills nothing at all, on the grounds that it never really happened — so
/// using it to record a genuine mid-stay ward transfer would quietly give away a night's bed.
/// A real transfer needs its own path, and does not exist yet.
/// </remarks>
public class CorrectBedRequest
{
    /// <summary>The bed they should have been given, from <c>GET /api/bed-availability</c>.</summary>
    /// <remarks>
    /// Nullable so that leaving it out is a 400 rather than a 404 for the all-zeroes bed — the
    /// same trap <see cref="AssignBedRequest.BedId"/> documents.
    /// </remarks>
    [Required]
    public Guid? BedId { get; set; }

    /// <summary>What went wrong, for whoever reads the trail later.</summary>
    /// <remarks>
    /// Optional, and not a rule. A nurse fixing their own mis-click ten seconds later has
    /// nothing useful to write, and demanding a sentence there would train everybody to type
    /// "correction" — which is the field's own name and tells a reader nothing.
    /// </remarks>
    [MaxLength(500)]
    public string? Reason { get; set; }
}
