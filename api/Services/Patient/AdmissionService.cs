using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Configurations.Patient;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using AdmissionEntity = CareLanka.Api.Data.Entities.Patient.Admission;
using AdmissionResponse = CareLanka.Api.DTOs.Patient.Admission;
using BedAssignmentEntity = CareLanka.Api.Data.Entities.Patient.BedAssignment;
using BedAssignmentResponse = CareLanka.Api.DTOs.Patient.BedAssignment;
using PatientEntity = CareLanka.Api.Data.Entities.Patient.Patient;

namespace CareLanka.Api.Services.Patient;

public sealed class AdmissionService : IAdmissionService
{
    // A visit that has ended. Everything else counts as open, which is what stops a second
    // concurrent admission for the same person.
    private static readonly AdmissionStatus[] ClosedStatuses =
    [
        AdmissionStatus.Discharged,
        AdmissionStatus.Cancelled
    ];

    private readonly CareLankaDbContext _db;
    private readonly IBedRegistryService _beds;
    private readonly IDischargeService _discharges;
    private readonly IBillingService _billing;

    public AdmissionService(
        CareLankaDbContext db,
        IBedRegistryService beds,
        IDischargeService discharges,
        IBillingService billing)
    {
        _db = db;
        _beds = beds;
        _discharges = discharges;
        _billing = billing;
    }

    public async Task<PagedResult<AdmissionSummary>> ListAsync(
        IReadOnlyCollection<AdmissionStatus>? statuses,
        AdmissionCategory? category,
        AdmissionSource? source,
        bool? detailsComplete,
        string? search,
        int page,
        int pageSize,
        AdmissionSortField sortBy,
        SortDirection sortDir,
        CancellationToken ct = default)
    {
        // BedAssignments as well as the patient: ward_name and bed_number are on every row of
        // this list, and they are reached through the live assignment. Loaded with the page
        // rather than queried per row.
        var query = _db.Admissions
            .AsNoTracking()
            .Include(a => a.Patient)
            .Include(a => a.BedAssignments)
            .AsQueryable();

        // No status filter means the worklist, not the archive. A ward board showing every
        // admission the hospital has ever had is useless by the second week.
        query = statuses is { Count: > 0 }
            ? query.Where(a => statuses.Contains(a.Status))
            : query.Where(a => !ClosedStatuses.Contains(a.Status));

        if (category is { } wantedCategory)
        {
            query = query.Where(a => a.Category == wantedCategory);
        }

        if (source is { } wantedSource)
        {
            query = query.Where(a => a.Source == wantedSource);
        }

        if (detailsComplete is { } complete)
        {
            query = query.Where(a => a.DetailsComplete == complete);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";

            query = query.Where(a =>
                EF.Functions.ILike(a.Patient.FullName, pattern)
                || EF.Functions.ILike(a.Patient.PatientCode, pattern)
                || (a.Patient.Nic != null && EF.Functions.ILike(a.Patient.Nic, pattern)));
        }

        var totalItems = await query.CountAsync(ct);

        var sorted = Sort(query, sortBy, sortDir);

        var admissions = await sorted
            // Id breaks ties. Two admissions created in the same millisecond otherwise land in
            // an arbitrary order that can differ between pages, so one is shown twice and
            // another never at all.
            .ThenBy(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var beds = await LabelBedsAsync(admissions, ct);

        return PagedResult<AdmissionSummary>.From(
            admissions.Select(a => ToSummary(a, beds, includePatient: true)).ToList(),
            page, pageSize, totalItems);
    }

    public async Task<AdmissionDetail> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var admission = await _db.Admissions
            .AsNoTracking()
            .Include(a => a.Patient)
            .Include(a => a.BedAssignments)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("Admission", id);

        var beds = await LabelBedsAsync(new[] { admission }, ct);
        var names = await StaffIdsAsync(admission, ct);

        var detail = new AdmissionDetail
        {
            // Newest first, and nothing is filtered out: a rejected or expired assignment is
            // part of the audit trail, not noise.
            BedAssignments = admission.BedAssignments
                .OrderByDescending(b => b.CreatedAt)
                .Select(assignment => ToBedAssignment(assignment, beds, names))
                .ToList(),

            // Both null on a visit nobody has opened a checklist or a bill for, which is the
            // ordinary state of one that has just started. Omitting the key instead would make
            // "not started" and "not served yet" look the same to a client.
            Discharge = await _discharges.FindForAdmissionAsync(id, ct),
            Bill = await _billing.FindForAdmissionAsync(id, ct)
        };

        return Fill(detail, admission, beds, names);
    }

