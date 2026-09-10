using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>Body of POST /api/appointments/{id}/check-in.</summary>
/// <remarks>
/// The care level is chosen here, at the desk, by the staff member checking the patient in —
/// not by the patient when they booked and not by an agent. Same rule every other admission
/// path follows, and <c>category_set_by_staff_id</c> is the recorded proof a human decided it.
/// </remarks>
public class CheckInRequest
{
    // The two enums below are nullable, which looks like a mistake and is not. [Required] on a
    // plain enum always passes: the binder has already turned an absent key into the first
    // declared member, so there is nothing left for validation to object to.

    /// <summary>
    /// Required. An omitted key would check the patient in as an ICU admission, because `icu`
    /// is declared first — the most expensive care level in the hospital, from a missing field.
    /// </summary>
    [Required]
    [EnumDataType(typeof(AdmissionCategory))]
    public AdmissionCategory? AdmissionCategory { get; set; }

    /// <summary>The staff member who chose the care level at the desk. Recorded proof a human decided it.</summary>
    [Required]
    public Guid CategorySetByStaffId { get; set; }

    /// <summary>Required. An omitted key would make every check-in `routine`, because it is declared first.</summary>
    [Required]
    [EnumDataType(typeof(AdmissionUrgency))]
    public AdmissionUrgency? Urgency { get; set; }

    /// <summary>Set by staff. Forces an isolation-capable bed once the bed agent runs.</summary>
    [DefaultValue(false)]
    public bool IsInfectious { get; set; }
}
