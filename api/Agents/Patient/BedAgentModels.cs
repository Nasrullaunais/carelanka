using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Patient;
using WardEntity = CareLanka.Api.Data.Entities.Patient.Ward;

namespace CareLanka.Api.Agents.Patient;

/// <summary>Who the identifier resolved to, and their open visit if they have one.</summary>
public sealed record ResolvedPatient(
    Guid PatientId,
    string PatientCode,
    string FullName,
    Gender Gender,
    DateOnly? DateOfBirth,
    Guid? OpenAdmissionId);

/// <summary>
/// Everything the agent is allowed to know about what this visit needs. Read from the database,
/// never from the request body - a caller who could supply the care level could lie about it.
/// </summary>
public sealed record AdmissionRequirements(
    Guid AdmissionId,
    Guid PatientId,
    string PatientCode,
    string FullName,
    Gender Gender,
    DateOnly? DateOfBirth,
    AdmissionCategory Category,
    AdmissionUrgency Urgency,
    AdmissionStatus Status,
    bool IsInfectious,
    DateTimeOffset? ExpectedArrivalAt);

/// <summary>
/// A bed that is free, usable and in an active ward. It has not been checked against the patient
/// yet - that is the FILTER step, and keeping the two apart is what lets the agent say which rule
/// stopped it rather than "none available".
/// </summary>
/// <remarks>
/// It carries the ward row and the register row whole, rather than copies of the fields, so the
/// agent can hand them straight to <c>BedPlacementRules.EnsurePlaceable</c> - the same method the
/// nurse's manual pick runs. The agent does not get its own rulebook.
/// </remarks>
public sealed record CandidateBed(RegisteredBed Bed, WardEntity Ward)
{
    public Guid BedId => Bed.Id;

    public string BedNumber => Bed.BedNumber;

    public Guid WardId => Ward.Id;

    public string WardName => Ward.Name;

    public WardType WardType => Ward.WardType;
}

/// <summary>Load per ward, for soft rule S1.</summary>
public sealed record WardLoad(Guid WardId, string WardName, int TotalBeds, int FreeBeds)
{
    /// <summary>A ward with no beds reads as full, so an empty one never outranks a real one.</summary>
    public double OccupancyRatio => TotalBeds == 0
        ? 1
        : (double)(TotalBeds - FreeBeds) / TotalBeds;
}

/// <summary>A bed that passed every hard rule, with what the ranking needs.</summary>
public sealed record PlaceableBed(
    CandidateBed Bed,
    bool IsDowngrade,
    bool RequiresDutyManager,
    IReadOnlyList<string> RulesSatisfied,
    double OccupancyRatio,
    bool PatientWasHereBefore)
{
    public string? Rationale { get; init; }
}

/// <summary>Why one bed was dropped, so the blocker sentence can name the wall that was hit.</summary>
public sealed record RejectedBed(CandidateBed Bed, BedSuggestionBlockerCode Reason);
