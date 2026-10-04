using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class MyCallTracking
{
    public Guid EmergencyCallId { get; set; }
    public CallStatus CallStatus { get; set; }
    public DispatchStatus? DispatchStatus { get; set; }
    public bool AmbulanceIsOnTheWay { get; set; }
    public bool LookingForAnotherAmbulance { get; set; }
    public string? AmbulanceRegistration { get; set; }
    public decimal? AmbulanceLatitude { get; set; }
    public decimal? AmbulanceLongitude { get; set; }
    public bool AmbulanceLocationIsStale { get; set; }
    public int? EstimatedMinutesToArrival { get; set; }
    public double? AmbulanceDistanceKm { get; set; }
    public CancellationRequestStatus? CancellationRequestStatus { get; set; }
    public string? CancellationReviewNotes { get; set; }
    public EmergencyCallOutcome? Outcome { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
