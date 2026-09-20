using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Patient;

[ApiController]
[Route("api/me")]
[Tags("Patient Self-Service")]
[Authorize(Policy = Policies.PatientOnly)]
public class MeController : ControllerBase
{
    private readonly IMeService _me;

    public MeController(IMeService me) => _me = me;

    [HttpPost("pre-register", Name = "preRegisterSelf")]
    [ProducesResponseType(typeof(MyProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<MyProfile>> PreRegisterSelf(
        [FromBody] PreRegisterRequest request, CancellationToken ct)
        => Ok(await _me.PreRegisterAsync(request, ct));

    [HttpPost("claim/preview", Name = "previewMyClaim")]
    [ProducesResponseType(typeof(PatientClaimPreview), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<PatientClaimPreview>> PreviewMyClaim(
        [FromBody] ClaimByPatientCodeRequest request, CancellationToken ct)
        => Ok(await _me.PreviewClaimAsync(request, ct));

    [HttpPost("claim", Name = "claimMyRecord")]
    [ProducesResponseType(typeof(MyProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<MyProfile>> ClaimMyRecord(
        [FromBody] ClaimByPatientCodeRequest request, CancellationToken ct)
        => Ok(await _me.ClaimAsync(request, ct));

    [HttpGet("profile", Name = "getMyProfile")]
    [ProducesResponseType(typeof(MyProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<MyProfile>> GetMyProfile(CancellationToken ct)
        => Ok(await _me.GetProfileAsync(ct));

    [HttpGet("admission", Name = "getMyAdmission")]
    [ProducesResponseType(typeof(MyAdmission), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<MyAdmission>> GetMyAdmission(CancellationToken ct)
        => Ok(await _me.GetCurrentAdmissionAsync(ct));

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

    [HttpGet("admissions/{admissionId:guid}/bill", Name = "getMyBill")]
    [ProducesResponseType(typeof(MyBill), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<MyBill>> GetMyBill(Guid admissionId, CancellationToken ct)
        => Ok(await _me.GetBillAsync(admissionId, ct));

    [HttpGet("appointments/{appointmentId:guid}/bill", Name = "getMyAppointmentBill")]
    [ProducesResponseType(typeof(MyBill), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<MyBill>> GetMyAppointmentBill(Guid appointmentId, CancellationToken ct)
        => Ok(await _me.GetAppointmentBillAsync(appointmentId, ct));

    [HttpGet("lab-reports", Name = "getMyLabReports")]
    [ProducesResponseType(typeof(PagedResult<MyLabReport>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<MyLabReport>>> GetMyLabReports(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _me.GetLabReportsAsync(page, pageSize, ct));

    [HttpGet("lab-reports/{reportId:guid}/file", Name = "downloadMyLabReport")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> DownloadMyLabReport(Guid reportId, CancellationToken ct)
    {
        var file = await _me.GetLabReportFileAsync(reportId, ct);

        Response.Headers.ContentDisposition = new ContentDisposition
        {
            Inline = true,
            FileName = file.FileName
        }.ToString();

        return File(file.Content, file.ContentType);
    }

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

        return Created((string?)null, appointment);
    }

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
