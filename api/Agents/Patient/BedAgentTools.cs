using System.Text.RegularExpressions;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Patient;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Agents.Patient;

public sealed partial class BedAgentTools : IBedAgentTools
{
    private static readonly AdmissionStatus[] ClosedStatuses =
    [
        AdmissionStatus.Discharged,
        AdmissionStatus.Cancelled
    ];

    private readonly CareLankaDbContext _db;
    private readonly IBedRegistryService _beds;
    private readonly TimeProvider _time;

    public BedAgentTools(CareLankaDbContext db, IBedRegistryService beds, TimeProvider time)
    {
        _db = db;
        _beds = beds;
        _time = time;
    }

    public async Task<ResolvedPatient?> FindPatientAsync(
        string identifier, CancellationToken ct = default)
    {
        var trimmed = identifier.Trim();

        if (trimmed.Length == 0)
        {
            return null;
        }

        var query = _db.Patients.AsNoTracking();

        // Matched case-insensitively both ways. A code is generated upper-case and an NIC ends in
        // V or X, and somebody reading a slip at a desk types whichever way their keyboard was.
        query = PatientCodePattern().IsMatch(trimmed)
            ? query.Where(patient => EF.Functions.ILike(patient.PatientCode, trimmed))
            : query.Where(patient => patient.Nic != null
                && EF.Functions.ILike(patient.Nic, trimmed));

        var found = await query
            .Select(patient => new
            {
                patient.Id,
                patient.PatientCode,
                patient.FullName,
                patient.Gender,
                patient.DateOfBirth
            })
            .FirstOrDefaultAsync(ct);

        if (found is null)
        {
            return null;
        }

        var openAdmissionId = await _db.Admissions
            .AsNoTracking()
            .Where(admission => admission.PatientId == found.Id)
            .Where(admission => !ClosedStatuses.Contains(admission.Status))
            .OrderByDescending(admission => admission.CreatedAt)
            .Select(admission => (Guid?)admission.Id)
            .FirstOrDefaultAsync(ct);

        return new ResolvedPatient(
            found.Id,
            found.PatientCode,
            found.FullName,
            found.Gender,
            found.DateOfBirth,
            openAdmissionId);
    }

    public async Task<AdmissionRequirements?> GetAdmissionRequirementsAsync(
        Guid admissionId, CancellationToken ct = default)
        => await _db.Admissions
            .AsNoTracking()
            .Where(admission => admission.Id == admissionId)
            .Select(admission => new AdmissionRequirements(
                admission.Id,
                admission.PatientId,
                admission.Patient.PatientCode,
                admission.Patient.FullName,
                admission.Patient.Gender,
                admission.Patient.DateOfBirth,
                admission.Category,
                admission.Urgency,
                admission.Status,
                admission.IsInfectious,
                admission.ExpectedArrivalAt))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<CandidateBed>> ListAvailableBedsAsync(
        WardType? wardType = null, CancellationToken ct = default)
    {
        var now = _time.GetUtcNow();

        var wardQuery = _db.Wards.AsNoTracking();

        if (wardType is { } onlyType)
        {
            wardQuery = wardQuery.Where(ward => ward.WardType == onlyType);
        }

        // The global query filter already hides retired wards, which is H5: a bed whose ward is
        // gone never reaches the candidate list at all.
        var wards = await wardQuery.ToListAsync(ct);

        var beds = await _beds.ListBedsInWardsAsync(wards.Select(ward => ward.Id).ToList(), ct);

        var claimedBedIds = await ClaimedBedIdsAsync(
            beds.Select(bed => bed.Id).ToList(), now, ct);

        var wardsById = wards.ToDictionary(ward => ward.Id);

        return beds
            .Where(bed => bed.Condition == BedCondition.Usable)
            .Where(bed => !claimedBedIds.Contains(bed.Id))
            .Where(bed => wardsById.ContainsKey(bed.WardId))
            .Select(bed => new CandidateBed(bed, wardsById[bed.WardId]))
            .OrderBy(bed => bed.WardName)
            .ThenBy(bed => bed.BedNumber)
            .ToList();
    }

    public async Task<IReadOnlyList<WardLoad>> GetWardOccupancyAsync(CancellationToken ct = default)
    {
        var now = _time.GetUtcNow();

        var wards = await _db.Wards
            .AsNoTracking()
            .Select(ward => new { ward.Id, ward.Name })
            .ToListAsync(ct);

        var beds = await _beds.ListBedsInWardsAsync(wards.Select(ward => ward.Id).ToList(), ct);

        var claimedBedIds = await ClaimedBedIdsAsync(beds.Select(bed => bed.Id).ToList(), now, ct);

        var bedsByWard = beds
            .GroupBy(bed => bed.WardId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return wards
            .Select(ward =>
            {
                var wardBeds = bedsByWard.TryGetValue(ward.Id, out var found) ? found : [];

                return new WardLoad(
                    ward.Id,
                    ward.Name,
                    wardBeds.Count,
                    wardBeds.Count(bed => bed.Condition == BedCondition.Usable
                        && !claimedBedIds.Contains(bed.Id)));
            })
            .ToList();
    }

    public async Task<IReadOnlyCollection<Guid>> ListPreviousWardsAsync(
        Guid patientId, CancellationToken ct = default)
    {
        var bedIds = await _db.BedAssignments
            .AsNoTracking()
            .Where(assignment => assignment.Admission.PatientId == patientId)
            .Select(assignment => assignment.BedId)
            .Distinct()
            .ToListAsync(ct);

        if (bedIds.Count == 0)
        {
            return [];
        }

        var beds = await _beds.ListBedsByIdAsync(bedIds, ct);

        return beds.Select(bed => bed.WardId).Distinct().ToList();
    }

    /// <summary>
    /// Beds a live assignment holds. <c>BedHold.LiveOn</c> applies the 30-minute expiry, so a hold
    /// that lapsed reads as free here - the same rule every other reader in this component uses,
    /// written down once.
    /// </summary>
    private async Task<HashSet<Guid>> ClaimedBedIdsAsync(
        IReadOnlyCollection<Guid> bedIds, DateTimeOffset now, CancellationToken ct)
    {
        if (bedIds.Count == 0)
        {
            return [];
        }

        var ids = bedIds.ToList();

        var claimed = await _db.BedAssignments
            .AsNoTracking()
            .Where(assignment => ids.Contains(assignment.BedId))
            .Where(BedHold.LiveOn(now))
            .Select(assignment => assignment.BedId)
            .Distinct()
            .ToListAsync(ct);

        return claimed.ToHashSet();
    }

    [GeneratedRegex(PatientIdentifierFormats.PatientCode, RegexOptions.IgnoreCase)]
    private static partial Regex PatientCodePattern();
}
