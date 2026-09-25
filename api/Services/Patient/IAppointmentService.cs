using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using AdmissionResponse = CareLanka.Api.DTOs.Patient.Admission;
using AppointmentEntity = CareLanka.Api.Data.Entities.Patient.Appointment;
using AppointmentResponse = CareLanka.Api.DTOs.Patient.Appointment;

namespace CareLanka.Api.Services.Patient;

public interface IAppointmentService
{
    Task<PagedResult<AppointmentResponse>> ListAsync(
        DateOnly? date,
        AppointmentStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AppointmentResponse> CreateAsync(
        CreateAppointmentRequest request, CancellationToken cancellationToken = default);

    Task<AppointmentResponse> CreateWalkInAsync(
        CreateWalkInAppointmentRequest request, CancellationToken cancellationToken = default);

    Task<AdmissionResponse> CheckInAsync(
        Guid id, CheckInRequest request, CancellationToken cancellationToken = default);

    Task<AppointmentEntity> BookForPatientAsync(
        Guid patientId, BookAppointmentRequest request, CancellationToken cancellationToken = default);

    Task<AppointmentEntity> CancelForPatientAsync(
        Guid appointmentId, Guid patientId, CancellationToken cancellationToken = default);

    Task<AppointmentResponse> CancelAtTheDeskAsync(
        Guid id, CancelAppointmentRequest request, CancellationToken cancellationToken = default);

    Task<AppointmentResponse> ConfirmAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AppointmentResponse> CompleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AppointmentResponse> MarkNoShowAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AppointmentEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AppointmentEntity> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
