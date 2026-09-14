using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class CheckInRequest
{

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
}
