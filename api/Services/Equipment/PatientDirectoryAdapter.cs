using CareLanka.Api.Services.Patient;

namespace CareLanka.Api.Services.Equipment;

public sealed class PatientDirectoryAdapter : IPatientDirectory
{
    private readonly IPatientService _patients;

    public PatientDirectoryAdapter(IPatientService patients) => _patients = patients;

    public async Task<bool> ExistsAsync(Guid patientId, CancellationToken cancellationToken = default)
        => await _patients.FindByIdAsync(patientId, cancellationToken) is not null;
}
