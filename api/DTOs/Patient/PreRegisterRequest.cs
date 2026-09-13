using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class PreRegisterRequest
{
    [Required]
    [MaxLength(20)]
    public string Nic { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = null!;

    [Required]
    [EnumDataType(typeof(Gender))]
    public Gender? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(200)]
    public string? EmergencyContactName { get; set; }

    [MaxLength(20)]
    public string? EmergencyContactPhone { get; set; }
}
