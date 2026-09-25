namespace CareLanka.Api.Data.Entities.Patient;

/// <summary>
/// Three free-text fields a clinician typed, one row per patient. Every character in it is
/// staff-authored - nothing here is a conclusion this system reached, which is why recording
/// that a patient is asthmatic does not put diagnosis inside the project's scope.
/// This is what the Patient Care Advisory Agent reads; before it existed that agent had nothing
/// to reason over but demographics.
/// </summary>
public class PatientMedicalProfile : AuditedEntity
{
    public Guid PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public string? KnownConditions { get; set; }

    /// <summary>
    /// Read deterministically by rule CR5, which rejects any agent draft naming a substance
    /// recorded here before a reviewer ever sees it. That check is only possible because this is
    /// a stored field rather than a sentence buried in a note.
    /// </summary>
    public string? Allergies { get; set; }

    public string? CurrentSymptoms { get; set; }

    public Guid UpdatedByStaffMemberId { get; set; }
}
