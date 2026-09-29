using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Data.Entities.Common;

public class StaffMember : SoftDeletableEntity
{
    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string? Department { get; set; }

    public StaffRole Role { get; set; }

    /// <summary>Salutation. Only asked for on a doctor today, but not tied to role at the data
    /// level, since other roles may want it later.</summary>
    public PersonTitle? Title { get; set; }

    /// <summary>A doctor's clinical field, e.g. "Dermatologist". Required for role Doctor.</summary>
    public string? Specialization { get; set; }

    /// <summary>A doctor's medical registration number (SLMC or equivalent). Required for role
    /// Doctor, unique among active staff.</summary>
    public string? RegistrationNumber { get; set; }

    /// <summary>The date this person starts at the hospital. Required for role Doctor.</summary>
    public DateOnly? JoiningDate { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public string FullName => $"{FirstName} {LastName}";
}
