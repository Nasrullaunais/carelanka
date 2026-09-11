using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>Pick a bed for an admission by hand, without the agent.</summary>
public class AssignBedRequest
{
    /// <summary>Equipment Management's bed id, from <c>GET /api/bed-availability</c>.</summary>
    /// <remarks>
    /// Nullable so that leaving it out is a 400. <c>[Required]</c> on a plain <c>Guid</c>
    /// always passes: the binder has already turned an absent key into <c>Guid.Empty</c>, so
    /// there is nothing left for validation to object to, and the request would get as far as
    /// a 404 for the all-zeroes bed. Same trap as every required enum in this component, and
    /// the published contract is unchanged - Swashbuckle still lists it in `required`.
    /// </remarks>
    [Required]
    public Guid? BedId { get; set; }

    /// <summary>
    /// Why a human ignored what the agent proposed. Optional, and nothing requires it yet:
    /// there are no proposals to override until the agent lands at step 11, and this endpoint
    /// is also the ordinary path when nobody asked the agent at all.
    /// </summary>
    [MaxLength(500)]
    public string? OverrideReason { get; set; }
}
