namespace CareLanka.Api.Data.Entities.Common;

public class PatientAccount : SoftDeletableEntity
{
    /// <summary>
    /// What the patient signs in with, stored lower-case so one person cannot
    /// hold two accounts that differ only in capitals.
    /// </summary>
    public string Username { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public DateTimeOffset? LastLoginAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
