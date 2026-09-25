using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using AdmissionEntity = CareLanka.Api.Data.Entities.Patient.Admission;
using ChecklistItemEntity = CareLanka.Api.Data.Entities.Patient.DischargeChecklistItem;
using DischargeEntity = CareLanka.Api.Data.Entities.Patient.Discharge;
using DischargeResponse = CareLanka.Api.DTOs.Patient.Discharge;
using PatientEntity = CareLanka.Api.Data.Entities.Patient.Patient;

namespace CareLanka.Api.Services.Patient;

public sealed class DischargeService : IDischargeService
{
    private static readonly IReadOnlyDictionary<DischargeChecklistItemType, bool> Mandatory =
        new Dictionary<DischargeChecklistItemType, bool>
        {
            [DischargeChecklistItemType.ClinicalClearance] = true,
            [DischargeChecklistItemType.BillingSettled] = true
        };

    private static readonly IReadOnlyDictionary<DischargeChecklistItemType, PrincipalRole> TickedBy =
        new Dictionary<DischargeChecklistItemType, PrincipalRole>
        {
            [DischargeChecklistItemType.ClinicalClearance] = PrincipalRole.Doctor
        };

    private static readonly AdmissionStatus[] OnTheWard =
        [AdmissionStatus.Admitted, AdmissionStatus.ReadyForDischarge];

    private static readonly AdmissionStatus[] OnTheWardOrGone =
        [AdmissionStatus.Admitted, AdmissionStatus.ReadyForDischarge, AdmissionStatus.Discharged];

    private readonly CareLankaDbContext _db;
    private readonly IBedRegistryService _beds;
    private readonly ICurrentUser _currentUser;

