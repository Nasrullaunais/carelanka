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

    /// <summary>
    /// Book a visit the patient made themselves in the app. Same rules as the desk booking —
    /// future date, one open booking at a time, never for somebody already admitted.
    /// </summary>
    /// <remarks>
    /// Records nobody as having taken the booking, which is what tells a self-booking apart
    /// from a desk booking afterwards. Returns the entity rather than a DTO because the caller
    /// is <c>MeService</c>, which publishes the patient's own narrow shape and not the staff one.
    /// </remarks>
    Task<AppointmentEntity> BookForPatientAsync(
        Guid patientId, BookAppointmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancel a booking the patient made. Only a <c>scheduled</c> one can be cancelled, and only
    /// their own — somebody else's reads as not found.
    /// </summary>
    Task<AppointmentEntity> CancelForPatientAsync(
        Guid appointmentId, Guid patientId, CancellationToken cancellationToken = default);

    /// <summary>Null when there is no such appointment. For internal lookups — use GetByIdAsync to answer a request.</summary>
    Task<AppointmentEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Throws NotFoundException when there is no such appointment.</summary>
    Task<AppointmentEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
