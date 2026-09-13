using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Equipment;

public class LabPatient
{
    [Required]
    public Guid PatientId { get; set; }

    [Required]
    public string PatientCode { get; set; } = string.Empty;

    [Required]
    public string FullName { get; set; } = string.Empty;

    // Null for a visit holding no bed. An outpatient in for a blood test is still a patient the
    // lab files against.
    public string? WardName { get; set; }

    public string? BedNumber { get; set; }

    [Required]
    public AdmissionStatus AdmissionStatus { get; set; }
}
