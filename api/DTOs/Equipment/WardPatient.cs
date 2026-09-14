using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Equipment;

// One current hospital visit, as the two screens that pick a patient need to see it: by where
// they are lying rather than by an identifier somebody has to type.
public class WardPatient
{
    // What an equipment assignment points at. A lab report points at PatientId instead, because
    // a result belongs to the person and not to the visit.
    [Required]
    public Guid AdmissionId { get; set; }

    [Required]
    public Guid PatientId { get; set; }

    [Required]
    public string PatientCode { get; set; } = string.Empty;

    [Required]
    public string FullName { get; set; } = string.Empty;

    // Null for a visit holding no bed. An outpatient in for a blood test is still somebody the
    // lab files against, though not somebody a ventilator is assigned to.
    public string? WardName { get; set; }

    public string? BedNumber { get; set; }

    [Required]
    public AdmissionStatus AdmissionStatus { get; set; }
}
