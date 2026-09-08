using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>A ward as the API publishes it. Every member is always present, so the generated clients type none of them as nullable.</summary>
public class Ward
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public string Name { get; set; } = null!;

    [Required]
    public WardType WardType { get; set; }

    [Required]
    public GenderPolicy GenderPolicy { get; set; }

    [Required]
    public bool IsActive { get; set; }

    /// <summary>Counted from the bed register, never stored. Two sources of truth would drift.</summary>
    [Required]
    public int TotalBeds { get; set; }

    // Not [Required]: created_at and updated_at come from the group-owned AuditFields schema,
    // which lists no required members in any of the five specs. Marking them here alone would
    // make this component's generated client disagree with the other three.
    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
