using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Configurations.Patient;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Common;
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
    private static readonly AdmissionStatus[] ClosedStatuses =
    [
        AdmissionStatus.Discharged,
        AdmissionStatus.Cancelled
    ];

    private const int PatientSaveAttempts = 5;

    private readonly CareLankaDbContext _db;
    private readonly IBedRegistryService _beds;
    private readonly IDischargeService _discharges;
    private readonly IBillingService _billing;
    private readonly ICurrentUser _currentUser;

    public AdmissionService(
        CareLankaDbContext db,
        IBedRegistryService beds,
        IDischargeService discharges,
        IBillingService billing,
        ICurrentUser currentUser)
    {
        _db = db;
        _beds = beds;
        _discharges = discharges;
        _billing = billing;
        _currentUser = currentUser;
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
        var query = _db.Admissions
            .AsNoTracking()
            .Include(a => a.Patient)
            .Include(a => a.BedAssignments)
            .AsQueryable();

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
                || (a.Patient.Nic != null && EF.Functions.ILike(a.Patient.Nic, pattern))
                || (a.Patient.TempReference != null
                    && EF.Functions.ILike(a.Patient.TempReference, pattern)));
        }

        var totalItems = await query.CountAsync(ct);

        var sorted = Sort(query, sortBy, sortDir);

        var admissions = await sorted
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
            BedAssignments = admission.BedAssignments
                .OrderByDescending(b => b.CreatedAt)
                .Select(assignment => ToBedAssignment(assignment, beds, names))
                .ToList(),

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
            throw new BadRequestException(MessageCode.DispatchIdRequired);
        }

        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == request.PatientId, ct)
            ?? throw new NotFoundException("Patient", request.PatientId);

        BedPlacementRules.EnsureMayHaveCategory(request.AdmissionCategory!.Value, patient.Gender);

        var alreadyAdmitted = await _db.Admissions
            .AnyAsync(a => a.PatientId == patient.Id && !ClosedStatuses.Contains(a.Status), ct);

        if (alreadyAdmitted)
        {
            throw new ConflictException(MessageCode.PatientHasOpenAdmission, patient.FullName);
        }

        var admission = new AdmissionEntity
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Source = request.Source!.Value,
            Category = request.AdmissionCategory!.Value,
            Urgency = request.Urgency!.Value,

            Status = AdmissionStatus.AwaitingBed,

            IsInfectious = request.IsInfectious,
            // Whoever is signed in chose it. Never taken from the request body.
            CategorySetByStaffMemberId = _currentUser.Id,
            CategorySetAt = DateTimeOffset.UtcNow,
            DispatchId = string.IsNullOrWhiteSpace(request.DispatchId) ? null : request.DispatchId.Trim(),
            ExpectedArrivalAt = request.ExpectedArrival,
            MissingFields = PatientDetailChecklist.MissingFor(patient)
        };

        _db.Admissions.Add(admission);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(exception, AdmissionConfiguration.OpenAdmissionUniqueIndex))
        {
            throw new ConflictException(MessageCode.PatientHasOpenAdmission, patient.FullName);
        }

        admission.Patient = patient;

        return await FillAsync(new AdmissionResponse(), admission, BedLabel.None, ct);
    }

    public async Task<AdmissionResponse> PreAdmitAsync(
        PreAdmitRequest request, CancellationToken ct = default)
    {
        var duplicate = await _db.Admissions
            .AnyAsync(a => a.DispatchId == request.DispatchId, ct);

        if (duplicate)
        {
            throw new ConflictException(MessageCode.PreAdmissionAlreadyExists, request.DispatchId);
        }

        var patient = request.PatientIsCaller
            ? await _db.Patients.FirstOrDefaultAsync(p => p.Id == request.PatientId!.Value, ct)
                ?? throw new NotFoundException("Patient", request.PatientId!.Value)
            : await NewProvisionalPatientAsync(request, ct);

        var alreadyAdmitted = await _db.Admissions
            .AnyAsync(a => a.PatientId == patient.Id && !ClosedStatuses.Contains(a.Status), ct);

        if (alreadyAdmitted)
        {
            throw new ConflictException(MessageCode.PatientHasOpenAdmission, patient.FullName);
        }

        // destination_ward_type_hint is a routing hint only - it has nowhere to be stored and
        // never becomes admission_category on its own. A human sets that via /classify.
        var admission = new AdmissionEntity
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Source = AdmissionSource.Emergency,
            Category = null,
            Urgency = request.Urgency,

            Status = AdmissionStatus.AwaitingBed,

            IsInfectious = false,
            CategorySetByStaffMemberId = null,
            CategorySetAt = null,
            DispatchId = request.DispatchId.Trim(),
            ExpectedArrivalAt = request.ExpectedArrival,
            ReportedByUserId = request.PatientIsCaller ? null : request.CallerUserId,
            MissingFields = PatientDetailChecklist.MissingFor(patient)
        };

        _db.Admissions.Add(admission);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(exception, AdmissionConfiguration.OpenAdmissionUniqueIndex))
        {
            throw new ConflictException(MessageCode.PatientHasOpenAdmission, patient.FullName);
        }

        admission.Patient = patient;

        return await FillAsync(new AdmissionResponse(), admission, BedLabel.None, ct);
    }

    public async Task<AdmissionResponse> ClassifyAsync(
        Guid id, ClassifyAdmissionRequest request, Guid staffId, CancellationToken ct = default)
    {
        var admission = await _db.Admissions
            .Include(a => a.Patient)
            .Include(a => a.BedAssignments)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("Admission", id);

        if (admission.Category is not null)
        {
            throw new ConflictException(MessageCode.AdmissionAlreadyClassified, id);
        }

        BedPlacementRules.EnsureMayHaveCategory(request.AdmissionCategory, admission.Patient.Gender);

        admission.Category = request.AdmissionCategory;
        admission.CategorySetByStaffMemberId = staffId;
        admission.CategorySetAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        return await FillAsync(
            new AdmissionResponse(), admission, await LabelBedsAsync(new[] { admission }, ct), ct);
    }

    private async Task<PatientEntity> NewProvisionalPatientAsync(
        PreAdmitRequest request, CancellationToken ct)
    {
        PatientEntity? contact = request.CallerUserId is { } callerUserId
            ? await _db.Patients.FirstOrDefaultAsync(p => p.UserAccountId == callerUserId, ct)
            : null;

        var patient = new PatientEntity
        {
            Id = Guid.NewGuid(),
            PatientCode = PatientCodes.Next(),
            FullName = Clean(request.ProvisionalName) ?? "Unknown patient",
            Gender = request.ProvisionalGender ?? Gender.Unknown,
            EmergencyContactName = contact?.FullName,
            EmergencyContactPhone = contact?.Phone
        };

        _db.Patients.Add(patient);

        for (var attempt = 1; ; attempt++)
        {
            patient.TempReference = await NextTempReferenceAsync(ct);

            try
            {
                await _db.SaveChangesAsync(ct);
                return patient;
            }
            catch (DbUpdateException exception)
                when (attempt < PatientSaveAttempts
                      && IsUniqueViolation(exception, PatientConfiguration.TempReferenceUniqueIndex))
            {
            }
            catch (DbUpdateException exception)
                when (attempt < PatientSaveAttempts
                      && IsUniqueViolation(exception, PatientConfiguration.PatientCodeUniqueIndex))
            {
                patient.PatientCode = PatientCodes.Next();
            }
        }
    }

    private async Task<string> NextTempReferenceAsync(CancellationToken ct)
    {
        var year = DateTimeOffset.UtcNow.Year;
        var prefix = $"UNKNOWN-{year}-";

        var used = await _db.Patients
            .IgnoreQueryFilters()
            .Where(p => p.TempReference != null && p.TempReference.StartsWith(prefix))
            .Select(p => p.TempReference!)
            .ToListAsync(ct);

        var highest = used
            .Select(reference => int.TryParse(reference[prefix.Length..], out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        if (highest >= 9999)
        {
            throw new ConflictException(MessageCode.TempReferenceExhausted);
        }

        return $"{prefix}{highest + 1:D4}";
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

        PatientIdentityRules.EnsureMayChange(
            patient,
            Clean(request.FullName) ?? patient.FullName,
            nic ?? patient.Nic,
            patient.Gender,
            _currentUser.Role);

        if (nic is not null && nic != patient.Nic)
        {
            var takenBy = await _db.Patients
                .Where(p => p.Nic == nic && p.Id != patient.Id)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct);

            if (takenBy is not null)
            {
                throw new ConflictException(MessageCode.PatientNicTaken, nic, takenBy);
            }
        }

        patient.Nic = nic ?? patient.Nic;
        patient.FullName = Clean(request.FullName) ?? patient.FullName;
        patient.DateOfBirth = request.DateOfBirth ?? patient.DateOfBirth;
        patient.Phone = Clean(request.Phone) ?? patient.Phone;
        patient.Address = Clean(request.Address) ?? patient.Address;
        patient.EmergencyContactName = Clean(request.EmergencyContactName) ?? patient.EmergencyContactName;
        patient.EmergencyContactPhone = Clean(request.EmergencyContactPhone) ?? patient.EmergencyContactPhone;

        admission.MissingFields = PatientDetailChecklist.MissingFor(patient);

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

    public Task<AdmissionResponse> MarkArrivedAsync(Guid id, CancellationToken ct = default)
        => InTransitionAsync(id, admission =>
        {
            AdmissionStatusMachine.EnsureMove(
                admission.Status, AdmissionStatus.Admitted, AdmissionStatus.BedReserved);

            admission.Status = AdmissionStatus.Admitted;
            admission.AdmittedAt = DateTimeOffset.UtcNow;

            var hold = admission.BedAssignments
                .FirstOrDefault(b => b.Status == AssignmentStatus.Reserved);

            if (hold is not null)
            {
                hold.Status = AssignmentStatus.Occupied;
                hold.ReservedUntil = null;

                hold.OccupiedAt = admission.AdmittedAt;
            }
        }, ct);

    public Task<AdmissionResponse> CancelAsync(
        Guid id, CancelAdmissionRequest request, CancellationToken ct = default)
        => InTransitionAsync(id, admission =>
        {
            AdmissionStatusMachine.EnsureMove(
                admission.Status,
                AdmissionStatus.Cancelled,
                AdmissionStatus.AwaitingBed,
                AdmissionStatus.AwaitingApproval,
                AdmissionStatus.BedReserved);

            admission.Status = AdmissionStatus.Cancelled;
            admission.CancelReason = request.Reason!.Value;
            admission.CancelNote = Clean(request.Note);

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

    private async Task<AdmissionResponse> InTransitionAsync(
        Guid id, Action<AdmissionEntity> change, CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        await _db.Database.ExecuteSqlAsync(
            $"SELECT id FROM admissions WHERE id = {id} FOR UPDATE", ct);

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

    private static AdmissionSummary ToSummary(
        AdmissionEntity admission,
        IReadOnlyDictionary<Guid, BedLabel> beds,
        bool includePatient)
    {
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

            WardName = label.WardName,
            BedNumber = label.BedNumber,

            Status = assignment.Status,
            ReservedUntil = assignment.ReservedUntil,
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

    private static BedAssignmentEntity? LiveAssignment(AdmissionEntity admission)
    {
        var now = DateTimeOffset.UtcNow;

        var live = admission.BedAssignments.FirstOrDefault(
            assignment => BedHold.IsLive(assignment, now));

        if (live is not null)
        {
            return live;
        }

        if (admission.Status is not (AdmissionStatus.Discharged or AdmissionStatus.Cancelled))
        {
            return null;
        }

        return admission.BedAssignments
            .OrderByDescending(assignment => assignment.ReleasedAt ?? DateTimeOffset.MinValue)
            .ThenByDescending(assignment => assignment.OccupiedAt ?? assignment.CreatedAt)
            .FirstOrDefault();
    }

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
