using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// The "is this you?" step. Every field is masked: the caller has proved they hold a
/// patient code and a matching date of birth, which is not enough to be handed the
/// record itself. Enough for the patient to recognise, not enough for a stranger to use.
/// </summary>
public class PatientClaimPreview
{
    [Required]
    public string PatientCode { get; set; } = string.Empty;

    [Required]
    public string MaskedFullName { get; set; } = string.Empty;

    public string? MaskedPhone { get; set; }
}
