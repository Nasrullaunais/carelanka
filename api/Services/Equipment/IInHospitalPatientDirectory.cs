using CareLanka.Api.DTOs.Equipment;

namespace CareLanka.Api.Services.Equipment;

// A port, like IBedOccupancyPort. It reads through Patient Management's admission service;
// Equipment never queries their tables.
public interface IInHospitalPatientDirectory
{
    /// <param name="wardName">Null for every ward, including visits holding no bed.</param>
    Task<IReadOnlyList<LabPatient>> ListAsync(
        string? wardName, CancellationToken cancellationToken = default);
}
