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

    public AdmissionService(CareLankaDbContext db) => _db = db;

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
        var query = _db.Admissions.AsNoTracking().Include(a => a.Patient).AsQueryable();

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

        return PagedResult<AdmissionSummary>.From(
            admissions.Select(a => ToSummary(a, includePatient: true)).ToList(),
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

        var detail = new AdmissionDetail
        {
            // Newest first, and nothing is filtered out: a rejected or expired assignment is
            // part of the audit trail, not noise.
            BedAssignments = admission.BedAssignments
                .OrderByDescending(b => b.CreatedAt)
                .Select(ToBedAssignment)
                .ToList()
        };

        return Fill(detail, admission);
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
            Source = request.Source,
            Category = request.AdmissionCategory,
            Urgency = request.Urgency,

            // Every admission starts here. Getting a bed is a separate, approved step.
            Status = AdmissionStatus.AwaitingBed,

            IsInfectious = request.IsInfectious,
            CategorySetByStaffMemberId = request.CategorySetByStaffId,
            CategorySetAt = DateTimeOffset.UtcNow,
            DispatchId = string.IsNullOrWhiteSpace(request.DispatchId) ? null : request.DispatchId.Trim(),
            ExpectedArrivalAt = request.ExpectedArrival,
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

        return Fill(new AdmissionResponse(), admission);
    }

    public async Task<AdmissionResponse> CompleteDetailsAsync(
        Guid id, CompleteDetailsRequest request, CancellationToken ct = default)
    {
        var admission = await _db.Admissions
            .Include(a => a.Patient)
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

        return Fill(new AdmissionResponse(), admission);
    }

    public Task<AdmissionEntity?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Admissions.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<AdmissionEntity> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await FindByIdAsync(id, ct) ?? throw new NotFoundException("Admission", id);

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

    private static AdmissionSummary ToSummary(AdmissionEntity admission, bool includePatient)
        => new()
        {
            Id = admission.Id,
            Patient = includePatient ? ToPatientSummary(admission.Patient) : null,
            Source = admission.Source,
            AdmissionCategory = admission.Category,
            Urgency = admission.Urgency,
            Status = admission.Status,
            DetailsComplete = admission.DetailsComplete,

            // Ward and bed stay null until step 6 puts a live BedAssignment behind them, and
            // reading the names needs Equipment's register — STUBS.md row 1.
            WardName = null,
            BedNumber = null,

            ExpectedArrival = admission.ExpectedArrivalAt,
            AdmittedAt = admission.AdmittedAt
        };

    private static PatientSummary ToPatientSummary(PatientEntity patient)
        => new()
        {
            Id = patient.Id,
            FullName = patient.FullName,
            Nic = patient.Nic,
            TempReference = patient.TempReference,
            Gender = patient.Gender,
            DateOfBirth = patient.DateOfBirth
        };

    private static BedAssignmentResponse ToBedAssignment(BedAssignmentEntity assignment)
        => new()
        {
            Id = assignment.Id,
            AdmissionId = assignment.AdmissionId,
            BedId = assignment.BedId,

            // STUB — the ward and bed names live in Equipment's register. See STUBS.md row 1.
            // No assignment rows exist until step 6, so this mapper does not run today.
            WardName = string.Empty,
            BedNumber = string.Empty,

            Status = assignment.Status,
            ReservedUntil = assignment.ReservedUntil,
            AssignedBy = assignment.AssignedBy,
            WorkflowId = assignment.WorkflowId,
            IsDowngrade = assignment.IsDowngrade,
            ApprovedByStaffId = assignment.ApprovedByStaffMemberId,
            ApprovedAt = assignment.ApprovedAt,
            OverrideReason = assignment.OverrideReason,
            ReleasedAt = assignment.ReleasedAt,
            ReleaseReason = assignment.ReleaseReason,
            CreatedAt = assignment.CreatedAt,
            UpdatedAt = assignment.UpdatedAt
        };

    private static TResponse Fill<TResponse>(TResponse response, AdmissionEntity admission)
        where TResponse : AdmissionResponse
    {
        var summary = ToSummary(admission, includePatient: true);

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
        response.CategorySetAt = admission.CategorySetAt;
        response.IsInfectious = admission.IsInfectious;
        response.ReportedByUserId = admission.ReportedByUserId;
        response.MissingFields = admission.MissingFields.ToList();
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
