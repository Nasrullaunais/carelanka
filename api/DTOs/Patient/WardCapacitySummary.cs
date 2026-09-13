using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

public class WardCapacitySummary
{
    [Required]
    public DateTimeOffset GeneratedAt { get; set; }

    [Required]
    public IReadOnlyList<WardCapacity> Wards { get; set; } = Array.Empty<WardCapacity>();
}

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

    [Required]
    public int FreeBeds { get; set; }
}
