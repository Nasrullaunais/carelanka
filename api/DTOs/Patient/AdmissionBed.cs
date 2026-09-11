using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// A bed as this component sees it: Equipment Management's frame, with our answer to whether
/// anyone is in it.
/// </summary>
/// <remarks>
/// <c>id</c>, <c>ward_id</c>, <c>bed_number</c>, <c>has_isolation</c> and <c>condition</c>
/// are read from their register and never written here. <c>ward_name</c> is ours. <c>availability</c> and
/// <c>occupied_by_admission_id</c> are computed from our own BedAssignment rows.
///
/// Named <c>AdmissionBed</c> and not <c>Bed</c> because equipment-spec.yaml already publishes
/// a <c>Bed</c>, and one app publishes one document — two shapes cannot share a schema name.
/// </remarks>
public class AdmissionBed
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid WardId { get; set; }

    [Required]
    public string WardName { get; set; } = null!;

    [Required]
    public string BedNumber { get; set; } = null!;

    /// <summary>Side room or curtained isolation. Hard rule H4: an infectious patient needs one.</summary>
    [Required]
    public bool HasIsolation { get; set; }

    [Required]
    public BedCondition Condition { get; set; }

    /// <summary>Computed. A hold past its <c>reserved_until</c> reports free.</summary>
    [Required]
    public BedAvailability Availability { get; set; }

    /// <summary>
    /// The visit holding or occupying this bed, and null when nothing is. Set for a hold as
    /// well as an occupancy: a bed board needs to show who is coming, not only who is here,
    /// and <c>availability</c> already says which of the two this is.
    /// </summary>
    public Guid? OccupiedByAdmissionId { get; set; }

    // Not [Required]: created_at and updated_at come from the group-owned AuditFields schema,
    // which publishes neither as required. Both are Equipment's — the frame's own history.
    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
