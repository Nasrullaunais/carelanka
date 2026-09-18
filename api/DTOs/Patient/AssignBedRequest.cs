using System.Text.Json.Serialization;

namespace CareLanka.Api.DTOs.Patient;

public class AssignBedRequest
{
    /// <summary>
    /// Nullable so that a missing key is a 400 rather than a 404 for the all-zeroes bed. A
    /// non-nullable Guid binds an absent key to <c>Guid.Empty</c>, which is a real-looking value
    /// the request then carries all the way to a lookup.
    /// </summary>
    /// <remarks>
    /// <c>[JsonRequired]</c> rather than <c>[Required]</c>: DataAnnotations are not the validation
    /// mechanism any more, but the published contract still lists <c>bed_id</c> as required and
    /// <c>JsonRequiredSchemaFilter</c> is what keeps it there. A missing key fails in the binder, a
    /// null one in the validator, and both are the same 400.
    /// </remarks>
    [JsonRequired]
    public Guid? BedId { get; set; }

    public string? OverrideReason { get; set; }

    /// <summary>
    /// The agent run that suggested this bed, when the nurse pressed a button on a suggestion
    /// rather than picking one by hand. Present means the assignment is stamped
    /// <c>assigned_by = agent</c> and linked to the run; absent means a person chose it unaided.
    /// </summary>
    /// <remarks>
    /// This is the only thing the bed agent adds to the write path. Everything else - the row
    /// lock, the hard rules, the role check, the 30-minute hold, the approver stamp - is the
    /// manual endpoint exactly as it has been since step 6.
    /// </remarks>
    public Guid? WorkflowId { get; set; }
}
