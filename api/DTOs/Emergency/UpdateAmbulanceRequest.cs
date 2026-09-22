using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class UpdateAmbulanceRequest
{
    public AmbulanceStatus? Status { get; set; }

    public string? RegistrationNumber { get; set; }

    public string? OutOfServiceReason { get; set; }
}
