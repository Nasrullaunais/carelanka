using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

/// <summary>One of the pharmacy categories. Seeded with the five from the component plan.</summary>
public class PharmacyCategory
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether dispensing anything in this category needs a doctor's prescription. Recorded here; the clinical decision is not ours.</summary>
    [Required]
    public bool RequiresPrescription { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }
}
