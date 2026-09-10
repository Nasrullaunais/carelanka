using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Free bed counts across every ward. Read by Emergency Service (Member 1) to choose a
/// destination — counts only, no patient data.
/// </summary>
public class WardCapacitySummary
{
    /// <summary>
    /// When this was counted. On the wire because free beds go stale in seconds: a dispatcher
    /// acting on a number needs to know how old it is.
    /// </summary>
    [Required]
    public DateTimeOffset GeneratedAt { get; set; }

    [Required]
    public IReadOnlyList<WardCapacity> Wards { get; set; } = Array.Empty<WardCapacity>();
}

/// <summary>
/// One ward's line in the capacity summary. Gender policy is on it because a male-only ward
/// with two free beds is no use to a female patient, and the caller has to be able to see that.
/// </summary>
public class WardCapacity
{
    [Required]
    public Guid WardId { get; set; }

    [Required]
    public string Name { get; set; } = null!;

    [Required]
    public WardType WardType { get; set; }

    [Required]
    public GenderPolicy GenderPolicy { get; set; }

    [Required]
    public int TotalBeds { get; set; }

    /// <summary>
    /// Usable, unoccupied, and not under a live hold. A hold past its expiry counts as free.
    /// That expiry rule lives here, in the owning service, so no other component
    /// re-implements it differently.
    /// </summary>
    [Required]
    public int FreeBeds { get; set; }
}
