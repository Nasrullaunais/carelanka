using System.Text.Json.Serialization;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class CreateAmbulanceRequest
{
    [JsonRequired]
    public string RegistrationNumber { get; set; } = string.Empty;

    public decimal? CurrentLatitude { get; set; }

    public decimal? CurrentLongitude { get; set; }
}
