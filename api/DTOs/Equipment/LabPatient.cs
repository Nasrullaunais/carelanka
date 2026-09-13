using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Equipment;

/// <summary>
/// One patient the laboratory can file a result against, as the lab needs to see them: by where
/// they are, not by an identifier somebody has to type.
/// </summary>
/// <remarks>
/// Everything here is already visible to this role through <c>GET /patients/{id}</c>, which
/// publishes a patient's admissions with the ward name on them. This shape exists so a lab
/// working through a rack of specimens can read a ward down a screen instead of opening one
/// patient at a time.
/// </remarks>
public class LabPatient
{
    [Required]
    public Guid PatientId { get; set; }

    [Required]
    public string PatientCode { get; set; } = string.Empty;

    [Required]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Null for a visit holding no bed - an outpatient in for a blood test is still a patient the lab files against.</summary>
    public string? WardName { get; set; }

    public string? BedNumber { get; set; }

    /// <summary>Where the visit has got to. A specimen can be taken before a bed is found, so this is not always `admitted`.</summary>
    [Required]
    public AdmissionStatus AdmissionStatus { get; set; }
}
