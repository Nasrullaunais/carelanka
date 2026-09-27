using System.Text.Json.Serialization;

namespace CareLanka.Api.DTOs.Equipment;

public sealed class UpdateReorderThresholdRequest
{
    [JsonRequired]
    public int ReorderThreshold { get; set; }
}
