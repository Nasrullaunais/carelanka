using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

public class PharmacyCategory
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public bool RequiresPrescription { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }
}
