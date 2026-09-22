namespace CareLanka.Api.Agents.Emergency;

public sealed record EligibleAmbulanceCandidate(
    Guid Id, string RegistrationNumber, decimal Latitude, decimal Longitude);

public sealed record DivertibleDispatchCandidate(
    Guid DispatchId,
    Guid AmbulanceId,
    string AmbulanceRegistration,
    Guid CallId,
    Data.Enums.CallPriority CallPriority,
    string? CallAddressLabel,
    Data.Enums.DispatchStatus Status,
    DateTimeOffset DispatchedAt);

/// <summary>
/// Allow-listed, read-only. No tool here writes to any table - a dispatch only exists after a
/// human confirms or approves through the proposal API.
/// </summary>
public interface IDispatchAgentTools
{
    Task<IReadOnlyList<EligibleAmbulanceCandidate>> ListEligibleAmbulancesAsync(
        IReadOnlyCollection<Guid> excludeAmbulanceIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DivertibleDispatchCandidate>> GetActiveDispatchesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, int?>> GetRouteMinutesAsync(
        IReadOnlyCollection<EligibleAmbulanceCandidate> ambulances,
        decimal destinationLatitude,
        decimal destinationLongitude,
        CancellationToken cancellationToken = default);
}
