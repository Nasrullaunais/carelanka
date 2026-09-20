using System.Text.RegularExpressions;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.Services.Patient;
using Microsoft.EntityFrameworkCore;
using AdmissionEntity = CareLanka.Api.Data.Entities.Patient.Admission;
using PatientEntity = CareLanka.Api.Data.Entities.Patient.Patient;

namespace CareLanka.Api.Agents.Patient;

/// <summary>
/// The Bed and Patient Details Agent's entire allow-list. Four tools, all four read-only: there is
/// no tool here that admits a patient, reserves a bed, changes a care level or touches another
/// component's data, so the agent physically cannot perform an action whatever the model decides.
/// Least privilege is the tool list, not a sentence in a prompt.
/// </summary>
public sealed class BedAgentTools : IBedAgentTools
{
    public const string FindPatient = "find_patient";
    public const string GetAdmissionRequirements = "get_admission_requirements";
    public const string ListAvailableBeds = "list_available_beds";
    public const string GetWardOccupancy = "get_ward_occupancy";

    private static readonly Regex PatientCodeFormat =
        new(PatientIdentifierFormats.PatientCode, RegexOptions.Compiled);

    private static readonly AdmissionStatus[] OpenStatuses =
    [
        AdmissionStatus.AwaitingBed,
        AdmissionStatus.AwaitingApproval,
        AdmissionStatus.BedReserved,
        AdmissionStatus.Admitted,
        AdmissionStatus.ReadyForDischarge
    ];

    private readonly CareLankaDbContext _db;
    private readonly IBedRegistryService _beds;

    public BedAgentTools(CareLankaDbContext db, IBedRegistryService beds)
    {
        _db = db;
        _beds = beds;
    }

    /// <summary>
    /// An NIC or a patient code - the caller does not say which and the agent does not need to be
    /// told. No match is an answer, not an error.
    /// </summary>
    public async Task<PatientLookup?> FindPatientAsync(
        string identifier, CancellationToken ct = default)
    {
        var trimmed = (identifier ?? string.Empty).Trim();

        if (trimmed.Length is 0 or > 32)
        {
            throw new ArgumentException(
                "A patient identifier is between 1 and 32 characters.", nameof(identifier));
        }

        var code = trimmed.ToUpperInvariant();

        var patient = PatientCodeFormat.IsMatch(code)
            ? await _db.Patients.AsNoTracking()
                .FirstOrDefaultAsync(row => row.PatientCode == code, ct)
            : null;

        patient ??= await _db.Patients.AsNoTracking()
            .FirstOrDefaultAsync(row => row.Nic == trimmed, ct);

        if (patient is null)
        {
            return null;
        }

        var open = await _db.Admissions.AsNoTracking()
            .Where(admission => admission.PatientId == patient.Id)
            .Where(admission => OpenStatuses.Contains(admission.Status))
            .OrderByDescending(admission => admission.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return new PatientLookup(patient, open);
    }

    /// <summary>
    /// Care level, gender, date of birth, infectious flag, urgency and expected arrival - read from
    /// the database rather than taken from the caller, because the care level is the one field this
    /// agent must never influence.
    /// </summary>
    public async Task<AdmissionRequirements?> GetAdmissionRequirementsAsync(
        Guid admissionId, CancellationToken ct = default)
    {
        if (admissionId == Guid.Empty)
        {
            throw new ArgumentException("An admission id is required.", nameof(admissionId));
        }

        var admission = await _db.Admissions.AsNoTracking()
            .Include(row => row.Patient)
            .FirstOrDefaultAsync(row => row.Id == admissionId, ct);

        return admission is null ? null : Requirements(admission, admission.Patient);
    }

    public static AdmissionRequirements Requirements(
        AdmissionEntity admission, PatientEntity patient)
        => new(
            admission.Id,
            patient.Id,
            admission.Category,
            patient.Gender,
            patient.DateOfBirth,
            admission.IsInfectious,
            admission.Urgency,
            admission.Status,
            admission.ExpectedArrivalAt);

    /// <summary>
    /// Free, usable beds in wards that are still in service. Equipment's register joined with our
    /// own assignments, with hold expiry applied, so a lapsed hold reads as free here exactly as it
    /// does on the bed board.
    /// <para>
    /// The gender and isolation arguments the design gives this tool are deliberately not filters:
    /// a bed dropped inside SQL cannot be counted, and counting the drops is the only way a blocked
    /// run can say which wall it hit rather than a bare "none available". Those rules are applied
    /// one step later, by the one rulebook every path uses.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<CandidateBed>> ListAvailableBedsAsync(
        WardType? wardType, CancellationToken ct = default)
    {
        var wardQuery = _db.Wards.AsNoTracking();

        if (wardType is { } onlyType)
        {
            wardQuery = wardQuery.Where(ward => ward.WardType == onlyType);
        }

        var wards = await wardQuery.ToListAsync(ct);

        if (wards.Count == 0)
        {
            return Array.Empty<CandidateBed>();
        }

        var beds = await _beds.ListBedsInWardsAsync(wards.Select(ward => ward.Id).ToList(), ct);
        var taken = await ClaimedBedsAsync(beds.Select(bed => bed.Id).ToList(), ct);
        var byWard = wards.ToDictionary(ward => ward.Id);

        return beds
            .Where(bed => !taken.Contains(bed.Id))
            .Where(bed => bed.Condition == BedCondition.Usable)
            .Select(bed => new CandidateBed(bed, byWard[bed.WardId]))
            .OrderBy(candidate => candidate.Ward.Name)
            .ThenBy(candidate => candidate.Bed.BedNumber)
            .ToList();
    }

