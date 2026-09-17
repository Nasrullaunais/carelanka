using System.Text.Json.Serialization;

namespace CareLanka.Api.DTOs.Equipment;

public sealed class RejectPrescriptionRequest
{
    [JsonRequired]
    public string Reason { get; set; } = string.Empty;
}
