using CareLanka.Api.Services.Patient;

namespace CareLanka.Api.Services.Equipment;

public sealed class PatientDirectoryAdapter : IPatientDirectory
{
    private readonly IPatientService _patients;

    public PatientDirectoryAdapter(IPatientService patients) => _patients = patients;

    public async Task<bool> ExistsAsync(Guid patientId, CancellationToken cancellationToken = default)
        => await _patients.FindByIdAsync(patientId, cancellationToken) is not null;

    public async Task<Guid?> FindPatientIdForAccountAsync(
        Guid accountId, CancellationToken cancellationToken = default)
        => (await _patients.FindByUserAccountIdAsync(accountId, cancellationToken))?.Id;

    public async Task<IReadOnlyDictionary<Guid, PatientSummaryName>> GetSummariesAsync(
        IReadOnlyCollection<Guid> patientIds, CancellationToken cancellationToken = default)
    {
        var summaries = new Dictionary<Guid, PatientSummaryName>();

        foreach (var id in patientIds)
        {
            if (await _patients.FindByIdAsync(id, cancellationToken) is { } patient)
            {
                summaries[id] = new PatientSummaryName(patient.PatientCode, patient.FullName);
            }
        }

        return summaries;
    }
}
