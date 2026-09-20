using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class UpdateEmergencyCallRequest
{
    public CallPriority? Priority { get; set; }
    public string? Details { get; set; }
    public string? CallerName { get; set; }
    public string? CallerPhone { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}
