using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// How full one ward is and what kind of care the people in it need. Read by Staff
/// Management (Member 2) to work out staffing demand — counts only, no patient identities.
/// </summary>
public class WardOccupancy
{
    [Required]
    public Guid WardId { get; set; }

    [Required]
    public string Name { get; set; } = null!;

    [Required]
    public WardType WardType { get; set; }

    /// <summary>Every bed standing in the ward today, counted from Equipment's register.</summary>
    [Required]
    public int TotalBeds { get; set; }

    /// <summary>Beds with a patient actually in them.</summary>
    [Required]
    public int OccupiedBeds { get; set; }

    /// <summary>Beds under a hold that has not lapsed. A hold past its expiry is not counted here.</summary>
    [Required]
    public int ReservedBeds { get; set; }

    /// <summary>Beds Equipment has withdrawn for repair or servicing.</summary>
    [Required]
    public int OutOfServiceBeds { get; set; }

    /// <summary>
    /// The care mix, keyed by the wire value of AdmissionCategory. Fifteen routine inpatients
    /// and two high-dependency patients need very different staffing, even though both are
    /// "seventeen patients".
    /// </summary>
    /// <remarks>
    /// Every category is present, zeros included. A category that vanished from the object
    /// when it dropped to zero would disappear from a chart rather than fall to the floor of
    /// it, and would make every reader write the same `?? 0`.
    /// </remarks>
    [Required]
    public IReadOnlyDictionary<string, int> PatientsByCategory { get; set; } =
        new Dictionary<string, int>();

    /// <summary>
    /// People holding a bed here who have not walked in yet, so Staff can staff ahead of a
    /// rush instead of reacting to one.
    /// </summary>
    /// <remarks>
    /// Named on the wire by hand, because the snake_case policy does not break before a digit:
    /// <c>IncomingNext2h</c> serialises as <c>incoming_next2h</c>, and the contract Staff
    /// Management generate against says <c>incoming_next_2h</c>. The only property in this
    /// component with a number in it, and the contract test is what found it.
    /// </remarks>
    [Required]
    [JsonPropertyName("incoming_next_2h")]
    public int IncomingNext2h { get; set; }
}
