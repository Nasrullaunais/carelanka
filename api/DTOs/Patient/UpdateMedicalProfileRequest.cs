namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// A full replace, not a patch. With four free-text fields a partial update cannot tell "clear
/// this field" from "I did not send it", and a clinical note kept alive by an omitted key is the
/// wrong way round - so omitting a field clears it.
/// </summary>
public class UpdateMedicalProfileRequest
{
    public string? KnownConditions { get; set; }

    public string? Allergies { get; set; }

    public string? CurrentSymptoms { get; set; }

    public string? RecentSituation { get; set; }
}
