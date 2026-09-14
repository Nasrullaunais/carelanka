using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class CreateEmergencyCallRequest
{
    public bool? PatientIsCaller { get; set; }
    public Guid? PatientId { get; set; }
    public string? CallerName { get; set; }
    public string? CallerPhone { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? LocationAccuracyMetres { get; set; }
    public DateTimeOffset? LocationCapturedAt { get; set; }
    public Guid? IdempotencyKey { get; set; }
    public string? Details { get; set; }
    public CallPriority? Priority { get; set; }
}
