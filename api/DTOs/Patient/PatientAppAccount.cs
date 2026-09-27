using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

public class PatientAppAccount
{
    [Required]
    public Guid PatientId { get; set; }

    [Required]
    [StringLength(8, MinimumLength = 8)]
    public string PatientCode { get; set; } = null!;

    [Required]
    public string FullName { get; set; } = null!;

    public string? Nic { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [Required]
    public string Username { get; set; } = null!;
}
