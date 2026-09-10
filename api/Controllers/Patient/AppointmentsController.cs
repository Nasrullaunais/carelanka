using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AdmissionResponse = CareLanka.Api.DTOs.Patient.Admission;
using AppointmentResponse = CareLanka.Api.DTOs.Patient.Appointment;

namespace CareLanka.Api.Controllers.Patient;

/// <summary>Booked visits — channeling. The third arrival path, where the patient booked beforehand.</summary>
[ApiController]
[Route("api/appointments")]
[Tags("Admissions")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointments;

    public AppointmentsController(IAppointmentService appointments)
        => _appointments = appointments;

    /// <summary>
    /// The expected-visits worklist: who is coming in, so the desk knows before they walk up.
    /// </summary>
    /// <remarks>
    /// The patient's own view of the same booking is `GET /me/appointments`, and the two are
    /// deliberately different shapes — this one carries who booked it and which admission it
    /// became, neither of which is the patient's business.
    ///
    /// `date` filters on whole UTC days, which is what the column stores.
    /// </remarks>
    [Authorize(Policy = Policies.AppointmentDesk)]
    [HttpGet(Name = "listAppointments")]
    [ProducesResponseType(typeof(PagedResult<AppointmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<AppointmentResponse>>> ListAppointments(
        [FromQuery] DateOnly? date,
        [FromQuery] AppointmentStatus? status,
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _appointments.ListAsync(date, status, page, pageSize, ct));

    /// <summary>
    /// Book a visit on a patient's behalf — the desk equivalent of the patient booking it in
    /// the app themselves.
    /// </summary>
    /// <remarks>
    /// Records who took the booking, off the token. That field is null for a self-booking,
    /// which is how the two paths stay tellable apart afterwards.
    ///
    /// One open booking at a time, and never a booking for somebody already admitted.
    /// </remarks>
    [Authorize(Policy = Policies.AppointmentDesk)]
    [HttpPost(Name = "createAppointment")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AppointmentResponse>> CreateAppointment(
        [FromBody] CreateAppointmentRequest request, CancellationToken ct)
    {
        var appointment = await _appointments.CreateAsync(request, ct);

        // No Location: there is no GET /appointments/{id}. A booking is read through the
        // worklist, and inventing a route here would put one in the spec.
        return Created((string?)null, appointment);
    }

    /// <summary>
    /// Check an expected patient in. Turns the booking into an admission with
    /// `source = pre_registered`, in one transaction.
    /// </summary>
    /// <remarks>
    /// The care level is chosen here, by the staff member at the desk — not by the patient
    /// when they booked, and not by an agent. `category_set_by_staff_id` records who chose it.
    ///
    /// A ward nurse may check somebody in as outpatient, day_case or inpatient. `icu` and
    /// `hdu` are the duty manager's, so a nurse asking for either is a 403 — the rule depends
    /// on the body rather than the route, which is why it is not a policy.
    ///
    /// From here the admission behaves like any other: it needs a bed, so the bed agent runs
    /// on it exactly as it would for a walk-in. Returns the admission, not the appointment,
    /// because the admission is what the desk works from next.
    /// </remarks>
    [Authorize(Policy = Policies.AppointmentDesk)]
    [HttpPost("{id:guid}/check-in", Name = "checkInAppointment")]
    [ProducesResponseType(typeof(AdmissionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AdmissionResponse>> CheckInAppointment(
        Guid id, [FromBody] CheckInRequest request, CancellationToken ct)
    {
        var admission = await _appointments.CheckInAsync(id, request, ct);

        return CreatedAtRoute("getAdmission", new { id = admission.Id }, admission);
    }
}
