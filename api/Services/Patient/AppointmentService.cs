using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using AdmissionResponse = CareLanka.Api.DTOs.Patient.Admission;
using AppointmentEntity = CareLanka.Api.Data.Entities.Patient.Appointment;
using AppointmentResponse = CareLanka.Api.DTOs.Patient.Appointment;
using PatientEntity = CareLanka.Api.Data.Entities.Patient.Patient;

namespace CareLanka.Api.Services.Patient;

public sealed class AppointmentService : IAppointmentService
{
    private static readonly AppointmentStatus[] OpenStatuses =
    [
        AppointmentStatus.Scheduled,
        AppointmentStatus.Confirmed
    ];

    private static readonly AdmissionStatus[] ClosedAdmissionStatuses =
    [
        AdmissionStatus.Discharged,
        AdmissionStatus.Cancelled
    ];

    private static readonly AdmissionCategory[] DutyManagerOnly =
    [
        AdmissionCategory.Icu
    ];

    private readonly CareLankaDbContext _db;
    private readonly IAdmissionService _admissions;
    private readonly ICurrentUser _currentUser;

    public AppointmentService(
        CareLankaDbContext db, IAdmissionService admissions, ICurrentUser currentUser)
    {
        _db = db;
        _admissions = admissions;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<AppointmentResponse>> ListAsync(
        DateOnly? date,
        AppointmentStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.Appointments.AsNoTracking().Include(a => a.Patient).AsQueryable();

        if (date is { } day)
        {
            var from = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var to = from.AddDays(1);

            query = query.Where(a => a.ScheduledAt >= from && a.ScheduledAt < to);
        }

        if (status is { } wanted)
        {
            query = query.Where(a => a.Status == wanted);
        }

        var totalItems = await query.CountAsync(ct);

        var appointments = await query
            .OrderBy(a => a.ScheduledAt)
            .ThenBy(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return PagedResult<AppointmentResponse>.From(
            appointments.Select(ToResponse).ToList(), page, pageSize, totalItems);
    }

    public async Task<AppointmentResponse> CreateAsync(
        CreateAppointmentRequest request, CancellationToken ct = default)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == request.PatientId, ct)
            ?? throw new NotFoundException("Patient", request.PatientId);

        var appointment = await BookAsync(
            patient, request.ScheduledAt, request.Reason, _currentUser.Id, ct);

        return ToResponse(appointment);
    }