    public DischargeService(
        CareLankaDbContext db, IBedRegistryService beds, ICurrentUser currentUser)
    {
        _db = db;
        _beds = beds;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<DischargeCandidate>> ListCandidatesAsync(
        Guid? wardId, bool includeDischarged, int page, int pageSize, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var statuses = includeDischarged ? OnTheWardOrGone : OnTheWard;

        var query = _db.Admissions
            .AsNoTracking()
            .Include(admission => admission.Patient)
            .Include(admission => admission.BedAssignments)
            .Include(admission => admission.Discharge!)
                .ThenInclude(discharge => discharge.ChecklistItems)
            .Where(admission => statuses.Contains(admission.Status));

        if (wardId is { } onlyWard)
        {
            var bedIds = (await _beds.ListBedsInWardsAsync([onlyWard], ct))
                .Select(bed => bed.Id)
                .ToList();

            query = query.Where(admission => admission.BedAssignments
                .Any(assignment => bedIds.Contains(assignment.BedId)
                    && assignment.Status != AssignmentStatus.Released));
        }

        var admissions = await query.ToListAsync(ct);

        var labels = await BedLabels.CurrentOrLastByAdmissionAsync(
            _db, _beds, admissions.Select(admission => admission.Id).ToList(), now, ct);

        var candidates = admissions
            .Select(admission => ToCandidate(admission, labels, now))

            .OrderBy(candidate => candidate.IsDischarged)
            .ThenBy(candidate => candidate.OutstandingItems.Count)

            .ThenByDescending(candidate => candidate.DischargedAt ?? DateTimeOffset.MinValue)
            .ThenByDescending(candidate => candidate.DaysInBed)
            .ThenBy(candidate => candidate.Patient.FullName)
            .ToList();

        return PagedResult<DischargeCandidate>.From(
            candidates.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            page,
            pageSize,
            candidates.Count);
    }

    public async Task<DischargeResponse> UpdateChecklistAsync(
        Guid admissionId, ChecklistUpdateRequest request, CancellationToken ct = default)
    {
        if (request.BillingSettled is not null)
        {
            throw new ConflictException(MessageCode.BillingTickedBySettlingOnly);
        }

        var wanted = Requested(request);

        foreach (var (item, _) in wanted)
        {
            EnsureMayTick(item);
        }

        var admission = await LoadForWriteAsync(admissionId, ct);

        if (!OnTheWard.Contains(admission.Status))
        {
            throw new ConflictException(
                MessageCode.DischargeChecklistNotOnWard, EnumWire.ToWire(admission.Status));
        }

        var discharge = await EnsureDischargeAsync(admission, ct);

        foreach (var (item, ticked) in wanted)
        {
            Tick(discharge, item, ticked, _currentUser.Id);
        }

        ApplyFlag(admission, discharge);

        await _db.SaveChangesAsync(ct);

        return await ToResponseAsync(discharge, ct);
    }

    public async Task<DischargeResponse> ConfirmAsync(
        Guid admissionId, ConfirmDischargeRequest request, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        await _db.Database.ExecuteSqlAsync(
            $"SELECT id FROM admissions WHERE id = {admissionId} FOR UPDATE", ct);

        var admission = await LoadForWriteAsync(admissionId, ct);

        var discharge = await EnsureDischargeAsync(admission, ct);

        var outstanding = Outstanding(discharge);

        if (outstanding.Count > 0)
        {
            throw new ConflictException(
                MessageCode.DischargeChecklistIncomplete, string.Join(", ", outstanding));
        }

        AdmissionStatusMachine.EnsureMove(
            admission.Status, AdmissionStatus.Discharged, AdmissionStatus.ReadyForDischarge);

        var now = DateTimeOffset.UtcNow;

        admission.Status = AdmissionStatus.Discharged;
        admission.DischargedAt = now;

        discharge.ConfirmedByStaffMemberId = _currentUser.Id;
        discharge.ConfirmedAt = now;
        discharge.SummaryNote = Clean(request.SummaryNote);

        foreach (var live in admission.BedAssignments
            .Where(assignment => assignment.Status != AssignmentStatus.Released))
        {
            live.Status = AssignmentStatus.Released;
            live.ReservedUntil = null;
            live.ReleasedAt = now;
            live.ReleaseReason = ReleaseReason.Discharged;
        }

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await ToResponseAsync(discharge, ct);
    }

    public async Task<DischargeResponse?> FindForAdmissionAsync(
        Guid admissionId, CancellationToken ct = default)
    {
        var discharge = await _db.Discharges
            .AsNoTracking()
            .Include(row => row.ChecklistItems)
            .FirstOrDefaultAsync(row => row.AdmissionId == admissionId, ct);

        return discharge is null ? null : await ToResponseAsync(discharge, ct);
    }

    public async Task MarkBillingSettledAsync(
        Guid admissionId, bool settled, CancellationToken ct = default)
    {
        var admission = await LoadForWriteAsync(admissionId, ct);
        var discharge = await EnsureDischargeAsync(admission, ct);

        Tick(discharge, DischargeChecklistItemType.BillingSettled, settled, _currentUser.Id);

        ApplyFlag(admission, discharge);

    }

    private static void ApplyFlag(AdmissionEntity admission, DischargeEntity discharge)
    {
        var ready = Outstanding(discharge).Count == 0;

        if (ready && admission.Status == AdmissionStatus.Admitted)
        {
            AdmissionStatusMachine.EnsureMove(
                admission.Status, AdmissionStatus.ReadyForDischarge, AdmissionStatus.Admitted);

            admission.Status = AdmissionStatus.ReadyForDischarge;

            discharge.FlaggedBy = AssignedBy.User;
            discharge.FlaggedAt = DateTimeOffset.UtcNow;

            return;
        }

        if (!ready && admission.Status == AdmissionStatus.ReadyForDischarge)
        {
            AdmissionStatusMachine.EnsureMove(
                admission.Status, AdmissionStatus.Admitted, AdmissionStatus.ReadyForDischarge);

            admission.Status = AdmissionStatus.Admitted;
        }
    }

    private static IReadOnlyList<string> Outstanding(DischargeEntity discharge)
        => discharge.ChecklistItems
            .Where(item => item.IsMandatory && item.TickedAt is null)
            .Select(item => EnumWire.ToWire(item.ItemType))
            .OrderBy(name => name)
            .ToList();

    private void EnsureMayTick(DischargeChecklistItemType item)
    {
        if (!TickedBy.TryGetValue(item, out var role) || _currentUser.Role == role)
        {
            return;
        }

        throw new ForbiddenException(
            MessageCode.ChecklistItemWrongRole, EnumWire.ToWire(item), EnumWire.ToWire(role));
    }

    private async Task<AdmissionEntity> LoadForWriteAsync(Guid admissionId, CancellationToken ct)
        => await _db.Admissions
            .Include(admission => admission.Patient)
            .Include(admission => admission.BedAssignments)
            .Include(admission => admission.Discharge!)
                .ThenInclude(discharge => discharge.ChecklistItems)
            .FirstOrDefaultAsync(admission => admission.Id == admissionId, ct)
            ?? throw new NotFoundException("Admission", admissionId);

    private Task<DischargeEntity> EnsureDischargeAsync(
        AdmissionEntity admission, CancellationToken ct)
    {
        if (admission.Discharge is { } existing)
        {
            BackfillMissingItems(existing);

            return Task.FromResult(existing);
        }

        var discharge = new DischargeEntity
        {
            Id = Guid.NewGuid(),
            AdmissionId = admission.Id,

            FlaggedBy = AssignedBy.User,
            FlaggedAt = DateTimeOffset.UtcNow
        };

        BackfillMissingItems(discharge);

        _db.Discharges.Add(discharge);
        admission.Discharge = discharge;

        return Task.FromResult(discharge);
    }

    private void BackfillMissingItems(DischargeEntity discharge)
    {
        foreach (var (item, mandatory) in Mandatory)
        {
            if (discharge.ChecklistItems.Any(existing => existing.ItemType == item))
            {
                continue;
            }

            var row = new ChecklistItemEntity
            {
                Id = Guid.NewGuid(),
                DischargeId = discharge.Id,
                ItemType = item,
                IsMandatory = mandatory
            };

            discharge.ChecklistItems.Add(row);
            _db.DischargeChecklistItems.Add(row);
        }
    }

    private static void Tick(
        DischargeEntity discharge, DischargeChecklistItemType item, bool ticked, Guid staffId)
    {
        var row = discharge.ChecklistItems.First(candidate => candidate.ItemType == item);

        if (ticked == row.TickedAt is not null)
        {
            return;
        }

        row.TickedAt = ticked ? DateTimeOffset.UtcNow : null;
        row.TickedByStaffMemberId = ticked ? staffId : null;
    }

    private static IReadOnlyList<(DischargeChecklistItemType Item, bool Ticked)> Requested(
        ChecklistUpdateRequest request)
    {
        var wanted = new List<(DischargeChecklistItemType, bool)>();

        Add(DischargeChecklistItemType.ClinicalClearance, request.ClinicalClearance);

        return wanted;

        void Add(DischargeChecklistItemType item, bool? ticked)
        {
            if (ticked is { } value)
            {
                wanted.Add((item, value));
            }
        }
    }

    private static DischargeCandidate ToCandidate(
        AdmissionEntity admission,
        IReadOnlyDictionary<Guid, BedLabel> labels,
        DateTimeOffset now)
    {
        var label = labels.TryGetValue(admission.Id, out var found) ? found : BedLabel.Unknown;

        var outstanding = admission.Discharge is { } discharge
            ? Outstanding(discharge)

            : Mandatory.Where(entry => entry.Value)
                .Select(entry => EnumWire.ToWire(entry.Key))
                .OrderBy(name => name)
                .ToList();

        return new DischargeCandidate
        {
            AdmissionId = admission.Id,
            Patient = ToPatientSummary(admission.Patient),
            WardName = label.WardName,
            BedNumber = label.BedNumber,
            AdmissionCategory = admission.Category!.Value,
            AdmittedAt = admission.AdmittedAt,
            DaysInBed = admission.AdmittedAt is { } admitted

                ? BillingRates.BillableDays(admitted, admission.Discharge?.ConfirmedAt ?? now)
                : 0,
            OutstandingItems = outstanding,
            IsDischarged = admission.Status == AdmissionStatus.Discharged,
            DischargedAt = admission.Discharge?.ConfirmedAt
        };
    }

    private async Task<DischargeResponse> ToResponseAsync(
        DischargeEntity discharge, CancellationToken ct)
    {
        var names = await StaffNames.ByIdAsync(
            _db,
            discharge.ChecklistItems
                .Select(item => item.TickedByStaffMemberId)
                .Append(discharge.ConfirmedByStaffMemberId),
            ct);

        return ToResponse(discharge, names);
    }

    private static DischargeResponse ToResponse(
        DischargeEntity discharge, IReadOnlyDictionary<Guid, string> names)
        => new()
        {
            Id = discharge.Id,
            AdmissionId = discharge.AdmissionId,
            FlaggedBy = discharge.FlaggedBy,
            FlaggedAt = discharge.FlaggedAt,
            Checklist = discharge.ChecklistItems
                .OrderBy(item => item.ItemType)
                .ToDictionary(
                    item => EnumWire.ToWire(item.ItemType),
                    item => new DTOs.Patient.ChecklistItem
                    {
                        Ticked = item.TickedAt is not null,
                        TickedByStaffId = item.TickedByStaffMemberId,
                        TickedByStaffName = StaffNames.Lookup(names, item.TickedByStaffMemberId),
                        TickedAt = item.TickedAt,
                        Mandatory = item.IsMandatory
                    }),
            AllMandatoryTicked = Outstanding(discharge).Count == 0,
            ConfirmedByStaffId = discharge.ConfirmedByStaffMemberId,
            ConfirmedByStaffName = StaffNames.Lookup(names, discharge.ConfirmedByStaffMemberId),
            ConfirmedAt = discharge.ConfirmedAt,
            SummaryNote = discharge.SummaryNote,
            CreatedAt = discharge.CreatedAt,
            UpdatedAt = discharge.UpdatedAt
        };

    private static PatientSummary ToPatientSummary(PatientEntity patient)
        => new()
        {
            Id = patient.Id,
            PatientCode = patient.PatientCode,
            FullName = patient.FullName,
            Nic = patient.Nic,
            TempReference = patient.TempReference,
            Gender = patient.Gender,
            DateOfBirth = patient.DateOfBirth
        };

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
