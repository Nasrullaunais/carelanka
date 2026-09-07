using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Common;

/// <summary>
/// Body of <c>POST /api/auth/patient/register</c>.
/// <para>
/// This creates a <strong>login</strong>, not a medical record. There is deliberately no
/// clinical field here — no date of birth, no national id, no next of kin. Staff create
/// the <c>Patient</c> record and link it later after checking identity.
/// </para>
/// </summary>
public class PatientRegisterRequest
{
    [Required]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;
}
