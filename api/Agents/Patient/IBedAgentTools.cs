using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Patient;
using PatientEntity = CareLanka.Api.Data.Entities.Patient.Patient;
using WardEntity = CareLanka.Api.Data.Entities.Patient.Ward;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// The allow-list as a type. A tool is a typed C# method, so the agent cannot call one that does
/// not exist or pass it a wrongly shaped argument - the compiler refuses first.
/// </summary>
public interface IBedAgentTools
{
    Task<PatientLookup?> FindPatientAsync(string identifier, CancellationToken ct = default);

    Task<AdmissionRequirements?> GetAdmissionRequirementsAsync(
        Guid admissionId, CancellationToken ct = default);

    Task<IReadOnlyList<CandidateBed>> ListAvailableBedsAsync(
        WardType? wardType, CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, WardLoad>> GetWardOccupancyAsync(CancellationToken ct = default);

    Task<IReadOnlyCollection<Guid>> ListPreviousWardsAsync(
        Guid patientId, CancellationToken ct = default);

    Task<PatientNotes> GetPatientNotesAsync(Guid patientId, CancellationToken ct = default);
}

/// <summary>
/// What a clinician typed about this patient, read straight off their medical profile. Four free
/// text fields and nothing derived: this is the only thing in the run that a rule cannot weigh,
/// which is why it is the only thing handed to a model.
/// </summary>
public sealed record PatientNotes(
    string? KnownConditions,
    string? Allergies,
    string? CurrentSymptoms,
    string? RecentSituation)
{
    public static readonly PatientNotes None = new(null, null, null, null);

    public bool IsEmpty
        => string.IsNullOrWhiteSpace(KnownConditions)
            && string.IsNullOrWhiteSpace(Allergies)
            && string.IsNullOrWhiteSpace(CurrentSymptoms)
            && string.IsNullOrWhiteSpace(RecentSituation);
}

public sealed record PatientLookup(
    PatientEntity Patient,
    Data.Entities.Patient.Admission? OpenAdmission);

public sealed record AdmissionRequirements(
    Guid AdmissionId,
    Guid PatientId,
    AdmissionCategory Category,
    Gender Gender,
    DateOnly? DateOfBirth,
    bool IsInfectious,
    AdmissionUrgency Urgency,
    AdmissionStatus Status,
    DateTimeOffset? ExpectedArrivalAt);

public sealed record CandidateBed(RegisteredBed Bed, WardEntity Ward);

public sealed record WardLoad(Guid WardId, string Name, int TotalBeds, int UsableBeds, int ClaimedBeds)
{
    /// <summary>
    /// Nought to one. A ward with no usable beds counts as full rather than as a divide by zero.
    /// </summary>
    public double Load => UsableBeds == 0 ? 1d : Math.Min(1d, (double)ClaimedBeds / UsableBeds);
}
