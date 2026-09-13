using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class PatientSummary
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    [StringLength(8, MinimumLength = 8)]
    public string PatientCode { get; set; } = null!;

    [Required]
    public string FullName { get; set; } = null!;

    public string? Nic { get; set; }

    public string? TempReference { get; set; }

    [Required]
    public Gender Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }
}
