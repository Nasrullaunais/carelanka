using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Configurations.Patient;
using CareLanka.Api.Data.Entities.Patient;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using AdmissionEntity = CareLanka.Api.Data.Entities.Patient.Admission;
using PatientEntity = CareLanka.Api.Data.Entities.Patient.Patient;
using PatientResponse = CareLanka.Api.DTOs.Patient.Patient;

namespace CareLanka.Api.Services.Patient;

public sealed class PatientService : IPatientService
{
    private static readonly AdmissionStatus[] ClosedStatuses =
    [
        AdmissionStatus.Discharged,
        AdmissionStatus.Cancelled
    ];

    private const int SaveAttempts = 5;

    private readonly CareLankaDbContext _db;

    public PatientService(CareLankaDbContext db) => _db = db;

    public async Task<PagedResult<PatientSummary>> ListAsync(
        string? search,
        int page,
        int pageSize,
        PatientSortField sortBy,
        SortDirection sortDir,
        CancellationToken ct = default)
    {
        var query = _db.Patients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";

            query = query.Where(p =>
                EF.Functions.ILike(p.FullName, pattern)
                || EF.Functions.ILike(p.PatientCode, pattern)
                || (p.Nic != null && EF.Functions.ILike(p.Nic, pattern))
                || (p.Phone != null && EF.Functions.ILike(p.Phone, pattern))
                || (p.TempReference != null && EF.Functions.ILike(p.TempReference, pattern)));
        }

        var totalItems = await query.CountAsync(ct);

        var sorted = (sortBy, sortDir) switch
        {
            (PatientSortField.FullName, SortDirection.Asc) => query.OrderBy(p => p.FullName),
            (PatientSortField.FullName, SortDirection.Desc) => query.OrderByDescending(p => p.FullName),
            (_, SortDirection.Asc) => query.OrderBy(p => p.CreatedAt),
            _ => query.OrderByDescending(p => p.CreatedAt)
        };

