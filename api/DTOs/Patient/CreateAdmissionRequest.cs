using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class CreateAdmissionRequest
{
    [Required]
    public Guid PatientId { get; set; }

    [Required]
    [EnumDataType(typeof(AdmissionSource))]
    public AdmissionSource? Source { get; set; }

    [MaxLength(64)]
    public string? DispatchId { get; set; }

    [Required]
    [EnumDataType(typeof(AdmissionCategory))]
    public AdmissionCategory? AdmissionCategory { get; set; }

    [Required]
    public Guid CategorySetByStaffId { get; set; }

    [Required]
    [EnumDataType(typeof(AdmissionUrgency))]
    public AdmissionUrgency? Urgency { get; set; }

    [DefaultValue(false)]
    public bool IsInfectious { get; set; }

    public DateTimeOffset? ExpectedArrival { get; set; }
}
