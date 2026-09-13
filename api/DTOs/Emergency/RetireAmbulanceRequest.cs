using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class RetireAmbulanceRequest
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}