        var patients = await sorted
            .ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedResult<PatientSummary>.From(
            patients.Select(ToSummary).ToList(), page, pageSize, totalItems);
    }

    public async Task<PatientDetail> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var patient = await _db.Patients
            .AsNoTracking()
            .Include(p => p.Admissions)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Patient", id);

        var detail = new PatientDetail
        {
            Admissions = patient.Admissions
                .OrderByDescending(a => a.CreatedAt)
                .Select(ToAdmissionSummary)
                .ToList()
        };

        return Fill(detail, patient);
    }

    public async Task<PatientResponse> CreateAsync(
        CreatePatientRequest request, CancellationToken ct = default)
    {
        var nic = Clean(request.Nic);
        var phone = Clean(request.Phone);

        if (nic is not null)
        {
            var existingId = await _db.Patients
                .Where(p => p.Nic == nic)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct);

            if (existingId is not null)
            {
                throw new ConflictException(MessageCode.PatientNicTaken, nic, existingId);
            }
        }

        var patient = new PatientEntity
        {
            Id = Guid.NewGuid(),

            PatientCode = PatientCodes.Next(),
            FullName = request.FullName.Trim(),
            Nic = nic,

            Gender = request.Gender!.Value,
            DateOfBirth = request.DateOfBirth,
            Phone = phone,
            Address = Clean(request.Address),
            EmergencyContactName = Clean(request.EmergencyContactName),
            EmergencyContactPhone = Clean(request.EmergencyContactPhone)
        };

        _db.Patients.Add(patient);

        await SaveWithIdentifierAsync(patient, nic, ct);

        return Fill(new PatientResponse(), patient);
    }

    public async Task<PatientResponse> UpdateAsync(
        Guid id, UpdatePatientRequest request, CancellationToken ct = default)
    {
        var patient = await GetByIdAsync(id, ct);

        var nic = Clean(request.Nic);
        var phone = Clean(request.Phone);

        if (nic is not null && nic != patient.Nic)
        {
            var existingId = await _db.Patients
                .Where(p => p.Nic == nic && p.Id != id)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct);

            if (existingId is not null)
            {
                throw new ConflictException(MessageCode.PatientNicTaken, nic, existingId);
            }
        }

        patient.FullName = request.FullName.Trim();
        patient.Nic = nic;
        patient.Gender = request.Gender!.Value;
        patient.DateOfBirth = request.DateOfBirth;
        patient.Phone = phone;
        patient.Address = Clean(request.Address);
        patient.EmergencyContactName = Clean(request.EmergencyContactName);
        patient.EmergencyContactPhone = Clean(request.EmergencyContactPhone);

        await SaveWithIdentifierAsync(patient, nic, ct);

        return Fill(new PatientResponse(), patient);
    }

    public async Task<PatientLookupResult> LookupByNicAsync(string nic, CancellationToken ct = default)
    {
        var trimmed = nic.Trim();

        var patient = await _db.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Nic == trimmed, ct);

        if (patient is null)
        {
            return new PatientLookupResult { Found = false };
        }

        var hasOpenAdmission = await _db.Admissions
            .AnyAsync(a => a.PatientId == patient.Id && !ClosedStatuses.Contains(a.Status), ct);

        return new PatientLookupResult
        {
            Found = true,
            Patient = ToSummary(patient),
            HasOpenAdmission = hasOpenAdmission
        };
    }

    public async Task LinkAccountAsync(Guid id, Guid userAccountId, CancellationToken ct = default)
    {
        var patient = await GetByIdAsync(id, ct);

        if (patient.UserAccountId == userAccountId)
        {
            return;
        }

        if (patient.UserAccountId is not null)
        {
            throw new ConflictException(MessageCode.PatientAlreadyHasAccount);
        }

        var accountExists = await _db.PatientAccounts
            .AnyAsync(a => a.Id == userAccountId, ct);

        if (!accountExists)
        {
            throw new NotFoundException("PatientAccount", userAccountId);
        }

        patient.UserAccountId = userAccountId;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(exception, PatientConfiguration.UserAccountUniqueIndex))
        {
            throw new ConflictException(MessageCode.AccountAlreadyLinked);
        }
    }

    public Task<PatientEntity?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Patients.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<PatientEntity> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await FindByIdAsync(id, ct) ?? throw new NotFoundException("Patient", id);

    private async Task SaveWithIdentifierAsync(
        PatientEntity patient, string? nic, CancellationToken ct)
    {
        var needsTempReference =
            nic is null && patient.Phone is null && patient.TempReference is null;

        for (var attempt = 1; ; attempt++)
        {
            if (needsTempReference)
            {
                patient.TempReference = await NextTempReferenceAsync(ct);
            }

            try
            {
                await _db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException exception)
                when (IsUniqueViolation(exception, PatientConfiguration.NicUniqueIndex))
            {
                throw new ConflictException(MessageCode.PatientNicTaken, nic, "another record");
            }
            catch (DbUpdateException exception)
                when (needsTempReference
                      && attempt < SaveAttempts
                      && IsUniqueViolation(exception, PatientConfiguration.TempReferenceUniqueIndex))
            {
            }
            catch (DbUpdateException exception)
                when (attempt < SaveAttempts
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

    private static PatientSummary ToSummary(PatientEntity patient)
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

    private static AdmissionSummary ToAdmissionSummary(AdmissionEntity admission)
        => new()
        {
            Id = admission.Id,
            Source = admission.Source,
            AdmissionCategory = admission.Category,
            Urgency = admission.Urgency,
            Status = admission.Status,
            DetailsComplete = admission.DetailsComplete,

            Patient = null,

            WardName = null,
            BedNumber = null,

            ExpectedArrival = admission.ExpectedArrivalAt,
            AdmittedAt = admission.AdmittedAt
        };

    private static TResponse Fill<TResponse>(TResponse response, PatientEntity patient)
        where TResponse : PatientResponse
    {
        response.Id = patient.Id;
        response.PatientCode = patient.PatientCode;
        response.FullName = patient.FullName;
        response.Nic = patient.Nic;
        response.TempReference = patient.TempReference;
        response.Gender = patient.Gender;
        response.DateOfBirth = patient.DateOfBirth;
        response.Phone = patient.Phone;
        response.Address = patient.Address;
        response.EmergencyContactName = patient.EmergencyContactName;
        response.EmergencyContactPhone = patient.EmergencyContactPhone;
        response.HasAccount = patient.UserAccountId is not null;
        response.CreatedAt = patient.CreatedAt;
        response.UpdatedAt = patient.UpdatedAt;

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
