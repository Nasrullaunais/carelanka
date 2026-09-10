using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>Body of POST /api/admissions. Creates a visit in status <c>awaiting_bed</c>.</summary>
public class CreateAdmissionRequest
{
    [Required]
    public Guid PatientId { get; set; }

    [Required]
    [EnumDataType(typeof(AdmissionSource))]
    public AdmissionSource Source { get; set; }

    /// <summary>Emergency Service's reference. Required when source is <c>emergency</c>.</summary>
    [MaxLength(64)]
    public string? DispatchId { get; set; }

    [Required]
    [EnumDataType(typeof(AdmissionCategory))]
    public AdmissionCategory AdmissionCategory { get; set; }

    /// <summary>
    /// The clinician who chose the category. Required on purpose: it is the recorded proof that
    /// a human chose the care level, and there is no code path in this API that lets an agent
    /// supply it.
    /// </summary>
    [Required]
    public Guid CategorySetByStaffId { get; set; }

    [Required]
    [EnumDataType(typeof(AdmissionUrgency))]
    public AdmissionUrgency Urgency { get; set; }

    /// <summary>Set by staff. Forces an isolation-capable bed when the bed agent runs.</summary>
    [DefaultValue(false)]
    public bool IsInfectious { get; set; }

    public DateTimeOffset? ExpectedArrival { get; set; }
}
