using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

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

    [Required]
    public int TotalBeds { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