    public async Task<AdmissionResponse> CreateAsync(
        CreateAdmissionRequest request, CancellationToken ct = default)
    {
        if (request.Source == AdmissionSource.Emergency && string.IsNullOrWhiteSpace(request.DispatchId))
        {
            // Without it there is no way back to Emergency's own record of the same journey,
            // and the two systems describe one arrival with no link between them.
            throw new BadRequestException(MessageCode.DispatchIdRequired);
        }

        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == request.PatientId, ct)
            ?? throw new NotFoundException("Patient", request.PatientId);

        var alreadyAdmitted = await _db.Admissions
            .AnyAsync(a => a.PatientId == patient.Id && !ClosedStatuses.Contains(a.Status), ct);

        if (alreadyAdmitted)
        {
            // The ordinary case, answered without waiting for the database to refuse. A second
            // concurrent admission is almost always the desk not realising this patient is
            // already in the building. The index below is what makes it a guarantee.
            throw new ConflictException(MessageCode.PatientHasOpenAdmission, patient.FullName);
        }

        var staffExists = await _db.StaffMembers
            .AnyAsync(s => s.Id == request.CategorySetByStaffId, ct);

        if (!staffExists)
        {
            throw new BadRequestException(
                MessageCode.CategoryStaffNotFound, request.CategorySetByStaffId);
        }

        var admission = new AdmissionEntity
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            // Not null: [ApiController] has already returned a 400 for a body that left any of
            // these out. They are nullable on the request so that omission is an error rather
            // than a silent default — see CreateAdmissionRequest.
            Source = request.Source!.Value,
            Category = request.AdmissionCategory!.Value,
            Urgency = request.Urgency!.Value,

            // A visit that needs a bed starts on the bed board; getting one is a separate,
            // approved step. A visit that does not need a bed has no board to sit on and is
            // admitted the moment the record is opened — see NewVisitStatus below.
            Status = NewVisitStatus(request.AdmissionCategory!.Value),

