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
    // A booking that has not been resolved yet. Everything else is finished with: checked in,
    // completed, cancelled, or they never turned up.
    private static readonly AppointmentStatus[] OpenStatuses = [AppointmentStatus.Scheduled];

    // A visit that has ended, matching AdmissionService. Anything else counts as open.
    private static readonly AdmissionStatus[] ClosedAdmissionStatuses =
    [
        AdmissionStatus.Discharged,
        AdmissionStatus.Cancelled
    ];

    // The two care levels a ward nurse may not authorise at the desk. Intensive and
    // high-dependency care are the duty manager's call wherever the admission comes from, so
    // the rule is here rather than in a policy: it depends on the body, not just the route.
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
            // Whole days in UTC, because that is what the column stores. A hospital in Colombo
            // is UTC+5:30, so "today" here starts at half past five in the morning local time.
            // Nothing in this project has a timezone yet and inventing one in a filter would
            // make this list disagree with every report - see RESUME.md, still open.
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
            // Time order, because this is a day list a receptionist reads down. Id breaks ties:
            // two bookings for the same minute otherwise land in an arbitrary order that can
            // differ between pages, so one is shown twice and another never at all.
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
        // Not null: [ApiController] has already returned a 400 for a body that left it out. It
        // is nullable on the request so that omission is an error rather than the year 1.
        var scheduledAt = request.ScheduledAt!.Value.ToUniversalTime();

        if (scheduledAt <= DateTimeOffset.UtcNow)
        {
            // Always a typo. Somebody already in the building is admitted, not booked.
            throw new BadRequestException(MessageCode.AppointmentInThePast);
        }

        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == request.PatientId, ct)
            ?? throw new NotFoundException("Patient", request.PatientId);

        await EnsureNothingOpenForAsync(patient, ct);

        var appointment = new AppointmentEntity
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            ScheduledAt = scheduledAt,
            Status = AppointmentStatus.Scheduled,
            Reason = Clean(request.Reason),

            // Off the token, never off the body. Null here would mean the patient booked it
            // themselves through the app, which is the one thing this path is not.
            BookedByStaffMemberId = _currentUser.Id
        };

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync(ct);

        appointment.Patient = patient;

        return ToResponse(appointment);
    }

    public async Task<AdmissionResponse> CheckInAsync(
        Guid id, CheckInRequest request, CancellationToken ct = default)
    {
        // Not null: [ApiController] has already rejected a body missing either enum.
        var category = request.AdmissionCategory!.Value;

        if (DutyManagerOnly.Contains(category) && _currentUser.Role != PrincipalRole.DutyManager)
        {
            // Checked before anything is read or written, so a nurse reaching for an ICU
            // check-in is refused on the rule rather than on some later side effect.
            throw new ForbiddenException(
                MessageCode.CareLevelNeedsDutyManager, EnumWire.ToWire(category));
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        // Takes the lock and nothing else, the same way AdmissionService does. Two desks
        // checking the same person in at the same instant both read `scheduled` otherwise, and
        // both pass the status check below. The second request waits here, re-reads, and gets
        // the honest 409: cannot move from checked_in to checked_in.
        //
        // Locking and loading in one composed query would put FOR UPDATE inside a join against
        // patients, which locks rows nobody asked about. A missing row locks nothing and falls
        // through to the 404.
        await _db.Database.ExecuteSqlAsync(
            $"SELECT id FROM appointments WHERE id = {id} FOR UPDATE", ct);

        var appointment = await _db.Appointments
            .Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("Appointment", id);

        if (appointment.Status != AppointmentStatus.Scheduled)
        {
            // checked_in is terminal for this row: from there the record of what happened next
            // is the Admission. Cancelled and no_show are finished with in the other direction.
            throw new IllegalTransitionException(
                "Appointment",
                EnumWire.ToWire(appointment.Status),
                EnumWire.ToWire(AppointmentStatus.CheckedIn));
        }

        // Through the admission service, not by building the entity here. That is where the
        // open-admission rule, the "does this staff member exist" check, the missing-fields
        // calculation and the catch on ux_admissions_open_patient already live — and a
        // pre-registered arrival has to obey every one of them exactly as a walk-in does.
        var admission = await _admissions.CreateAsync(new CreateAdmissionRequest
        {
            PatientId = appointment.PatientId,

            // The whole point of the endpoint: this is the third arrival path, and afterwards
            // a report can tell channeling apart from a walk-in and from an ambulance.
            Source = AdmissionSource.PreRegistered,

            AdmissionCategory = category,
            CategorySetByStaffId = request.CategorySetByStaffId,
            Urgency = request.Urgency!.Value,
            IsInfectious = request.IsInfectious,

            // They are standing at the desk. The booked time is when they said they would come,
            // not a future arrival to staff ahead of, and carrying it forward would put somebody
            // already here into next hour's incoming count.
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

    /// <summary>
    /// One booking at a time, and never a booking for somebody already in the building.
    /// </summary>
    /// <remarks>
    /// **A read, with no index behind it** — unlike the open-admission rule, which is
    /// guaranteed by <c>ux_admissions_open_patient</c> and only explained by the read that
    /// precedes it. Two desks booking the same patient in the same instant both pass this and
    /// both rows are written.
    ///
    /// Left as a read on purpose: the fix is a partial unique index and therefore a migration,
    /// and the damage is two rows on a worklist rather than anything clinical — the second
    /// check-in is still refused, by the admission index that does exist. Written down in
    /// RESUME.md rather than left to be discovered.
    /// </remarks>
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
            // Booking a visit for somebody who is already in a bed is not a visit. Their open
            // admission is the record of them being here.
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
