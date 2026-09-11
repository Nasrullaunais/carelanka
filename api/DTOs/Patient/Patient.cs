using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>A patient record as the API publishes it. The spec builds this from PatientSummary + AuditFields, so this inherits rather than repeating the identity fields.</summary>
public class Patient : PatientSummary
{
    public string? Phone { get; set; }

    public string? Address { get; set; }

    public string? EmergencyContactName { get; set; }

    public string? EmergencyContactPhone { get; set; }

    /// <summary>
    /// Whether an optional patient login is linked. Most records never have one — a walk-in is
    /// a medical record, not a user. The account id itself is not published: it belongs to
    /// common auth, and no screen in this component has a use for it.
    /// </summary>
    [Required]
    public bool HasAccount { get; set; }

    // Not [Required]: created_at and updated_at come from the group-owned AuditFields schema,
    // which lists no required members in any of the five specs. Same reasoning as Ward.
    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
