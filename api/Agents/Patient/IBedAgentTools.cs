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
