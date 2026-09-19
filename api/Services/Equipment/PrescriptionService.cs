using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Configurations.Equipment;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PrescriptionEntity = CareLanka.Api.Data.Entities.Equipment.Prescription;

namespace CareLanka.Api.Services.Equipment;

public sealed class PrescriptionService : IPrescriptionService
{
    public const int MaxBytes = 10 * 1024 * 1024;

    // A closed history list is capped so a year of deliveries is not one response. The open
    // lists - waiting and ready - are the pharmacy's work and are never cut short.
    public const int ClosedListLimit = 100;

    private const int TokenAttempts = 5;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png"
    };

    private readonly CareLankaDbContext _db;
    private readonly IPatientDirectory _patients;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public PrescriptionService(
        CareLankaDbContext db,
        IPatientDirectory patients,
        ICurrentUser currentUser,
        TimeProvider clock)
    {
        _db = db;
        _patients = patients;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<IReadOnlyList<MyPrescription>> ListMineAsync(
        CancellationToken cancellationToken = default)
    {
        var patientId = await MyPatientIdAsync(cancellationToken);

        return await _db.Prescriptions.AsNoTracking()
            .Where(p => p.PatientId == patientId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new MyPrescription
            {
                Id = p.Id,
                Note = p.Note,
                FileName = p.FileName,
                ContentType = p.ContentType,
                ByteSize = p.ByteSize,
                Status = p.Status,
                TokenDate = p.TokenDate,
                TokenNumber = p.TokenNumber,
                ReadyAt = p.ReadyAt,
                DeliveredAt = p.DeliveredAt,
                RejectionReason = p.RejectionReason,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<MyPrescription> UploadMineAsync(
        UploadPrescriptionRequest request, CancellationToken cancellationToken = default)
    {
        var patientId = await MyPatientIdAsync(cancellationToken);
        var file = request.File!;

        if (file.Length <= 0)
        {
            throw new BadRequestException(MessageCode.PrescriptionFileEmpty);
        }

        if (file.Length > MaxBytes)
        {
            throw new BadRequestException(MessageCode.PrescriptionFileTooLarge, MaxBytes / (1024 * 1024));
        }

        var contentType = file.ContentType?.Split(';')[0].Trim() ?? string.Empty;

        if (!AllowedContentTypes.Contains(contentType))
        {
            throw new BadRequestException(MessageCode.PrescriptionFileType, contentType);
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);

        var prescription = new PrescriptionEntity
        {
            Id = Guid.NewGuid(),
            // From the token, never the form: a patient cannot send one in somebody else's name.
            PatientId = patientId,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            FileName = Path.GetFileName(file.FileName),
            ContentType = contentType,
            Content = buffer.ToArray(),
            ByteSize = (int)file.Length,
            Status = PrescriptionStatus.Submitted
        };

        _db.Prescriptions.Add(prescription);
        await _db.SaveChangesAsync(cancellationToken);

        return ToMine(prescription);
    }

    public async Task<IReadOnlyList<Prescription>> ListAsync(
        PrescriptionStatus status, CancellationToken cancellationToken = default)
    {
        var query = _db.Prescriptions.AsNoTracking().Where(p => p.Status == status);

        // Oldest first while there is work to do, so nobody who sent theirs early is skipped;
        // newest first once it is history.
        query = status is PrescriptionStatus.Submitted or PrescriptionStatus.Ready
            ? query.OrderBy(p => p.CreatedAt)
            : query.OrderByDescending(p => p.UpdatedAt).Take(ClosedListLimit);

        // Projected without Content, so listing does not read every stored photo.
        var rows = await query
            .Select(p => new PrescriptionEntity
            {
                Id = p.Id,
                PatientId = p.PatientId,
                Note = p.Note,
                FileName = p.FileName,
                ContentType = p.ContentType,
                ByteSize = p.ByteSize,
                Status = p.Status,
                TokenDate = p.TokenDate,
                TokenNumber = p.TokenNumber,
                ReadyAt = p.ReadyAt,
                DeliveredAt = p.DeliveredAt,
                RejectionReason = p.RejectionReason,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var patients = await _patients.GetSummariesAsync(
            rows.Select(p => p.PatientId).Distinct().ToList(), cancellationToken);

        return rows.Select(p => ToStaff(p, patients)).ToList();
    }

    public async Task<LabReportFile> GetFileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var prescription = await _db.Prescriptions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("Prescription", id);

        return new LabReportFile(prescription.Content, prescription.ContentType, prescription.FileName);
    }

    public async Task<Prescription> MarkReadyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var prescription = await GetInStatusAsync(id, "ready", cancellationToken, PrescriptionStatus.Submitted);
        var now = _clock.GetUtcNow();
        // Tokens restart each day at the hospital, not at midnight UTC.
        var today = HospitalTime.Today(now);

        prescription.Status = PrescriptionStatus.Ready;
        prescription.ReadyAt = now;
        prescription.ReadyByStaffId = _currentUser.Id;
        prescription.TokenDate = today;

        // Two pharmacists pressing Ready at once would both read the same highest number. The
        // unique index turns that into a failed save, and the loser simply takes the next one.
        for (var attempt = 1; ; attempt++)
        {
            prescription.TokenNumber = 1 + (await _db.Prescriptions
                .Where(p => p.TokenDate == today && p.TokenNumber != null)
                .MaxAsync(p => p.TokenNumber, cancellationToken) ?? 0);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateException error) when (attempt < TokenAttempts && IsTokenClash(error))
            {
            }
        }

        return await ToStaffAsync(prescription, cancellationToken);
    }

    public async Task<Prescription> MarkDeliveredAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var prescription = await GetInStatusAsync(
            id, "delivered", cancellationToken, PrescriptionStatus.Ready);

        prescription.Status = PrescriptionStatus.Delivered;
        prescription.DeliveredAt = _clock.GetUtcNow();
        prescription.DeliveredByStaffId = _currentUser.Id;

        await _db.SaveChangesAsync(cancellationToken);

        return await ToStaffAsync(prescription, cancellationToken);
    }

    public async Task<Prescription> RejectAsync(
        Guid id, string reason, CancellationToken cancellationToken = default)
    {
        var prescription = await GetInStatusAsync(
            id, "rejected", cancellationToken, PrescriptionStatus.Submitted, PrescriptionStatus.Ready);

        prescription.Status = PrescriptionStatus.Rejected;
        prescription.RejectionReason = reason.Trim();
        prescription.RejectedAt = _clock.GetUtcNow();
        prescription.RejectedByStaffId = _currentUser.Id;

        await _db.SaveChangesAsync(cancellationToken);

        return await ToStaffAsync(prescription, cancellationToken);
    }

    private async Task<Guid> MyPatientIdAsync(CancellationToken cancellationToken)
        => await _patients.FindPatientIdForAccountAsync(_currentUser.Id, cancellationToken)
           ?? throw new NotFoundException(MessageCode.AccountHasNoPatientRecord);

    private async Task<PrescriptionEntity> GetInStatusAsync(
        Guid id, string wanted, CancellationToken cancellationToken, params PrescriptionStatus[] allowed)
    {
        var prescription = await _db.Prescriptions
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("Prescription", id);

        if (!allowed.Contains(prescription.Status))
        {
            throw new ConflictException(
                MessageCode.PrescriptionWrongStatus, EnumWire.ToWire(prescription.Status), wanted);
        }

        return prescription;
    }

    private static bool IsTokenClash(DbUpdateException error)
        => error.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: PrescriptionConfiguration.TokenUniqueIndex
        };

    private async Task<Prescription> ToStaffAsync(
        PrescriptionEntity prescription, CancellationToken cancellationToken)
    {
        var patients = await _patients.GetSummariesAsync(new[] { prescription.PatientId }, cancellationToken);

        return ToStaff(prescription, patients);
    }

    private static Prescription ToStaff(
        PrescriptionEntity p, IReadOnlyDictionary<Guid, PatientSummaryName> patients)
    {
        var patient = patients.GetValueOrDefault(p.PatientId);

        return new Prescription
        {
            Id = p.Id,
            PatientId = p.PatientId,
            PatientCode = patient?.PatientCode ?? string.Empty,
            PatientName = patient?.FullName ?? "Unknown patient",
            Note = p.Note,
            FileName = p.FileName,
            ContentType = p.ContentType,
            ByteSize = p.ByteSize,
            Status = p.Status,
            TokenDate = p.TokenDate,
            TokenNumber = p.TokenNumber,
            ReadyAt = p.ReadyAt,
            DeliveredAt = p.DeliveredAt,
            RejectionReason = p.RejectionReason,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
    }

    private static MyPrescription ToMine(PrescriptionEntity p)
        => new()
        {
            Id = p.Id,
            Note = p.Note,
            FileName = p.FileName,
            ContentType = p.ContentType,
            ByteSize = p.ByteSize,
            Status = p.Status,
            TokenDate = p.TokenDate,
            TokenNumber = p.TokenNumber,
            ReadyAt = p.ReadyAt,
            DeliveredAt = p.DeliveredAt,
            RejectionReason = p.RejectionReason,
            CreatedAt = p.CreatedAt
        };
}
