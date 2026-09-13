using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.Services.Patient;

public interface IMeService
{
    Task<MyProfile> PreRegisterAsync(
        PreRegisterRequest request, CancellationToken cancellationToken = default);

    Task<MyProfile> GetProfileAsync(CancellationToken cancellationToken = default);

    Task<MyAdmission> GetCurrentAdmissionAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<MyAdmission>> GetHistoryAsync(
        int page, int pageSize, CancellationToken cancellationToken = default);

    Task<MyAppointment> BookAppointmentAsync(
        BookAppointmentRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<MyAppointment>> ListAppointmentsAsync(
        int page, int pageSize, CancellationToken cancellationToken = default);

    Task<MyAppointment> CancelAppointmentAsync(
        Guid appointmentId, CancellationToken cancellationToken = default);
}
