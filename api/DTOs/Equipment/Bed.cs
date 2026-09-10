using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Equipment;

/// <summary>A bed frame as Equipment Management publishes it. The writable source of truth Patient Management reads and never writes.</summary>
public class Bed
{
    [Required]
    public Guid Id { get; set; }

    /// <summary>Patient Management's Ward.</summary>
    [Required]
    public Guid WardId { get; set; }

    /// <summary>Read from Patient Management, not stored here. Stubbed until GET /wards exists — see STUBS.md row 2.</summary>
    [Required]
    public string WardName { get; set; } = string.Empty;

    [Required]
    public string BedNumber { get; set; } = string.Empty;

    [Required]
    public bool HasIsolation { get; set; }

    /// <summary>1 is closest to the nurse station.</summary>
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
