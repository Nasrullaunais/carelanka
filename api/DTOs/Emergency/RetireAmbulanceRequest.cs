using System.Text.Json.Serialization;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class RetireAmbulanceRequest
{
    [JsonRequired]
    public string Reason { get; set; } = string.Empty;
}
