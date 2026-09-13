using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.Services.Equipment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Equipment;

[ApiController]
[Route("api/lab-reports")]
[Tags("Laboratory")]
public class LabReportsController : ControllerBase
{
    private readonly ILabReportService _reports;

    public LabReportsController(ILabReportService reports) => _reports = reports;

    [Authorize(Policy = Policies.LabReportReader)]
    [HttpGet(Name = "listLabReports")]
    [ProducesResponseType(typeof(PagedResult<LabReport>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<LabReport>>> ListLabReports(
        [FromQuery][Required] Guid patientId,
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _reports.ListForPatientAsync(patientId, page, pageSize, ct));

    [Authorize(Policy = Policies.LabReportReader)]
    [HttpGet("patients", Name = "listLabPatients")]
    [ProducesResponseType(typeof(PagedResult<LabPatient>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<LabPatient>>> ListLabPatients(
        [FromQuery] string? wardName,
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _reports.ListPatientsAsync(wardName, page, pageSize, ct));

    [Authorize(Policy = Policies.LabReportAuthor)]
    [HttpPost(Name = "uploadLabReport")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(LabReport), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<LabReport>> UploadLabReport(
        [FromForm] UploadLabReportRequest request, CancellationToken ct = default)
    {
        var report = await _reports.UploadAsync(request, ct);

        return CreatedAtRoute("listLabReports", new { patientId = report.PatientId }, report);
    }

    [Authorize(Policy = Policies.LabReportReader)]
    [HttpGet("{id:guid}/file", Name = "downloadLabReport")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> DownloadLabReport(Guid id, CancellationToken ct = default)
    {
        var file = await _reports.GetFileAsync(id, ct);

        // Set by hand because the File overload that takes a filename sends `attachment`. A ward
        // reads the result on screen; a forced download onto a shared nursing-station machine is
        // how results end up in somebody's Downloads folder.
        Response.Headers.ContentDisposition = new ContentDisposition
        {
            Inline = true,
            FileName = file.FileName
        }.ToString();

        return File(file.Content, file.ContentType);
    }
}
