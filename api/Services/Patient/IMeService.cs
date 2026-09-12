using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// Everything a patient may do about themselves from the mobile app. The whole surface behind
/// <c>/api/me</c>.
/// </summary>
/// <remarks>
/// <b>Nothing on here takes a patient id, and that is the point.</b> Every method resolves the
/// medical record from the <c>sub</c> claim through <see cref="Common.ICurrentUser"/>. A method
/// that cannot be passed an id cannot be passed somebody else's, so the worst bug in this
/// component — one patient reading another patient's record — is impossible by construction
/// rather than by a check somebody has to remember to write.
///
/// The shapes it returns are <see cref="MyProfile"/>, <see cref="MyAdmission"/> and
/// <see cref="MyAppointment"/>, none of which is a staff object with a filter over it.
/// </remarks>
public interface IMeService
{
    /// <summary>
    /// Save your own details, and join this login to a hospital record — matching on NIC so a
    /// returning patient stays one person rather than becoming two.
    /// </summary>
    /// <remarks>
    /// Creates no admission. Being admitted is a staff action at the desk, because an admission
    /// records which clinician chose the care level.
    /// </remarks>
    Task<MyProfile> PreRegisterAsync(
        PreRegisterRequest request, CancellationToken cancellationToken = default);

    /// <summary>Your details as they stand. Throws when this login has no record linked yet.</summary>
    Task<MyProfile> GetProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>Your current stay. Throws when you have none, which is the ordinary state.</summary>
    Task<MyAdmission> GetCurrentAdmissionAsync(CancellationToken cancellationToken = default);

    /// <summary>Your past visits, most recent first. An empty page when you have never been treated here.</summary>
    Task<PagedResult<MyAdmission>> GetHistoryAsync(
        int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Book a visit for yourself.</summary>
    Task<MyAppointment> BookAppointmentAsync(
        BookAppointmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Your bookings, upcoming and past, soonest-first among the upcoming ones.</summary>
    Task<PagedResult<MyAppointment>> ListAppointmentsAsync(
        int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Cancel a booking of your own. Somebody else's reads as not found.</summary>
    Task<MyAppointment> CancelAppointmentAsync(
        Guid appointmentId, CancellationToken cancellationToken = default);
}
