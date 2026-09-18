namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Exactly one of <see cref="AdmissionId"/> or <see cref="PatientIdentifier"/>.
/// </summary>
/// <remarks>
/// Note what is not here: <c>admission_category</c>. A caller who could supply the care level
/// could lie about it, and that is the one field this agent must never influence. It is read from
/// the database through a read-only tool.
/// </remarks>
public sealed class BedSuggestionRequest
{
    /// <summary>From a row already on the patients board.</summary>
    public Guid? AdmissionId { get; set; }

    /// <summary>
    /// An NIC or a patient code, off the hospital slip. The caller does not say which and the
    /// agent does not need to be told - <c>PatientIdentifierFormats</c> already tells them apart.
    /// </summary>
    public string? PatientIdentifier { get; set; }
}
