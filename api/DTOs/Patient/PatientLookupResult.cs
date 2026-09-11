using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// The answer to "have we seen this NIC before?". A miss is a 200 with found = false, not a
/// 404 — not finding someone is the normal outcome at a registration desk, not an error.
/// </summary>
public class PatientLookupResult
{
    [Required]
    public bool Found { get; set; }

    public PatientSummary? Patient { get; set; }

    /// <summary>
    /// True when this patient is already in the hospital. Registering a second concurrent
    /// admission is almost always a mistake, so the desk is told before it happens rather than
    /// blocked afterwards.
    /// </summary>
    [Required]
    public bool HasOpenAdmission { get; set; }
}
