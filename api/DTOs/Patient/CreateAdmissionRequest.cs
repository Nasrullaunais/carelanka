using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>Body of POST /api/admissions. Creates a visit in status <c>awaiting_bed</c>.</summary>
public class CreateAdmissionRequest
{
    [Required]
    public Guid PatientId { get; set; }

    // The three enums below are nullable, which looks like a mistake and is not. [Required] on a
    // plain enum always passes: the model binder has already turned an absent key into the first
    // declared member, so there is nothing left for validation to object to. Making them nullable
    // is what turns a missing key into a 400 instead of a silent default. Each one is spelled out
    // on the property, because the default each would have taken is a different kind of wrong.

    /// <summary>How the patient got here. Required: an omitted key would file a walk-in as an emergency.</summary>
    [Required]
    [EnumDataType(typeof(AdmissionSource))]
    public AdmissionSource? Source { get; set; }

    /// <summary>Emergency Service's reference. Required when source is <c>emergency</c>.</summary>
    [MaxLength(64)]
    public string? DispatchId { get; set; }

    /// <summary>The care level. Set by clinical staff, never by an agent.</summary>
    /// <remarks>
    /// The most important of the three to get a 400 rather than a default. `icu` is declared
    /// first because the enum is ordered most to least intensive for the downgrade ladder, so a
    /// body missing this key would file the patient at the most acute care level in the hospital
    /// - and it is the input to hard rule H2, so the bed agent would then reason from it.
    /// </remarks>
    [Required]
    [EnumDataType(typeof(AdmissionCategory))]
    public AdmissionCategory? AdmissionCategory { get; set; }

    /// <summary>
    /// The clinician who chose the category. Required on purpose: it is the recorded proof that
    /// a human chose the care level, and there is no code path in this API that lets an agent
    /// supply it.
    /// </summary>
    [Required]
    public Guid CategorySetByStaffId { get; set; }

    /// <summary>How urgent. Feeds soft rule S2, so a default would quietly reorder the worklist.</summary>
    [Required]
    [EnumDataType(typeof(AdmissionUrgency))]
    public AdmissionUrgency? Urgency { get; set; }

    /// <summary>Set by staff. Forces an isolation-capable bed when the bed agent runs.</summary>
    [DefaultValue(false)]
    public bool IsInfectious { get; set; }

    public DateTimeOffset? ExpectedArrival { get; set; }
}
