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
    /// <summary>
    /// The five boxes, and which of them stop a discharge. Written once here rather than
    /// scattered through the methods below, because "is this patient ready" is a question three
    /// endpoints ask and they must all mean the same thing by it.
    /// </summary>
    private static readonly IReadOnlyDictionary<DischargeChecklistItemType, bool> Mandatory =
        new Dictionary<DischargeChecklistItemType, bool>
        {
            [DischargeChecklistItemType.ClinicalClearance] = true,
            [DischargeChecklistItemType.MedicationIssued] = true,
            [DischargeChecklistItemType.BillingSettled] = true,
            [DischargeChecklistItemType.FollowUpRecorded] = false,
            [DischargeChecklistItemType.TransportArranged] = false
        };

    /// <summary>
    /// Who may tick what. The role gate is here and not on the route, because which boxes you
    /// are allowed to touch depends on which boxes are in the body.
    /// </summary>
    /// <remarks>
    /// <c>ClinicalClearance</c> is the wall: a doctor, a human, always. No automated process can
    /// ever set it, which is why the agent has no tool that reaches this method.
    ///
    /// <c>BillingSettled</c> is absent on purpose. It is not ticked by a role at all - settling
    /// the bill writes it, through <see cref="MarkBillingSettledAsync"/>.
    /// </remarks>
    private static readonly IReadOnlyDictionary<DischargeChecklistItemType, PrincipalRole> TickedBy =
        new Dictionary<DischargeChecklistItemType, PrincipalRole>
        {
            [DischargeChecklistItemType.ClinicalClearance] = PrincipalRole.Doctor,
            [DischargeChecklistItemType.MedicationIssued] = PrincipalRole.WardNurse,
            [DischargeChecklistItemType.FollowUpRecorded] = PrincipalRole.WardNurse,
            [DischargeChecklistItemType.TransportArranged] = PrincipalRole.WardNurse
        };

    /// <summary>The two care levels whose discharge is the duty manager's, from plan 6.3.</summary>
    private static readonly AdmissionCategory[] NeedsDutyManager =
        [AdmissionCategory.Icu, AdmissionCategory.Hdu];

    /// <summary>A visit still in the building. Nothing else can be on the candidate list.</summary>
    private static readonly AdmissionStatus[] OnTheWard =
        [AdmissionStatus.Admitted, AdmissionStatus.ReadyForDischarge];

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
        Guid? wardId, int page, int pageSize, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var query = _db.Admissions
            .AsNoTracking()
            .Include(admission => admission.Patient)
            .Include(admission => admission.BedAssignments)
            .Include(admission => admission.Discharge!)
                .ThenInclude(discharge => discharge.ChecklistItems)
            .Where(admission => OnTheWard.Contains(admission.Status));

        if (wardId is { } onlyWard)
        {
            // Which ward a patient is in is not a column of ours: it is Equipment's bed, joined
            // to our live assignment. So the filter is "in one of that ward's beds".
            var bedIds = (await _beds.ListBedsInWardsAsync([onlyWard], ct))
                .Select(bed => bed.Id)
                .ToList();

            query = query.Where(admission => admission.BedAssignments
                .Any(assignment => bedIds.Contains(assignment.BedId)
                    && assignment.Status != AssignmentStatus.Released));
        }

        var admissions = await query.ToListAsync(ct);

        var labels = await BedLabels.LiveByAdmissionAsync(
            _db, _beds, admissions.Select(admission => admission.Id).ToList(), now, ct);

        var candidates = admissions
            .Select(admission => ToCandidate(admission, labels, now))

            // Ready first, then whoever has least left to do, then longest stay. A ward nurse
            // works down this list, so the order is the order the work gets done in.
            .OrderBy(candidate => candidate.OutstandingItems.Count)
            .ThenByDescending(candidate => candidate.DaysInBed)
            .ThenBy(candidate => candidate.Patient.FullName)
            .ToList();

        // Paged in memory, for the same reason the bed candidate list is: readiness is not a
        // column anywhere. It is a count over the checklist rows, so the rows have to exist
        // before they can be ordered or counted. A ward holds tens of patients, not millions.
        return PagedResult<DischargeCandidate>.From(
            candidates.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            page,
            pageSize,
            candidates.Count);
    }

    public async Task<DischargeResponse> UpdateChecklistAsync(
        Guid admissionId, ChecklistUpdateRequest request, CancellationToken ct = default)
    {
        // Refused before anything is loaded, because it is a complaint about the request and
        // not about this admission. Settling the bill writes this box; nothing else does.
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
        var discharge = await EnsureDischargeAsync(admission, ct);

        foreach (var (item, ticked) in wanted)
        {
            Tick(discharge, item, ticked, _currentUser.Id);
        }

        ApplyFlag(admission, discharge);

        await _db.SaveChangesAsync(ct);

        return ToResponse(discharge);
    }

    /// <remarks>
    /// The row lock is the same one <c>/arrive</c>, <c>/cancel</c> and <c>assign-bed</c> take,
    /// for the same reason: a nurse confirming a discharge and a manager cancelling the visit
    /// both read a legal status, both pass, and the later write wins. Nothing about either row
    /// is illegal on its own, so no index can catch it.
    /// </remarks>
    public async Task<DischargeResponse> ConfirmAsync(
        Guid admissionId, ConfirmDischargeRequest request, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        // Takes the lock and nothing else. Locking and loading in one composed query puts
        // FOR UPDATE inside a join and locks rows nobody asked about.
        await _db.Database.ExecuteSqlAsync(
            $"SELECT id FROM admissions WHERE id = {admissionId} FOR UPDATE", ct);

        var admission = await LoadForWriteAsync(admissionId, ct);

        EnsureMayConfirm(admission.Category);

        var discharge = await EnsureDischargeAsync(admission, ct);

        var outstanding = Outstanding(discharge);

        if (outstanding.Count > 0)
        {
            // Belt and braces: the status check below would refuse this too, because nothing
            // reaches ready_for_discharge with a box outstanding. This one names what is
            // missing, which is the answer a nurse can act on.
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

        // The bed goes back in the same transaction. Leave it behind and the ward reports a
        // bed nobody is in as occupied, and ux_bed_assignments_live_bed then refuses the next
        // patient who needs it.
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

        return ToResponse(discharge);
    }

    public async Task<DischargeResponse?> FindForAdmissionAsync(
        Guid admissionId, CancellationToken ct = default)
    {
        var discharge = await _db.Discharges
            .AsNoTracking()
            .Include(row => row.ChecklistItems)
            .FirstOrDefaultAsync(row => row.AdmissionId == admissionId, ct);

        return discharge is null ? null : ToResponse(discharge);
    }

    public async Task MarkBillingSettledAsync(
        Guid admissionId, bool settled, CancellationToken ct = default)
    {
        var admission = await LoadForWriteAsync(admissionId, ct);
        var discharge = await EnsureDischargeAsync(admission, ct);

        Tick(discharge, DischargeChecklistItemType.BillingSettled, settled, _currentUser.Id);

        ApplyFlag(admission, discharge);

        // No SaveChanges. This runs inside BillingService's transaction, so the money and the
        // tick commit together or neither does.
    }

    // ---------- the rules ----------

    /// <summary>
    /// Moves the admission onto or off the candidate list, which is what ticking the last
    /// mandatory box actually does.
    /// </summary>
    /// <remarks>
    /// Both directions, because unticking has to undo it. <c>ready_for_discharge</c> to
    /// <c>admitted</c> is a published edge precisely for this - a nurse who realises the
    /// medication was not issued after all.
    ///
    /// <c>FlaggedAt</c> is restamped when the flag fires. On a row that has only just been
    /// created it is the moment the checklist was opened, which is the honest reading of a
    /// discharge nobody has flagged yet.
    /// </remarks>
    private static void ApplyFlag(AdmissionEntity admission, DischargeEntity discharge)
    {
        var ready = Outstanding(discharge).Count == 0;

        if (ready && admission.Status == AdmissionStatus.Admitted)
        {
            AdmissionStatusMachine.EnsureMove(
                admission.Status, AdmissionStatus.ReadyForDischarge, AdmissionStatus.Admitted);

            admission.Status = AdmissionStatus.ReadyForDischarge;

            // A rule flagged it, not the agent - plan 6.2. Checking whether three boxes are
            // ticked is a WHERE clause, and a language model in front of it would buy nothing.
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

    /// <summary>The mandatory boxes still unticked, by their wire name. Empty means ready.</summary>
    private static IReadOnlyList<string> Outstanding(DischargeEntity discharge)
        => discharge.ChecklistItems
            .Where(item => item.IsMandatory && item.TickedAt is null)
            .Select(item => EnumWire.ToWire(item.ItemType))
            .OrderBy(name => name)
            .ToList();

    /// <summary>A 403 and not a 409: the box is fine, the caller is not the person who ticks it.</summary>
    private void EnsureMayTick(DischargeChecklistItemType item)
    {
        if (!TickedBy.TryGetValue(item, out var role) || _currentUser.Role == role)
        {
            return;
        }

        throw new ForbiddenException(
            MessageCode.ChecklistItemWrongRole, EnumWire.ToWire(item), EnumWire.ToWire(role));
    }

    /// <summary>
    /// ICU and HDU discharges are the duty manager's. Everything else is the ward nurse's, and
    /// the duty manager may do those too.
    /// </summary>
    private void EnsureMayConfirm(AdmissionCategory category)
    {
        if (_currentUser.Role == PrincipalRole.DutyManager
            || !NeedsDutyManager.Contains(category))
        {
            return;
        }

        throw new ForbiddenException(
            MessageCode.DischargeNeedsDutyManager, EnumWire.ToWire(category));
    }

    // ---------- loading and writing ----------

    private async Task<AdmissionEntity> LoadForWriteAsync(Guid admissionId, CancellationToken ct)
        => await _db.Admissions
            .Include(admission => admission.Patient)
            .Include(admission => admission.BedAssignments)
            .Include(admission => admission.Discharge!)
                .ThenInclude(discharge => discharge.ChecklistItems)
            .FirstOrDefaultAsync(admission => admission.Id == admissionId, ct)
            ?? throw new NotFoundException("Admission", admissionId);

    /// <summary>
    /// The discharge record for this admission, created with its five boxes if it has none.
    /// </summary>
    /// <remarks>
    /// Created on first touch rather than alongside the admission. Every visit already on the
    /// system when this step landed gets one the moment somebody needs it, so there was no
    /// backfill and no migration for the rows - and a visit nobody ever discharges never grows
    /// five rows it does not need.
    /// </remarks>
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

            // A human opened this checklist. The agent has no tool that reaches here, and
            // AssignedBy.Agent on a discharge would be a claim nothing in this component makes.
            FlaggedBy = AssignedBy.User,
            FlaggedAt = DateTimeOffset.UtcNow
        };

        BackfillMissingItems(discharge);

        _db.Discharges.Add(discharge);
        admission.Discharge = discharge;

        return Task.FromResult(discharge);
    }

    /// <summary>
    /// Adds any box this discharge row is missing, so adding a sixth item later is a change to
    /// <see cref="Mandatory"/> and nothing else. On an existing row it normally does nothing.
    /// </summary>
    /// <remarks>
    /// The explicit <c>_db.DischargeChecklistItems.Add</c> is not belt and braces. We allocate
    /// the key ourselves, and change tracking decides Added-versus-Modified for an entity it
    /// meets through a navigation by looking at the key: a non-default one means "this row
    /// already exists", so a new box would be saved as an UPDATE that matches no row and the
    /// request would die with a concurrency exception.
    /// </remarks>
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

    /// <summary>
    /// Ticking is writing a timestamp and a staff id; unticking is clearing both. One nullable
    /// timestamp rather than a bool beside it, so the two can never contradict each other.
    /// </summary>
    private static void Tick(
        DischargeEntity discharge, DischargeChecklistItemType item, bool ticked, Guid staffId)
    {
        var row = discharge.ChecklistItems.First(candidate => candidate.ItemType == item);

        if (ticked == row.TickedAt is not null)
        {
            // Already in the state asked for. Rewriting it would move the stamp to whoever
            // pressed the button second, which is not who did the work.
            return;
        }

        row.TickedAt = ticked ? DateTimeOffset.UtcNow : null;
        row.TickedByStaffMemberId = ticked ? staffId : null;
    }

    /// <summary>The keys actually present in the body. A key left out is not touched.</summary>
    private static IReadOnlyList<(DischargeChecklistItemType Item, bool Ticked)> Requested(
        ChecklistUpdateRequest request)
    {
        var wanted = new List<(DischargeChecklistItemType, bool)>();

        Add(DischargeChecklistItemType.ClinicalClearance, request.ClinicalClearance);
        Add(DischargeChecklistItemType.MedicationIssued, request.MedicationIssued);
        Add(DischargeChecklistItemType.FollowUpRecorded, request.FollowUpRecorded);
        Add(DischargeChecklistItemType.TransportArranged, request.TransportArranged);

        return wanted;

        void Add(DischargeChecklistItemType item, bool? ticked)
        {
            if (ticked is { } value)
            {
                wanted.Add((item, value));
            }
        }
    }

    // ---------- mapping ----------

    private static DischargeCandidate ToCandidate(
        AdmissionEntity admission,
        IReadOnlyDictionary<Guid, BedLabel> labels,
        DateTimeOffset now)
    {
        var label = labels.TryGetValue(admission.Id, out var found) ? found : BedLabel.Unknown;

        var outstanding = admission.Discharge is { } discharge
            ? Outstanding(discharge)

            // No checklist row yet, so everything mandatory is outstanding. Listed rather than
            // hidden: this patient is exactly who a nurse is looking for.
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
            AdmissionCategory = admission.Category,
            AdmittedAt = admission.AdmittedAt,
            DaysInBed = admission.AdmittedAt is { } admitted
                ? BillingRates.BillableDays(admitted, now)
                : 0,
            OutstandingItems = outstanding
        };
    }

    private static DischargeResponse ToResponse(DischargeEntity discharge)
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
                        TickedAt = item.TickedAt,
                        Mandatory = item.IsMandatory
                    }),
            AllMandatoryTicked = Outstanding(discharge).Count == 0,
            ConfirmedByStaffId = discharge.ConfirmedByStaffMemberId,
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
