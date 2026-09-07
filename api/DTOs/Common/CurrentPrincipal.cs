using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Common;

/// <summary>
/// Who the caller is. Returned by <c>GET /api/auth/me</c> and embedded in every
/// <see cref="AuthTokens"/>, so a client knows which navigation to render the moment it
/// signs in.
/// </summary>
public class CurrentPrincipal
{
    /// <summary>A <c>StaffMember.Id</c> or a <c>PatientAccount.Id</c>, per <see cref="PrincipalType"/>.</summary>
    public Guid Id { get; set; }

    public PrincipalType PrincipalType { get; set; }

    public PrincipalRole Role { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Staff only. Null for a patient account.</summary>
    public string? Email { get; set; }

    /// <summary>Patient accounts only. Null for staff.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// The linked medical record, if staff have linked one.
    /// <para>
    /// <strong>Null is the ordinary state, not an error.</strong> It is null for every
    /// staff member, and null for a patient who has signed up but has never been treated
    /// here. A Flutter screen that assumes it is non-null crashes on the first real user.
    /// </para>
    /// Always null until Patient Management ships
    /// <c>POST /patients/{id}/link-account</c>.
    /// </summary>
    public Guid? PatientId { get; set; }
}