    /// <summary>
    /// Load per ward, for the balancing rule S1. Counts every live claim, so a ward whose beds are
    /// all held but not yet arrived reads as full - which it is, to anybody looking for one.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, WardLoad>> GetWardOccupancyAsync(
        CancellationToken ct = default)
    {
        var wards = await _db.Wards.AsNoTracking().ToListAsync(ct);
        var beds = await _beds.ListBedsInWardsAsync(wards.Select(ward => ward.Id).ToList(), ct);
        var taken = await ClaimedBedsAsync(beds.Select(bed => bed.Id).ToList(), ct);

        var bedsByWard = beds
            .GroupBy(bed => bed.WardId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return wards.ToDictionary(
            ward => ward.Id,
            ward =>
            {
                var wardBeds = bedsByWard.TryGetValue(ward.Id, out var found)
                    ? found
                    : new List<RegisteredBed>();

                return new WardLoad(
                    ward.Id,
                    ward.Name,
                    wardBeds.Count,
                    wardBeds.Count(bed => bed.Condition == BedCondition.Usable),
                    wardBeds.Count(bed => taken.Contains(bed.Id)));
            });
    }

    /// <summary>
    /// Every ward this patient has stayed in before, for the continuity rule S2.
    /// </summary>
    public async Task<IReadOnlyCollection<Guid>> ListPreviousWardsAsync(
        Guid patientId, CancellationToken ct = default)
    {
        var bedIds = await _db.BedAssignments.AsNoTracking()
            .Where(assignment => assignment.Admission.PatientId == patientId)
            .Select(assignment => assignment.BedId)
            .Distinct()
            .ToListAsync(ct);

        if (bedIds.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        var beds = await _beds.ListBedsByIdAsync(bedIds, ct);

        return beds.Select(bed => bed.WardId).Distinct().ToList();
    }

    private async Task<HashSet<Guid>> ClaimedBedsAsync(
        IReadOnlyCollection<Guid> bedIds, CancellationToken ct)
    {
        if (bedIds.Count == 0)
        {
            return [];
        }

        var ids = bedIds.ToList();
        var now = DateTimeOffset.UtcNow;

        var claimed = await _db.BedAssignments.AsNoTracking()
            .Where(assignment => ids.Contains(assignment.BedId))
            .Where(BedHold.LiveOn(now))
            .Select(assignment => assignment.BedId)
            .ToListAsync(ct);

        return claimed.ToHashSet();
    }
}
