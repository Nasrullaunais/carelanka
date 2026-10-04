using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class FleetMap
{
    public IReadOnlyList<FleetMapAmbulance> Ambulances { get; set; } = [];
    public IReadOnlyList<FleetMapCall> Calls { get; set; } = [];
    public int LocationMaxAgeMinutes { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
}

public sealed class FleetMapAmbulance
{
    public Guid Id { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public AmbulanceStatus Status { get; set; }
    public string? OutOfServiceReason { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public DateTimeOffset? LocationUpdatedAt { get; set; }
    public bool LocationIsStale { get; set; }
    public int CurrentCrewCount { get; set; }
    public int RequiredCrewCount { get; set; }
    public bool IsEligible { get; set; }
    public IReadOnlyList<AmbulanceEligibilityBlockReason> EligibilityBlockReasons { get; set; } = [];
    public Guid? ActiveDispatchId { get; set; }
    public DispatchStatus? ActiveDispatchStatus { get; set; }
    public Guid? ActiveCallId { get; set; }
}

public sealed class FleetMapCall
{
    public Guid Id { get; set; }
    public CallPriority Priority { get; set; }
    public CallStatus Status { get; set; }
    public string? AddressLabel { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int WaitingMinutes { get; set; }
    public Guid? AssignedAmbulanceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
