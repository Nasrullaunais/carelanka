using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class UpdateAmbulanceRequest
{
    public AmbulanceStatus? Status { get; set; }

    [MaxLength(20)]
    public string? RegistrationNumber { get; set; }

    [MaxLength(500)]
    public string? OutOfServiceReason { get; set; }
}