    public async Task<AppointmentEntity> BookForPatientAsync(
        Guid patientId, BookAppointmentRequest request, CancellationToken ct = default)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, ct)
            ?? throw new NotFoundException("Patient", patientId);

        return await BookAsync(patient, request.ScheduledAt, request.Reason, null, ct);
    }

    public async Task<AppointmentEntity> CancelForPatientAsync(
        Guid appointmentId, Guid patientId, CancellationToken ct = default)
    {
        var appointment = await _db.Appointments
            .FirstOrDefaultAsync(a => a.Id == appointmentId && a.PatientId == patientId, ct)
            ?? throw new NotFoundException("Appointment", appointmentId);

        if (!OpenStatuses.Contains(appointment.Status))
        {
            throw new IllegalTransitionException(
                "Appointment",
                EnumWire.ToWire(appointment.Status),
                EnumWire.ToWire(AppointmentStatus.Cancelled));
        }

        appointment.Status = AppointmentStatus.Cancelled;

        await _db.SaveChangesAsync(ct);

        return appointment;
    }

    /// <summary>
    /// The desk reads the booking and accepts it. Nothing physical has happened - the patient
    /// may not be due for weeks - which is exactly why this is not the same click as admitting
    /// them. Everything the desk does on the day needs a confirmed booking first.
    /// </summary>
    public async Task<AppointmentResponse> ConfirmAsync(Guid id, CancellationToken ct = default)
    {
        var appointment = await OpenAsync(id, ct);

        if (appointment.Status != AppointmentStatus.Scheduled)
        {
            throw new IllegalTransitionException(
                "Appointment",
                EnumWire.ToWire(appointment.Status),
                EnumWire.ToWire(AppointmentStatus.Confirmed));
        }

        appointment.Status = AppointmentStatus.Confirmed;
        appointment.ConfirmedAt = DateTimeOffset.UtcNow;
        appointment.ConfirmedByStaffMemberId = _currentUser.Id;

        await _db.SaveChangesAsync(ct);

        return ToResponse(appointment);
    }

    /// <summary>
    /// They were expected, the time has passed and they never came. A separate ending from a
    /// cancellation, which somebody chose.
    /// </summary>
    public async Task<AppointmentResponse> MarkNoShowAsync(
        Guid id, CancellationToken ct = default)
    {
        var appointment = await OpenAsync(id, ct);

        EnsureConfirmed(appointment, AppointmentStatus.NoShow);

        appointment.Status = AppointmentStatus.NoShow;

        await _db.SaveChangesAsync(ct);

        return ToResponse(appointment);
    }

    public async Task<AppointmentResponse> CancelAtTheDeskAsync(
        Guid id, CancelAppointmentRequest request, CancellationToken ct = default)
    {
        var appointment = await OpenAsync(id, ct);

        if (!OpenStatuses.Contains(appointment.Status))
        {
            throw new IllegalTransitionException(
                "Appointment",
                EnumWire.ToWire(appointment.Status),
                EnumWire.ToWire(AppointmentStatus.Cancelled));
        }

        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancellationReason = request.Reason.Trim();
        appointment.CancelledByStaffMemberId = _currentUser.Id;

        await _db.SaveChangesAsync(ct);

        return ToResponse(appointment);
    }

    /// <summary>
    /// The patient came in, was seen, and went home. No admission and no bed, so the only thing
    /// left is the bill, and it is raised against this appointment.
    /// </summary>
    public async Task<AppointmentResponse> CompleteAsync(Guid id, CancellationToken ct = default)
    {
        var appointment = await OpenAsync(id, ct);

        EnsureConfirmed(appointment, AppointmentStatus.Completed);

        appointment.Status = AppointmentStatus.Completed;

        await _db.SaveChangesAsync(ct);

        return ToResponse(appointment);
    }

    private async Task<AppointmentEntity> OpenAsync(Guid id, CancellationToken ct)
        => await _db.Appointments
            .Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("Appointment", id);

    /// <summary>
    /// The three day-of endings - admitted, seen, never came - all need the desk to have read
    /// the booking first. Confirming is the one step that is never skipped.
    /// </summary>
    private static void EnsureConfirmed(AppointmentEntity appointment, AppointmentStatus wanted)
    {
        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            throw new IllegalTransitionException(
                "Appointment",
                EnumWire.ToWire(appointment.Status),
                EnumWire.ToWire(wanted));
        }
    }

    private async Task<AppointmentEntity> BookAsync(
        PatientEntity patient,
        DateTimeOffset? requestedAt,
        string? reason,
        Guid? bookedByStaffMemberId,
        CancellationToken ct)
    {
        var scheduledAt = requestedAt!.Value.ToUniversalTime();

        if (scheduledAt <= DateTimeOffset.UtcNow)
        {
            throw new BadRequestException(MessageCode.AppointmentInThePast);
        }

        await EnsureNothingOpenForAsync(patient, ct);

        var appointment = new AppointmentEntity
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            ScheduledAt = scheduledAt,
            Status = AppointmentStatus.Scheduled,
            Reason = Clean(reason),
            BookedByStaffMemberId = bookedByStaffMemberId
        };

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync(ct);

        appointment.Patient = patient;

        return appointment;
    }

    public async Task<AdmissionResponse> CheckInAsync(
        Guid id, CheckInRequest request, CancellationToken ct = default)
    {
        var category = request.AdmissionCategory!.Value;

        if (DutyManagerOnly.Contains(category) && _currentUser.Role != PrincipalRole.DutyManager)
        {
            throw new ForbiddenException(
                MessageCode.CareLevelNeedsDutyManager, EnumWire.ToWire(category));
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        await _db.Database.ExecuteSqlAsync(
            $"SELECT id FROM appointments WHERE id = {id} FOR UPDATE", ct);

        var appointment = await _db.Appointments
            .Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("Appointment", id);

        EnsureConfirmed(appointment, AppointmentStatus.Completed);

        var admission = await _admissions.CreateAsync(new CreateAdmissionRequest
        {
            PatientId = appointment.PatientId,

            Source = AdmissionSource.PreRegistered,

            AdmissionCategory = category,
            CategorySetByStaffId = request.CategorySetByStaffId,
            Urgency = request.Urgency!.Value,
            IsInfectious = request.IsInfectious,

            ExpectedArrival = null
        }, ct);

        // Over as a booking either way. AdmissionId is what says they stayed in rather than
        // went home, and it is what stops a second bill being raised here.
        appointment.Status = AppointmentStatus.Completed;
        appointment.AdmissionId = admission.Id;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return admission;
    }

    public Task<AppointmentEntity?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Appointments.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<AppointmentEntity> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await FindByIdAsync(id, ct) ?? throw new NotFoundException("Appointment", id);

    private async Task EnsureNothingOpenForAsync(PatientEntity patient, CancellationToken ct)
    {
        var alreadyBooked = await _db.Appointments
            .AnyAsync(a => a.PatientId == patient.Id && OpenStatuses.Contains(a.Status), ct);

        if (alreadyBooked)
        {
            throw new ConflictException(MessageCode.PatientHasOpenAppointment, patient.FullName);
        }

        var alreadyAdmitted = await _db.Admissions
            .AnyAsync(a => a.PatientId == patient.Id
                && !ClosedAdmissionStatuses.Contains(a.Status), ct);

        if (alreadyAdmitted)
        {
            throw new ConflictException(MessageCode.PatientHasOpenAdmission, patient.FullName);
        }
    }

    private static AppointmentResponse ToResponse(AppointmentEntity appointment)
        => new()
        {
            Id = appointment.Id,
            Patient = new PatientSummary
            {
                Id = appointment.Patient.Id,
                PatientCode = appointment.Patient.PatientCode,
                FullName = appointment.Patient.FullName,
                Nic = appointment.Patient.Nic,
                TempReference = appointment.Patient.TempReference,
                Gender = appointment.Patient.Gender,
                DateOfBirth = appointment.Patient.DateOfBirth
            },
            ScheduledAt = appointment.ScheduledAt,
            Status = appointment.Status,
            Reason = appointment.Reason,
            BookedByStaffId = appointment.BookedByStaffMemberId,
            AdmissionId = appointment.AdmissionId,
            CancellationReason = appointment.CancellationReason,
            CancelledByStaffId = appointment.CancelledByStaffMemberId,
            ConfirmedAt = appointment.ConfirmedAt,
            ConfirmedByStaffId = appointment.ConfirmedByStaffMemberId,
            CanConfirm = appointment.Status == AppointmentStatus.Scheduled,
            CanCancel = OpenStatuses.Contains(appointment.Status),
            CanComplete = appointment.Status == AppointmentStatus.Confirmed,
            CreatedAt = appointment.CreatedAt,
            UpdatedAt = appointment.UpdatedAt
        };

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
