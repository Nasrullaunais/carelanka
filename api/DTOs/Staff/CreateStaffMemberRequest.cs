using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Staff;

public class CreateStaffMemberRequest
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(12)]
    public string TemporaryPassword { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [Required]
    public StaffRole Role { get; set; }

    [MaxLength(100)]
    public string? Department { get; set; }

    public List<Guid>? SkillIds { get; set; }

    /// <summary>Salutation. Required when Role is Doctor.</summary>
    public PersonTitle? Title { get; set; }

    /// <summary>Clinical field, e.g. "Dermatologist". Required when Role is Doctor.</summary>
    [MaxLength(150)]
    public string? Specialization { get; set; }

    /// <summary>Medical registration number (SLMC or equivalent). Required when Role is Doctor,
    /// unique among active staff.</summary>
    [MaxLength(50)]
    public string? RegistrationNumber { get; set; }

    /// <summary>The date this person starts at the hospital. Required when Role is Doctor.</summary>
    public DateOnly? JoiningDate { get; set; }
}
