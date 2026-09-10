using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using AdmissionResponse = CareLanka.Api.DTOs.Patient.Admission;
using AppointmentEntity = CareLanka.Api.Data.Entities.Patient.Appointment;
using AppointmentResponse = CareLanka.Api.DTOs.Patient.Appointment;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// Channeling — the third arrival path, where the patient booked beforehand. An appointment
/// is not an admission: it is an intention to come in, and it becomes an admission at the desk.
/// </summary>
public interface IAppointmentService
{
    /// <summary>Who is expected in, so the desk knows before they walk up.</summary>
    Task<PagedResult<AppointmentResponse>> ListAsync(
        DateOnly? date,
        AppointmentStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Book a visit on a patient's behalf, recording which staff member took the booking.</summary>
    Task<AppointmentResponse> CreateAsync(
        CreateAppointmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turn a booked visit into an admission with <c>source = pre_registered</c>, in one
    /// transaction: the appointment moves to <c>checked_in</c> and the admission is created.
    /// </summary>
    /// <remarks>
    /// From here the admission behaves like any other — it needs a bed, and the bed agent runs
    /// on it exactly as it would for a walk-in.
    /// </remarks>
    Task<AdmissionResponse> CheckInAsync(
        Guid id, CheckInRequest request, CancellationToken cancellationToken = default);

    /// <summary>Null when there is no such appointment. For internal lookups — use GetByIdAsync to answer a request.</summary>
    Task<AppointmentEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Throws NotFoundException when there is no such appointment.</summary>
    Task<AppointmentEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
