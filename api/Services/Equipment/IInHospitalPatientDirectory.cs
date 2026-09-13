using CareLanka.Api.DTOs.Equipment;

namespace CareLanka.Api.Services.Equipment;

/// <summary>
/// Who is in the hospital right now, with the ward they are in. What a laboratory needs in order
/// to work through a round of specimens without typing an identifier for each one.
/// </summary>
/// <remarks>
/// A port, like <see cref="IBedOccupancyPort"/> and <see cref="IPatientDirectory"/>. It reads
/// through Patient Management's own admission service - Equipment never queries their tables and
/// never writes them.
///
/// It exists as a port rather than a direct call for the reason the other two do: when M4
/// publishes the `wardId` filter on `GET /admissions` that `STUBS.md` already calls unblocked,
/// this is the one file that changes and the filtering below goes away.
/// </remarks>
public interface IInHospitalPatientDirectory
{
    /// <param name="wardName">Null for every ward, including visits holding no bed at all.</param>
    Task<IReadOnlyList<LabPatient>> ListAsync(
        string? wardName, CancellationToken cancellationToken = default);
}
