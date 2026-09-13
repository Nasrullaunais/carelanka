using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class CreateAmbulanceRequest
{
    [Required]
    [MaxLength(20)]
    public string RegistrationNumber { get; set; } = string.Empty;

    [Range(-90, 90)]
    public decimal? CurrentLatitude { get; set; }

    [Range(-180, 180)]
    public decimal? CurrentLongitude { get; set; }
}
