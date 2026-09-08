namespace CareLanka.Api.Data.Entities.Common;

public class PatientAccount : SoftDeletableEntity
{
    public string PhoneNumber { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public DateTimeOffset? LastLoginAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
