using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// The ward board: one paged list answering "who are we dealing with, and what is happening
/// with them", across bookings and visits together.
/// </summary>
/// <remarks>
/// Reads only. Every write still goes to the service that owns the row — a booking is checked
/// in through <see cref="IAppointmentService"/>, a visit gets a bed or is completed through
/// <see cref="IAdmissionService"/>. This is a view, not a fifth place a status can change.
/// </remarks>
public interface IWorklistService
{
    /// <summary>
    /// One page of the board, newest first.
    /// </summary>
    /// <param name="search">Matches the patient's name or NIC, case-insensitively. Null matches everyone.</param>
    /// <param name="includeFinished">
    /// False — the default — is who the hospital is dealing with now. True adds visits that are
    /// discharged or cancelled, which is the archive rather than the board.
    /// </param>
    Task<PagedResult<WorklistRow>> ListAsync(
        string? search,
        bool includeFinished,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
