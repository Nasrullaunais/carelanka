using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// One admission with its bed history. Nothing is deleted or overwritten, so a rejected or
/// expired assignment stays on the list — this is the audit trail, not the current state.
/// </summary>
/// <remarks>
/// The spec also publishes <c>workflows</c> on this schema. It is not served yet and is omitted
/// rather than returned empty: <c>AgentWorkflow</c> is common and unbuilt (ADR 3). A field with
/// no table behind it is left out, not faked — see STUBS.md.
/// </remarks>
public class AdmissionDetail : Admission
{
    /// <summary>Every assignment ever made, including rejected and expired ones.</summary>
    [Required]
    public IReadOnlyList<BedAssignment> BedAssignments { get; set; } = Array.Empty<BedAssignment>();

    /// <summary>
    /// The discharge checklist, or null when nobody has opened one for this visit. Null is the
    /// ordinary state of a patient who has just arrived, not a gap in the data.
    /// </summary>
    public Discharge? Discharge { get; set; }

    /// <summary>
    /// What the visit costs, or null when nobody has prepared a bill yet. Read-only here; the
    /// bill is written through <c>/api/admissions/{id}/bill</c>.
    /// </summary>
    public Bill? Bill { get; set; }
}
