namespace CareLanka.Api.Data.Entities.Common;

/// <summary>
/// Unified identity for every operator role, field and admin alike.
/// Patients are deliberately not StaffMembers — they authenticate through PatientAccount.
/// </summary>
public class StaffMember : SoftDeletableEntity
{
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Department { get; set; }
    public required StaffRole Role { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
