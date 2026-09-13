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
    private static readonly AppointmentStatus[] OpenStatuses = [AppointmentStatus.Scheduled];

    private static readonly AdmissionStatus[] ClosedAdmissionStatuses =
    [
        AdmissionStatus.Discharged,
        AdmissionStatus.Cancelled
    ];

    private static readonly AdmissionCategory[] DutyManagerOnly =
    [
        AdmissionCategory.Icu,
        AdmissionCategory.Hdu
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

        if (appointment.Status != AppointmentStatus.Scheduled)
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

        if (appointment.Status != AppointmentStatus.Scheduled)
        {
            throw new IllegalTransitionException(
                "Appointment",
                EnumWire.ToWire(appointment.Status),
                EnumWire.ToWire(AppointmentStatus.CheckedIn));
        }

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

        appointment.Status = AppointmentStatus.CheckedIn;
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
            CreatedAt = appointment.CreatedAt,
            UpdatedAt = appointment.UpdatedAt
        };

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
