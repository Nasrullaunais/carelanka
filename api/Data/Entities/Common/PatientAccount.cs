namespace CareLanka.Api.Data.Entities.Common;

/// <summary>
/// A patient's login, created by a person signing up. Logs in with a phone number.
/// <para>
/// <strong>An account is not a medical record.</strong> A <c>Patient</c> row is a record,
/// created by staff, and it exists whether or not that person ever installs the app — an
/// unconscious arrival certainly has not. An account exists whether or not its owner has
/// ever been treated here.
/// </para>
/// <para>
/// The two are joined by <c>Patient.UserAccountId</c>, set deliberately by staff through
/// <c>POST /patients/{id}/link-account</c> after checking identity. It is never inferred
/// from a matching phone number: two people share a phone far more often than a hospital
/// would like.
/// </para>
/// </summary>
public class PatientAccount : SoftDeletableEntity
{
    /// <summary>The login identifier. Unique among active accounts.</summary>
    public string PhoneNumber { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public DateTimeOffset? LastLoginAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