            IsInfectious = request.IsInfectious,
            CategorySetByStaffMemberId = request.CategorySetByStaffId,
            CategorySetAt = DateTimeOffset.UtcNow,
            DispatchId = string.IsNullOrWhiteSpace(request.DispatchId) ? null : request.DispatchId.Trim(),
            ExpectedArrivalAt = NewVisitExpectedArrival(request),
            AdmittedAt = NewVisitAdmittedAt(request.AdmissionCategory!.Value),
            MissingFields = MissingFieldsFor(patient)
        };

        _db.Admissions.Add(admission);

        try
        {
            // DetailsComplete is deliberately not set: it is a stored generated column over
            // missing_fields, and EF reads it back after the insert.
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(exception, AdmissionConfiguration.OpenAdmissionUniqueIndex))
        {
            // Two desks admitting the same person in the same instant both passed the read
            // above. The partial unique index is what actually stops the second one.
            throw new ConflictException(MessageCode.PatientHasOpenAdmission, patient.FullName);
        }

        admission.Patient = patient;

        // A brand new admission holds no bed, so there is nothing to label.
        return await FillAsync(new AdmissionResponse(), admission, BedLabel.None, ct);
    }

    public async Task<AdmissionResponse> CompleteDetailsAsync(
        Guid id, CompleteDetailsRequest request, CancellationToken ct = default)
    {
        var admission = await _db.Admissions
            .Include(a => a.Patient)
            .Include(a => a.BedAssignments)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("Admission", id);

        var patient = admission.Patient;
        var nic = Clean(request.Nic);

        if (nic is not null && nic != patient.Nic)
        {
            var takenBy = await _db.Patients
                .Where(p => p.Nic == nic && p.Id != patient.Id)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct);

            if (takenBy is not null)
            {
                // The relative arrived with an NIC that already belongs to a different record.
                // Merging the two is a decision, not something this endpoint should guess at.
                throw new ConflictException(MessageCode.PatientNicTaken, nic, takenBy);
            }
        }

        // A key left out is left alone. That is the whole difference between this and the PUT
        // on a patient, where an omitted field is cleared.
        patient.Nic = nic ?? patient.Nic;
        patient.FullName = Clean(request.FullName) ?? patient.FullName;
        patient.DateOfBirth = request.DateOfBirth ?? patient.DateOfBirth;
        patient.Phone = Clean(request.Phone) ?? patient.Phone;
        patient.Address = Clean(request.Address) ?? patient.Address;
        patient.EmergencyContactName = Clean(request.EmergencyContactName) ?? patient.EmergencyContactName;
        patient.EmergencyContactPhone = Clean(request.EmergencyContactPhone) ?? patient.EmergencyContactPhone;

        // Recalculated here rather than trusted from the caller: completeness is a fact about
        // the record, and a client that computed it wrong would hide outstanding paperwork.
        admission.MissingFields = MissingFieldsFor(patient);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(exception, PatientConfiguration.NicUniqueIndex))
        {
            throw new ConflictException(MessageCode.PatientNicTaken, nic, "another record");
        }

        return await FillAsync(
            new AdmissionResponse(), admission, await LabelBedsAsync(new[] { admission }, ct), ct);
    }

    /// <summary>Where a brand-new visit starts, which depends on whether it needs a bed.</summary>
    /// <remarks>
    /// A visit that needs a bed starts at <c>awaiting_bed</c>: it goes on the bed board and
    /// waits for one to be assigned and approved.
    ///
    /// A visit that does not — an outpatient scan, a blood test — starts at <c>admitted</c>,
    /// because opening the record *is* the arrival. There is nothing to wait for and no board
    /// to wait on. Putting them at <c>awaiting_bed</c> was the bug: the only edge into
    /// <c>admitted</c> is from <c>bed_reserved</c>, so a patient who would never be given a
    /// bed could never reach it, could never be discharged, and sat on the ward board as
    /// "awaiting bed" until somebody cancelled them.
    /// </remarks>
    private static AdmissionStatus NewVisitStatus(AdmissionCategory category)
        => BedPlacementRules.RequiresBed(category)
            ? AdmissionStatus.AwaitingBed
            : AdmissionStatus.Admitted;

    /// <summary>The arrival stamp a new visit is born with. Null unless it skipped the board.</summary>
    private static DateTimeOffset? NewVisitAdmittedAt(AdmissionCategory category)
        => BedPlacementRules.RequiresBed(category) ? null : DateTimeOffset.UtcNow;

    /// <summary>
    /// The expected-arrival time a new visit keeps. Always null for a visit needing no bed.
    /// </summary>
    /// <remarks>
    /// An outpatient record is opened with the patient in front of you, so "expected at" is
    /// already in the past by the time it is written. Kept, it would put somebody standing at
    /// the desk into the next two hours' incoming count on the capacity screen — the same
    /// reason check-in drops it. Dropped rather than refused: a booking carried the time for a
    /// good reason up to this point, and a 400 here would be punishing the caller for it.
    /// </remarks>
    private static DateTimeOffset? NewVisitExpectedArrival(CreateAdmissionRequest request)
        => BedPlacementRules.RequiresBed(request.AdmissionCategory!.Value)
            ? request.ExpectedArrival
            : null;

    /// <summary>
    /// Ends a visit that never needed a bed: the scan is done, the patient has gone home.
    /// </summary>
    /// <remarks>
    /// Narrow on purpose. This is **not** the discharge workflow — that is step 7 of
    /// build/patient.md, it has a checklist, a summary note and an approver, and it has to
    /// release the bed. A visit holding a bed is refused here and told so, rather than being
    /// quietly half-discharged by an endpoint that does not know how to give the bed back.
    ///
    /// Two hops, the way assigning a bed by hand does it: the published table has no
    /// <c>admitted -&gt; discharged</c> edge and this is not the place to invent one. For a
    /// visit with no bed and no checklist, being ready to go and going are the same act, so
    /// both moves happen and both are checked. Nothing observes the middle state.
    /// </remarks>
    public Task<AdmissionResponse> CompleteAsync(Guid id, CancellationToken ct = default)
        => InTransitionAsync(id, admission =>
        {
            if (BedPlacementRules.RequiresBed(admission.Category))
            {
                throw new ConflictException(
                    MessageCode.VisitNeedsDischargeNotComplete,
                    EnumWire.ToWire(admission.Category));
            }

            AdmissionStatusMachine.EnsureMove(
                admission.Status,
                AdmissionStatus.ReadyForDischarge,
                AdmissionStatus.Admitted);
            AdmissionStatusMachine.EnsureMove(
                AdmissionStatus.ReadyForDischarge,
                AdmissionStatus.Discharged,
                AdmissionStatus.ReadyForDischarge);

            admission.Status = AdmissionStatus.Discharged;
            admission.DischargedAt = DateTimeOffset.UtcNow;
        }, ct);

    public Task<AdmissionResponse> MarkArrivedAsync(Guid id, CancellationToken ct = default)
        => InTransitionAsync(id, admission =>
        {
            // Only from bed_reserved, and nowhere else. The workflow also allows
            // ready_for_discharge -> admitted, but that is a nurse un-flagging a discharge and
            // it must not stamp an arrival time, so it is a different endpoint at step 7.
            AdmissionStatusMachine.EnsureMove(
                admission.Status, AdmissionStatus.Admitted, AdmissionStatus.BedReserved);

            admission.Status = AdmissionStatus.Admitted;
            admission.AdmittedAt = DateTimeOffset.UtcNow;

            // The hold becomes an occupancy. Until this happens the 30-minute expiry can still
            // take the bed back, which would free a bed with a patient already in it.
            var hold = admission.BedAssignments
                .FirstOrDefault(b => b.Status == AssignmentStatus.Reserved);

            if (hold is not null)
            {
                hold.Status = AssignmentStatus.Occupied;
                hold.ReservedUntil = null;

                // When the stay in THIS bed started, which is what the bill is priced from.
                // The same instant as admitted_at for a first bed, and not the same at all for
                // a second one after a mid-stay transfer.
                hold.OccupiedAt = admission.AdmittedAt;
            }
        }, ct);

    public Task<AdmissionResponse> CancelAsync(
        Guid id, CancelAdmissionRequest request, CancellationToken ct = default)
        => InTransitionAsync(id, admission =>
        {
            // Everything before the patient is physically here. An admitted patient is
            // discharged, not cancelled — you cannot call off somebody lying in your ward.
            AdmissionStatusMachine.EnsureMove(
                admission.Status,
                AdmissionStatus.Cancelled,
                AdmissionStatus.AwaitingBed,
                AdmissionStatus.AwaitingApproval,
                AdmissionStatus.BedReserved);

            admission.Status = AdmissionStatus.Cancelled;
            admission.CancelReason = request.Reason!.Value;
            admission.CancelNote = Clean(request.Note);

            // Any bed this visit was holding goes back to the pool in the same transaction.
            // Leave it behind and the bed is out of service for nobody, and
            // ux_bed_assignments_live_bed then refuses the next patient who needs it.
            foreach (var live in admission.BedAssignments
                .Where(b => b.Status != AssignmentStatus.Released))
            {
                live.Status = AssignmentStatus.Released;
                live.ReservedUntil = null;
                live.ReleasedAt = DateTimeOffset.UtcNow;
                live.ReleaseReason = ReleaseReason.Cancelled;
            }
        }, ct);

    public Task<AdmissionEntity?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Admissions.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<AdmissionEntity> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await FindByIdAsync(id, ct) ?? throw new NotFoundException("Admission", id);

    /// <summary>
    /// Runs one status change against a locked row, so two people acting on the same visit are
    /// serialised rather than each overwriting the other.
    /// </summary>
    /// <remarks>
    /// The problem this solves: a transition check reads the status, and the save writes it. A
    /// manager cancelling and a nurse marking arrival both read <c>bed_reserved</c> in the gap
    /// between, both pass the check, and the later write wins — so a cancelled patient ends up
    /// admitted. No index can catch that, because each row is legal on its own.
    ///
    /// <c>SELECT ... FOR UPDATE</c> holds the admission row until this transaction commits, so
    /// the second request waits, then reads the status the first one left behind and is refused
    /// with the honest 409: cannot move from <c>cancelled</c> to <c>admitted</c>. The same
    /// approach the bed approval takes at step 11, for the same reason.
    /// </remarks>
    private async Task<AdmissionResponse> InTransitionAsync(
        Guid id, Action<AdmissionEntity> change, CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        // Takes the lock and nothing else. Locking and loading in one composed query puts
        // FOR UPDATE inside a join against patients and bed_assignments, which locks rows
        // nobody asked about. A missing row locks nothing and falls through to the 404 below.
        await _db.Database.ExecuteSqlAsync(
            $"SELECT id FROM admissions WHERE id = {id} FOR UPDATE", ct);

        // Read committed gives every statement its own snapshot, so this sees whatever the
        // request we just waited for committed — not the stale row we queued behind.
        var admission = await _db.Admissions
            .Include(a => a.Patient)
            .Include(a => a.BedAssignments)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("Admission", id);

        change(admission);

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await FillAsync(
            new AdmissionResponse(), admission, await LabelBedsAsync(new[] { admission }, ct), ct);
    }

    private static IOrderedQueryable<AdmissionEntity> Sort(
        IQueryable<AdmissionEntity> query, AdmissionSortField sortBy, SortDirection sortDir)
    {
        var ascending = sortDir == SortDirection.Asc;

        return sortBy switch
        {
            AdmissionSortField.ExpectedArrival => ascending
                ? query.OrderBy(a => a.ExpectedArrivalAt)
                : query.OrderByDescending(a => a.ExpectedArrivalAt),

            AdmissionSortField.AdmittedAt => ascending
                ? query.OrderBy(a => a.AdmittedAt)
                : query.OrderByDescending(a => a.AdmittedAt),

            // Ranked, not sorted by the stored value. Urgency is a snake_case string in the
            // database, so ordering the column alphabetically gives emergency, routine, urgent
            // — which reads like a sort and is not one. Descending is most urgent first.
            //
            // Written inline rather than as a helper: a method call inside the lambda is not
            // something EF can turn into SQL, and it fails at run time, not at compile time.
            AdmissionSortField.Urgency => ascending
                ? query.OrderBy(a => a.Urgency == AdmissionUrgency.Emergency ? 2
                    : a.Urgency == AdmissionUrgency.Urgent ? 1 : 0)
                : query.OrderByDescending(a => a.Urgency == AdmissionUrgency.Emergency ? 2
                    : a.Urgency == AdmissionUrgency.Urgent ? 1 : 0),

            _ => ascending
                ? query.OrderBy(a => a.CreatedAt)
                : query.OrderByDescending(a => a.CreatedAt)
        };
    }

    /// <summary>
    /// What paperwork is outstanding, as the closed vocabulary of <see cref="PatientDetailField"/>.
    /// Field names rather than a count, because "two things missing" does not tell a ward clerk
    /// what to chase.
    /// </summary>
    private static List<string> MissingFieldsFor(PatientEntity patient)
    {
        var missing = new List<string>();

        void Require(PatientDetailField field, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                missing.Add(EnumWire.ToWire(field));
            }
        }

        Require(PatientDetailField.Nic, patient.Nic);
        Require(PatientDetailField.FullName, patient.FullName);
        Require(PatientDetailField.Phone, patient.Phone);
        Require(PatientDetailField.Address, patient.Address);
        Require(PatientDetailField.EmergencyContactName, patient.EmergencyContactName);
        Require(PatientDetailField.EmergencyContactPhone, patient.EmergencyContactPhone);

        if (patient.DateOfBirth is null)
        {
            missing.Add(EnumWire.ToWire(PatientDetailField.DateOfBirth));
        }

        return missing;
    }

    private static AdmissionSummary ToSummary(
        AdmissionEntity admission,
        IReadOnlyDictionary<Guid, BedLabel> beds,
        bool includePatient)
    {
        // Where this patient is *now*, which is the live assignment and nothing else. A
        // released row is history and an expired hold is a bed somebody else may already have,
        // so naming either here would put a patient in a bed they are not in.
        var live = LiveAssignment(admission);

        var label = live is not null && beds.TryGetValue(live.BedId, out var found)
            ? found
            : (BedLabel?)null;

        return new AdmissionSummary
        {
            Id = admission.Id,
            Patient = includePatient ? ToPatientSummary(admission.Patient) : null,
            Source = admission.Source,
            AdmissionCategory = admission.Category,
            Urgency = admission.Urgency,
            Status = admission.Status,
            DetailsComplete = admission.DetailsComplete,
            RequiresBed = BedPlacementRules.RequiresBed(admission.Category),

            // Null when nobody has been given a bed yet, which is the ordinary state of an
            // admission in awaiting_bed. Not an error and not a gap in the data.
            WardName = label?.WardName,
            BedNumber = label?.BedNumber,

            ExpectedArrival = admission.ExpectedArrivalAt,
            AdmittedAt = admission.AdmittedAt
        };
    }

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

    private static BedAssignmentResponse ToBedAssignment(
        BedAssignmentEntity assignment,
        IReadOnlyDictionary<Guid, BedLabel> beds,
        IReadOnlyDictionary<Guid, string> names)
    {
        var label = beds.TryGetValue(assignment.BedId, out var found) ? found : BedLabel.Unknown;

        return new BedAssignmentResponse
        {
            Id = assignment.Id,
            AdmissionId = assignment.AdmissionId,
            BedId = assignment.BedId,

            // Read from Equipment's register, never stored on our row. Empty when the bed has
            // since been retired: the assignment is history and the frame it names is gone, so
            // there is no name to give. The id is still there for anyone who needs to trace it.
            WardName = label.WardName,
            BedNumber = label.BedNumber,

            Status = assignment.Status,
            ReservedUntil = assignment.ReservedUntil,
            AssignedBy = assignment.AssignedBy,
            WorkflowId = assignment.WorkflowId,
            IsDowngrade = assignment.IsDowngrade,
            ApprovedByStaffId = assignment.ApprovedByStaffMemberId,
            ApprovedByStaffName = StaffNames.Lookup(names, assignment.ApprovedByStaffMemberId),
            ApprovedAt = assignment.ApprovedAt,
            OverrideReason = assignment.OverrideReason,
            ReleasedAt = assignment.ReleasedAt,
            ReleaseReason = assignment.ReleaseReason,
            CreatedAt = assignment.CreatedAt,
            UpdatedAt = assignment.UpdatedAt
        };
    }

    /// <summary>
    /// Ward name and bed number for every bed these admissions have ever been assigned to.
    /// </summary>
    /// <remarks>
    /// Two reads for a whole page rather than two per row: one into Equipment's register
    /// through the adapter, one into our own ward table. An admission that has never held a bed
    /// contributes nothing and costs nothing — both reads are skipped entirely.
    /// </remarks>
    private async Task<IReadOnlyDictionary<Guid, BedLabel>> LabelBedsAsync(
        IReadOnlyCollection<AdmissionEntity> admissions, CancellationToken ct)
    {
        var bedIds = admissions
            .SelectMany(admission => admission.BedAssignments)
            .Select(assignment => assignment.BedId)
            .Distinct()
            .ToList();

        return await BedLabels.ByBedIdAsync(_db, _beds, bedIds, ct);
    }

    /// <summary>
    /// The assignment that claims a bed for this admission right now, or null when none does.
    /// </summary>
    /// <remarks>
    /// At most one can qualify: <c>ux_bed_assignments_live_admission</c> makes a live
    /// assignment per admission unique. The expiry half comes from <see cref="BedHold"/>, so
    /// this agrees with every other read of "is that bed still held".
    /// </remarks>
    private static BedAssignmentEntity? LiveAssignment(AdmissionEntity admission)
    {
        var now = DateTimeOffset.UtcNow;

        return admission.BedAssignments.FirstOrDefault(
            assignment => BedHold.IsLive(assignment, now));
    }

    /// <summary>
    /// <see cref="Fill{TResponse}"/>, having first looked up every staff name the response
    /// carries: whoever chose the care level, and whoever approved each bed.
    /// </summary>
    /// <remarks>
    /// One query for the whole response. The ids on an admission are a small, heavily repeated
    /// set - the same nurse approves every bed on a ward - so resolving them per row would be
    /// the same answer fetched many times.
    /// </remarks>
    private async Task<TResponse> FillAsync<TResponse>(
        TResponse response,
        AdmissionEntity admission,
        IReadOnlyDictionary<Guid, BedLabel> beds,
        CancellationToken ct)
        where TResponse : AdmissionResponse
        => Fill(response, admission, beds, await StaffIdsAsync(admission, ct));

    private Task<IReadOnlyDictionary<Guid, string>> StaffIdsAsync(
        AdmissionEntity admission, CancellationToken ct)
        => StaffNames.ByIdAsync(
            _db,
            admission.BedAssignments
                .Select(assignment => assignment.ApprovedByStaffMemberId)
                .Append(admission.CategorySetByStaffMemberId),
            ct);

    private static TResponse Fill<TResponse>(
        TResponse response,
        AdmissionEntity admission,
        IReadOnlyDictionary<Guid, BedLabel> beds,
        IReadOnlyDictionary<Guid, string> names)
        where TResponse : AdmissionResponse
    {
        var summary = ToSummary(admission, beds, includePatient: true);

        response.Id = summary.Id;
        response.Patient = summary.Patient;
        response.Source = summary.Source;
        response.AdmissionCategory = summary.AdmissionCategory;
        response.Urgency = summary.Urgency;
        response.Status = summary.Status;
        response.DetailsComplete = summary.DetailsComplete;
        response.RequiresBed = summary.RequiresBed;
        response.WardName = summary.WardName;
        response.BedNumber = summary.BedNumber;
        response.ExpectedArrival = summary.ExpectedArrival;
        response.AdmittedAt = summary.AdmittedAt;

        response.DispatchId = admission.DispatchId;
        response.CategorySetByStaffId = admission.CategorySetByStaffMemberId;
        response.CategorySetByStaffName =
            StaffNames.Lookup(names, admission.CategorySetByStaffMemberId);
        response.CategorySetAt = admission.CategorySetAt;
        response.IsInfectious = admission.IsInfectious;
        response.ReportedByUserId = admission.ReportedByUserId;
        response.MissingFields = admission.MissingFields.ToList();
        response.DischargedAt = admission.DischargedAt;
        response.CancelReason = admission.CancelReason;
        response.CancelNote = admission.CancelNote;
        response.CreatedAt = admission.CreatedAt;
        response.UpdatedAt = admission.UpdatedAt;

        return response;
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsUniqueViolation(DbUpdateException exception, string constraintName)
        => exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        } postgres && postgres.ConstraintName == constraintName;
}
