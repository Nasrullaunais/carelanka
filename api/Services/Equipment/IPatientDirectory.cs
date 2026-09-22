namespace CareLanka.Api.Services.Equipment;

// A port, like IBedOccupancyPort: it keeps Patient Management's entity out of this component.
// Equipment stores the id and asks yes or no.
public interface IPatientDirectory
{
    Task<bool> ExistsAsync(Guid patientId, CancellationToken cancellationToken = default);

    /// <summary>The hospital record a patient login is linked to, or null before it is linked.</summary>
    Task<Guid?> FindPatientIdForAccountAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Code and name only - enough to hand medicine to the right person.</summary>
    Task<IReadOnlyDictionary<Guid, PatientSummaryName>> GetSummariesAsync(
        IReadOnlyCollection<Guid> patientIds, CancellationToken cancellationToken = default);
}

public sealed record PatientSummaryName(string PatientCode, string FullName);
