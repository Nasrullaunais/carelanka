using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public class Ambulance
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public string RegistrationNumber { get; set; } = string.Empty;

    public decimal? CurrentLatitude { get; set; }
    public decimal? CurrentLongitude { get; set; }

    [Required]
    public AmbulanceStatus Status { get; set; }

    public string? OutOfServiceReason { get; set; }

    [Required]
    public bool IsActive { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }
}
