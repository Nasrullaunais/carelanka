using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Equipment;

public class CreateBedRequest
{
    [Required]
    public Guid WardId { get; set; }

    [Required]
    [MaxLength(20)]
    public string BedNumber { get; set; } = string.Empty;

    public bool HasIsolation { get; set; }

    [Range(1, int.MaxValue)]
    public int NurseStationDistance { get; set; } = 1;

    [MaxLength(50)]
    public string? AssetTag { get; set; }
}
