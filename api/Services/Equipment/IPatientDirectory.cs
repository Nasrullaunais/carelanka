namespace CareLanka.Api.Services.Equipment;

/// <summary>
/// The one question Equipment asks about a patient: does this id exist? Filing a lab report
/// against a patient who does not is how a result goes missing - nothing errors, and the ward
/// waits for a report that is sitting under a typo.
/// </summary>
/// <remarks>
/// A port, like <see cref="IBedOccupancyPort"/>, for the same reason: it keeps Patient
/// Management's entity out of this component. Equipment stores the id and asks yes or no. It
/// never holds a name, a NIC or a ward, so there is no copy here to go stale when they change
/// one of theirs.
/// </remarks>
public interface IPatientDirectory
{
    Task<bool> ExistsAsync(Guid patientId, CancellationToken cancellationToken = default);
}
