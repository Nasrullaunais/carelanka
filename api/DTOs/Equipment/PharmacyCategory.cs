namespace CareLanka.Api.DTOs.Equipment;

/// <summary>One of the pharmacy categories. Seeded with the five from the component plan.</summary>
public class PharmacyCategory
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Whether dispensing anything in this category needs a doctor's prescription. Recorded here; the clinical decision is not ours.</summary>
    public bool RequiresPrescription { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
