using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class CancelEmergencyCallRequest
{
    public EmergencyCallOutcome? Outcome { get; set; }
    public string? Notes { get; set; }
}
