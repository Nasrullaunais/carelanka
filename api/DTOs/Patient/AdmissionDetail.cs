using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// One admission with its bed history. Nothing is deleted or overwritten, so a rejected or
/// expired assignment stays on the list — this is the audit trail, not the current state.
/// </summary>
/// <remarks>
/// The spec also publishes <c>workflows</c> and <c>discharge</c> on this schema. Neither is
/// served yet and both are omitted rather than returned empty: <c>AgentWorkflow</c> is common
/// and unbuilt (ADR 3), and the discharge checklist is step 7 of build/patient.md. A field
/// with no table behind it is left out, not faked — see STUBS.md.
/// </remarks>
public class AdmissionDetail : Admission
{
    /// <summary>Every assignment ever made, including rejected and expired ones. Empty until step 6 puts beds behind it.</summary>
    [Required]
    public IReadOnlyList<BedAssignment> BedAssignments { get; set; } = Array.Empty<BedAssignment>();
}
