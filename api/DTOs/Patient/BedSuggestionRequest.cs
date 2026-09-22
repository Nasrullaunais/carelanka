namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Exactly one of <see cref="AdmissionId"/> or <see cref="PatientIdentifier"/>.
/// <para>
/// Note what is not here: the care level. A caller who could supply it could lie about it, and
/// that is the one field this agent must never influence - it is read from the database through
/// a read-only tool.
/// </para>
/// </summary>
public sealed class BedSuggestionRequest
{
    public Guid? AdmissionId { get; set; }

    public string? PatientIdentifier { get; set; }
}
