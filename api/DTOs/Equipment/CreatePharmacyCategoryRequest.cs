using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

/// <summary>Body of POST /api/pharmacy-categories.</summary>
public class CreatePharmacyCategoryRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public bool RequiresPrescription { get; set; }
}
