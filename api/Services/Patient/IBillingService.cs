using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using BillResponse = CareLanka.Api.DTOs.Patient.Bill;

namespace CareLanka.Api.Services.Patient;

public interface IBillingService
{
    Task<BillResponse> GetAsync(Guid admissionId, CancellationToken cancellationToken = default);

    Task<BillResponse?> FindForAdmissionAsync(
        Guid admissionId, CancellationToken cancellationToken = default);

    Task<BillResponse> PrepareAsync(Guid admissionId, CancellationToken cancellationToken = default);

    Task<BillResponse> AddChargeAsync(
        Guid admissionId, AddBillChargeRequest request, CancellationToken cancellationToken = default);

    Task<BillResponse> RemoveChargeAsync(
        Guid admissionId, Guid lineId, CancellationToken cancellationToken = default);

    Task<BillResponse> SettleAsync(
        Guid admissionId, SettleBillRequest request, CancellationToken cancellationToken = default);

    Task<BillResponse> GetForAppointmentAsync(
        Guid appointmentId, CancellationToken cancellationToken = default);

    Task<BillResponse> PrepareForAppointmentAsync(
        Guid appointmentId, CancellationToken cancellationToken = default);

    Task<BillResponse> AddAppointmentChargeAsync(
        Guid appointmentId,
        AddBillChargeRequest request,
        CancellationToken cancellationToken = default);

    Task<BillResponse> RemoveAppointmentChargeAsync(
        Guid appointmentId, Guid lineId, CancellationToken cancellationToken = default);

    Task<BillResponse> SettleAppointmentAsync(
        Guid appointmentId,
        SettleBillRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<OutstandingBill>> ListOutstandingAsync(
        string? search,
        bool includeSettled,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
