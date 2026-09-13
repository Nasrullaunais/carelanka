using CareLanka.Api.Services.Patient;

namespace CareLanka.Api.Services.Equipment;

/// <summary>
/// Answers "does this patient exist?" from Patient Management's own service. Equipment never
/// reads their table directly and never writes it at all.
/// </summary>
/// <remarks>
/// The same shape as <see cref="BedOccupancyAdapter"/>: one file in the consuming component
/// that knows the other component exists, so widening what Equipment asks for is a change to
/// this file rather than a search through the whole track.
/// </remarks>
public sealed class PatientDirectoryAdapter : IPatientDirectory
{
    private readonly IPatientService _patients;

    public PatientDirectoryAdapter(IPatientService patients) => _patients = patients;

    public async Task<bool> ExistsAsync(Guid patientId, CancellationToken cancellationToken = default)
        => await _patients.FindByIdAsync(patientId, cancellationToken) is not null;
}
