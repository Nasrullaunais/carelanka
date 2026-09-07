using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Common;

/// <summary>
/// An internal operator: nurse, doctor, ambulance crew, manager. Logs in with an email.
/// <para>
/// A patient is not a <see cref="StaffMember"/> and never gets a row here — their login is
/// a <see cref="PatientAccount"/>, and their medical record is a separate <c>Patient</c>
/// row owned by Patient Management.
/// </para>
/// </summary>
public class StaffMember : SoftDeletableEntity
{
    public string Email { get; set; } = null!;

    /// <summary>
    /// PBKDF2, produced by ASP.NET Core's <c>PasswordHasher</c>. Never on a DTO, never in a
    /// log line, never in a response body.
    /// </summary>
    public string PasswordHash { get; set; } = null!;

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string? Department { get; set; }

    public StaffRole Role { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public string FullName => $"{FirstName} {LastName}";
}
