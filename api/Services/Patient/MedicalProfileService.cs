using CareLanka.Api.Data;
using CareLanka.Api.Data.Configurations.Patient;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using MedicalProfileEntity = CareLanka.Api.Data.Entities.Patient.PatientMedicalProfile;
using MedicalProfileResponse = CareLanka.Api.DTOs.Patient.PatientMedicalProfile;

namespace CareLanka.Api.Services.Patient;

public sealed class MedicalProfileService : IMedicalProfileService
{
    private readonly CareLankaDbContext _db;
    private readonly IPatientService _patients;
    private readonly ICurrentUser _currentUser;

    public MedicalProfileService(
        CareLankaDbContext db, IPatientService patients, ICurrentUser currentUser)
    {
        _db = db;
        _patients = patients;
        _currentUser = currentUser;
    }

    public async Task<MedicalProfileResponse> GetAsync(Guid patientId, CancellationToken ct = default)
    {
        await _patients.GetByIdAsync(patientId, ct);

        var profile = await _db.PatientMedicalProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PatientId == patientId, ct);

        // No row is the ordinary state of a patient nobody has written a profile for, so this is
        // an empty 200 rather than a 404. A 404 here would read as "no such patient".
        if (profile is null)
        {
            return new MedicalProfileResponse { PatientId = patientId };
        }

        return await ToResponseAsync(profile, ct);
    }

    public async Task<MedicalProfileResponse> ReplaceAsync(
        Guid patientId, UpdateMedicalProfileRequest request, CancellationToken ct = default)
    {
        await _patients.GetByIdAsync(patientId, ct);

        var profile = await _db.PatientMedicalProfiles
            .FirstOrDefaultAsync(p => p.PatientId == patientId, ct);

        if (profile is null)
        {
            profile = new MedicalProfileEntity { PatientId = patientId };
            _db.PatientMedicalProfiles.Add(profile);
        }

        Apply(request, profile);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(exception, PatientMedicalProfileConfiguration.PatientUniqueIndex))
        {
            // Two first-time saves for one patient raced. The unique index is what stops the
            // second one, so the loser re-reads the row the winner wrote and replaces it, which
            // is what a full replace means anyway.
            _db.Entry(profile).State = EntityState.Detached;

            profile = await _db.PatientMedicalProfiles
                .FirstAsync(p => p.PatientId == patientId, ct);

            Apply(request, profile);

            await _db.SaveChangesAsync(ct);
        }

        return await ToResponseAsync(profile, ct);
    }

    private void Apply(UpdateMedicalProfileRequest request, MedicalProfileEntity profile)
    {
        profile.KnownConditions = Clean(request.KnownConditions);
        profile.Allergies = Clean(request.Allergies);
        profile.CurrentSymptoms = Clean(request.CurrentSymptoms);
        profile.RecentSituation = Clean(request.RecentSituation);
        profile.UpdatedByStaffMemberId = _currentUser.Id;
    }

    private async Task<MedicalProfileResponse> ToResponseAsync(
        MedicalProfileEntity profile, CancellationToken ct)
    {
        var names = await StaffNames.ByIdAsync(_db, [profile.UpdatedByStaffMemberId], ct);

        return new MedicalProfileResponse
        {
            PatientId = profile.PatientId,
            KnownConditions = profile.KnownConditions,
            Allergies = profile.Allergies,
            CurrentSymptoms = profile.CurrentSymptoms,
            RecentSituation = profile.RecentSituation,
            UpdatedByStaffId = profile.UpdatedByStaffMemberId,
            UpdatedByStaffName = StaffNames.Lookup(names, profile.UpdatedByStaffMemberId),
            UpdatedAt = profile.UpdatedAt
        };
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsUniqueViolation(DbUpdateException exception, string constraintName)
        => exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        } postgres && postgres.ConstraintName == constraintName;
}
