using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class AmbulanceSummary
{
    [Required]
    public Guid Id { get; set; }
    [Required]
    public string RegistrationNumber { get; set; } = string.Empty;
    [Required]
    public AmbulanceStatus Status { get; set; }
    public decimal? CurrentLatitude { get; set; }
    public decimal? CurrentLongitude { get; set; }
    public DateTimeOffset? LocationUpdatedAt { get; set; }
    public int CurrentCrewCount { get; set; }
    public int RequiredCrewCount { get; set; }
    public bool IsEligible { get; set; }
    public IReadOnlyList<AmbulanceEligibilityBlockReason> EligibilityBlockReasons { get; set; } = [];
    public Guid? ActiveDispatchId { get; set; }
    [Required]
    public bool IsDivertible { get; set; }
    public double? DistanceKm { get; set; }
}
