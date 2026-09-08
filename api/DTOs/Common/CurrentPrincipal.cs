using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Common;

/// <summary>Who the caller is. Returned by GET /api/auth/me and embedded in every sign-in response.</summary>
public class CurrentPrincipal
{
    /// <summary>A StaffMember.Id or a PatientAccount.Id, per PrincipalType.</summary>
    [Required]
    public Guid Id { get; set; }

    [Required]
    public PrincipalType PrincipalType { get; set; }

    [Required]
    public PrincipalRole Role { get; set; }

    [Required]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Staff only. Null for a patient account.</summary>
    public string? Email { get; set; }

    /// <summary>Patient accounts only. Null for staff.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>The linked medical record, if staff have linked one. Null is the ordinary state, not an error — null for every staff member, and for a patient never treated here.</summary>
    public Guid? PatientId { get; set; }
}
