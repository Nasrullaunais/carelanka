using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Patient;

/// <summary>
/// What a patient may see and do about themselves, from the mobile app. Nothing here is a
/// staff screen and nothing here reads another patient.
/// </summary>
/// <remarks>
/// <b>Not one route on this controller takes a patient id.</b> Every one resolves the medical
/// record from the <c>sub</c> claim. A route with no id in it cannot be given somebody else's,
/// which is the only way to be sure of the worst bug this component could have - one patient
/// reading another patient's medical record.
///
/// <c>PatientOnly</c> throughout, so a staff token is refused here even though staff can see
/// far more through their own endpoints. The two audiences read different shapes through
/// different routes, and mixing them is how a filter gets forgotten.
/// </remarks>
[ApiController]
[Route("api/me")]
[Tags("Patient Self-Service")]
[Authorize(Policy = Policies.PatientOnly)]
public class MeController : ControllerBase
{
    private readonly IMeService _me;

    public MeController(IMeService me) => _me = me;

    /// <summary>Save your own details, and join this login to your hospital record.</summary>
    /// <remarks>
    /// Matches on NIC. A record the hospital already holds is linked to this account rather
    /// than duplicated, which is what stops a returning patient's history splitting in two.
    ///
    /// <b>This does not book a visit and does not admit you.</b> Booking is
    /// `POST /me/appointments`; being admitted happens at the desk, because an admission
    /// records which clinician chose the care level and a patient is not one.
    ///
    /// Safe to call again to correct or complete your details. It is the same record every
    /// time, so this answers 200 rather than 201 - there is no second resource to create.
    /// </remarks>
    [HttpPost("pre-register", Name = "preRegisterSelf")]
    [ProducesResponseType(typeof(MyProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<MyProfile>> PreRegisterSelf(
        [FromBody] PreRegisterRequest request, CancellationToken ct)
        => Ok(await _me.PreRegisterAsync(request, ct));

    /// <summary>Your details as the hospital holds them.</summary>
    /// <remarks>
    /// 404 while this login has no record linked to it yet, which is the ordinary state for an
    /// account that signed up last night. The app reads that as "show the details form", not as
    /// an error - `GET /auth/me` cannot answer it, because it publishes `patient_id` as null for
    /// everybody today.
    /// </remarks>
    [HttpGet("profile", Name = "getMyProfile")]
    [ProducesResponseType(typeof(MyProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<MyProfile>> GetMyProfile(CancellationToken ct)
        => Ok(await _me.GetProfileAsync(ct));

    /// <summary>Your current stay - where you are, and what happens next.</summary>
    /// <remarks>
    /// Returns `MyAdmission`, a separate and deliberately small shape. This is not the staff
    /// admission object with a filter applied: a filtered staff object leaks the first time
    /// somebody adds a field and forgets the filter, and a separate shape cannot leak what it
    /// does not contain. There is no staff note, no agent reasoning, no rejection history and
    /// nothing about any other patient anywhere in it.
    ///
    /// 404 when you are not in hospital, which is the ordinary state.
    /// </remarks>
    [HttpGet("admission", Name = "getMyAdmission")]
    [ProducesResponseType(typeof(MyAdmission), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<MyAdmission>> GetMyAdmission(CancellationToken ct)
        => Ok(await _me.GetCurrentAdmissionAsync(ct));

    /// <summary>Your past visits, most recent first.</summary>
    /// <remarks>
    /// Finished visits only. The one that is still open is `GET /me/admission`, so a patient
    /// currently in a bed does not see that stay listed twice on one screen.
    ///
    /// An empty page, never a 404, for somebody who has never been treated here.
    /// </remarks>
    [HttpGet("history", Name = "getMyHistory")]
    [ProducesResponseType(typeof(PagedResult<MyAdmission>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<MyAdmission>>> GetMyHistory(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _me.GetHistoryAsync(page, pageSize, ct));

    /// <summary>Book a visit for yourself.</summary>
    /// <remarks>
    /// You say when you intend to arrive; the desk sees you on their worklist and checks you in
    /// when you get there. Deliberately narrow: <b>no doctor calendars, no time slots, no
    /// availability search and no rescheduling.</b> Rescheduling is cancel and rebook.
    ///
    /// One open booking at a time, so the app cannot be used to hold several speculative slots,
    /// and no booking at all while you are already admitted - the stay you are on is the record
    /// of you being here.
    ///
    /// 409 when your login has no medical record yet. Fill in your details first.
    /// </remarks>
    [HttpPost("appointments", Name = "bookMyAppointment")]
    [ProducesResponseType(typeof(MyAppointment), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<MyAppointment>> BookMyAppointment(
        [FromBody] BookAppointmentRequest request, CancellationToken ct)
    {
        var appointment = await _me.BookAppointmentAsync(request, ct);

        // No Location: there is no route that reads one booking on its own. A patient reads
        // theirs through GET /me/appointments, and inventing a route here would put one in the
        // contract that nothing needs.
        return Created((string?)null, appointment);
    }

    /// <summary>Your bookings, upcoming and past.</summary>
    /// <remarks>
    /// Returns `MyAppointment`, a separate narrow shape for the same reason `MyAdmission` is
    /// one. It carries neither who took the booking nor which admission it became.
    ///
    /// An empty page, never a 404, for somebody who has never booked anything.
    /// </remarks>
    [HttpGet("appointments", Name = "listMyAppointments")]
    [ProducesResponseType(typeof(PagedResult<MyAppointment>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<MyAppointment>>> ListMyAppointments(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _me.ListAppointmentsAsync(page, pageSize, ct));

    /// <summary>Cancel a visit you booked.</summary>
    /// <remarks>
    /// The id in the path is checked against your own record, never trusted on its own -
    /// somebody else's booking answers 404, because a 403 would confirm the id is real.
    ///
    /// Only a `scheduled` booking can be cancelled. Once the desk has checked you in it is an
    /// admission, and cancelling an admission is a staff action with its own rules.
    /// </remarks>
    [HttpPost("appointments/{id:guid}/cancel", Name = "cancelMyAppointment")]
    [ProducesResponseType(typeof(MyAppointment), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<MyAppointment>> CancelMyAppointment(
        Guid id, CancellationToken ct)
        => Ok(await _me.CancelAppointmentAsync(id, ct));
}
