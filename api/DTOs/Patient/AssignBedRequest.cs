using System.Text.Json.Serialization;

namespace CareLanka.Api.DTOs.Patient;

public class AssignBedRequest
{
    [JsonRequired]
    public Guid BedId { get; set; }

    public string? OverrideReason { get; set; }
}
