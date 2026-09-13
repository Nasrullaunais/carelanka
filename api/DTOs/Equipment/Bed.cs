using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Equipment;

public class Bed
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid WardId { get; set; }

    [Required]
    public string WardName { get; set; } = string.Empty;

    [Required]
    public string BedNumber { get; set; } = string.Empty;

    [Required]
    public bool HasIsolation { get; set; }

    [Required]
    public int NurseStationDistance { get; set; }

    [Required]
    public BedCondition Condition { get; set; }

    public string? AssetTag { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }
}
