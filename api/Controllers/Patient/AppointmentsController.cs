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

[ApiController]
[Route("api/appointments")]
[Tags("Admissions")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointments;

    public AppointmentsController(IAppointmentService appointments)
        => _appointments = appointments;

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

        return Created((string?)null, appointment);
    }

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
