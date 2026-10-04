using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class CloseRunAtSceneRequest
{
    public EmergencyCallOutcome? Outcome { get; set; }
    public string? Notes { get; set; }
}
