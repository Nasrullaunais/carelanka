using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Patient;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// The three read tools, each a plain query against tables this component already owns. Nothing
/// here writes, and nothing here reads another patient's data - every query is scoped by the id
/// the caller already resolved and guarded.
/// </summary>
public sealed class CareAgentTools : ICareAgentTools
{
    /// <summary>
    /// Enough for the model to see a pattern without the prompt growing without bound.
    /// </summary>
    private const int HistoryLimit = 5;

    private readonly CareLankaDbContext _db;
    private readonly IBedRegistryService _beds;

    public CareAgentTools(CareLankaDbContext db, IBedRegistryService beds)
    {
        _db = db;
        _beds = beds;
    }

    public async Task<CareMedicalProfileFacts?> GetMedicalProfileAsync(
        Guid patientId, CancellationToken ct = default)
    {
        var profile = await _db.PatientMedicalProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.PatientId == patientId, ct);

        return profile is null
            ? null
            : new CareMedicalProfileFacts(
                profile.KnownConditions,
                profile.Allergies,
                profile.CurrentSymptoms);
    }

    public async Task<CarePatientHistoryFacts> GetPatientHistoryAsync(
        Guid patientId, CancellationToken ct = default)
    {
        var patient = await _db.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == patientId, ct);

        var pastAdmissions = await _db.Admissions
            .AsNoTracking()
            .Where(admission => admission.PatientId == patientId)
            .OrderByDescending(admission => admission.CreatedAt)
            .Take(HistoryLimit)
            .Select(admission => new CarePastAdmission(
                admission.Category!.Value, admission.Urgency, admission.AdmittedAt))
            .ToListAsync(ct);

        var pastRecommendations = await _db.CareRecommendations
            .AsNoTracking()
            .Where(row => row.PatientId == patientId && row.Status != CareRecommendationStatus.PendingReview)
            .OrderByDescending(row => row.ReportedAt)
            .Take(HistoryLimit)
            .Select(row => new CarePastRecommendation(row.ReportedText, row.UrgencyFlag, row.ReportedAt))
            .ToListAsync(ct);

        return new CarePatientHistoryFacts(
            Age(patient?.DateOfBirth), patient?.Gender ?? Gender.Unknown, pastAdmissions, pastRecommendations);
    }

    public async Task<CareCurrentAdmissionFacts?> GetCurrentAdmissionAsync(
        Guid admissionId, CancellationToken ct = default)
    {
        var admission = await _db.Admissions
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == admissionId, ct);

        if (admission is null)
        {
            return null;
        }

        var labels = await BedLabels.LiveByAdmissionAsync(
            _db, _beds, [admissionId], DateTimeOffset.UtcNow, ct);

        var wardName = labels.TryGetValue(admissionId, out var label) && !string.IsNullOrEmpty(label.WardName)
            ? label.WardName
            : null;

        return new CareCurrentAdmissionFacts(
            admission.Category!.Value, admission.Urgency, admission.IsInfectious, wardName, admission.AdmittedAt);
    }

    private static int? Age(DateOnly? dateOfBirth)
    {
        if (dateOfBirth is not { } born)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - born.Year;

        return born > today.AddYears(-age) ? age - 1 : age;
    }
}
