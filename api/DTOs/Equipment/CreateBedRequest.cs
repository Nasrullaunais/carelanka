using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

/// <summary>Body of POST /api/beds.</summary>
public class CreateBedRequest
{
    /// <summary>References Patient Management's Ward table. We store the reference and never write that table.</summary>
    [Required]
    public Guid WardId { get; set; }

    [Required]
    [MaxLength(20)]
    public string BedNumber { get; set; } = string.Empty;

    public bool HasIsolation { get; set; }

    /// <summary>1 is closest to the nurse station.</summary>
    [Range(1, int.MaxValue)]
    public int NurseStationDistance { get; set; } = 1;

    [MaxLength(50)]
    public string? AssetTag { get; set; }
}
