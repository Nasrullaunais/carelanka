using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Why a visit was called off. Always a human's claim, which is why the reason is mandatory:
/// a computer cannot know whether the ambulance was diverted, the patient died, or it is
/// simply stuck in traffic.
/// </summary>
public class CancelAdmissionRequest
{
    /// <summary>The closed vocabulary, so cancellations can be counted and reported on.</summary>
    /// <remarks>
    /// Nullable, unlike the enums on CreateAdmissionRequest, and deliberately. [Required] on a
    /// plain enum passes when the key is absent, because the binder has already turned a
    /// missing value into the first member - so leaving <c>reason</c> out would file every
    /// cancellation as <c>diverted_to_other_hospital</c>. Nullable makes an absent key a 400.
    /// </remarks>
    [Required]
    [EnumDataType(typeof(CancelReason))]
    public CancelReason? Reason { get; set; }

    /// <summary>
    /// The part no enum can carry - "diverted to Kandy, family informed". Optional, because
    /// forcing a sentence out of a nurse in a hurry produces "n/a" and nothing else.
    /// </summary>
    [MaxLength(500)]
    public string? Note { get; set; }
}
