using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Body of POST /api/patients/{id}/link-account. A patient RECORD and a patient ACCOUNT are
/// different things; this attaches an optional login to a record staff already created.
/// </summary>
public class LinkPatientAccountRequest
{
    /// <summary>
    /// A PatientAccount.Id. Staff link it deliberately, after checking identity — never inferred
    /// from a matching phone number, because two people share a phone far more often than a
    /// hospital would like.
    /// </summary>
    [Required]
    public Guid UserAccountId { get; set; }
}
