using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using BillResponse = CareLanka.Api.DTOs.Patient.Bill;

namespace CareLanka.Api.Services.Patient;

/// <summary>What a visit costs, and taking the money for it.</summary>
public interface IBillingService
{
    /// <summary>
    /// The bill for this visit. Throws <c>NotFoundException</c> when nobody has prepared one
    /// yet, which is the ordinary state of a visit still in progress.
    /// </summary>
    Task<BillResponse> GetAsync(Guid admissionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The bill for this visit, or null when there is none. <c>Find</c>, not <c>Get</c>: on a
    /// list a missing bill is a fact to show, not a failure.
    /// </summary>
    Task<BillResponse?> FindForAdmissionAsync(
        Guid admissionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Work the bill out from the stay: an admission fee and a line per bed the patient has
    /// actually been in. Every generated line is replaced; typed lines are left alone.
    /// </summary>
    Task<BillResponse> PrepareAsync(Guid admissionId, CancellationToken cancellationToken = default);

    /// <summary>A charge reception types in, because no table in this component records one.</summary>
    Task<BillResponse> AddChargeAsync(
        Guid admissionId, AddBillChargeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Take a typed line off again. A generated line is refused.</summary>
    Task<BillResponse> RemoveChargeAsync(
        Guid admissionId, Guid lineId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The money is in. Freezes the bill and ticks <c>billing_settled</c> on the discharge
    /// checklist, in one transaction - that tick has no other way of being written.
    /// </summary>
    Task<BillResponse> SettleAsync(
        Guid admissionId, SettleBillRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reception's worklist: visits whose money has not been taken yet.</summary>
    Task<PagedResult<OutstandingBill>> ListOutstandingAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken = default);
}
