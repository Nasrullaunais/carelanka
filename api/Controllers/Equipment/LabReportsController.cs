using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.Services.Equipment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Equipment;

/// <summary>
/// Laboratory results, filed against the patient they belong to. The point is that a ward reads
/// a result the moment the lab issues it, instead of waiting for paper to arrive from the
/// basement.
/// </summary>
[ApiController]
[Route("api/lab-reports")]
[Tags("Laboratory")]
public class LabReportsController : ControllerBase
{
    private readonly ILabReportService _reports;

    public LabReportsController(ILabReportService reports) => _reports = reports;

    /// <summary>
    /// One patient's results, newest first. Metadata only: each row says what the file is and
    /// how big it is, and the file itself is a second request.
    /// </summary>
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

    /// <summary>
    /// Who the laboratory can file against right now, ward by ward. Omit `wardName` for every
    /// current visit, including outpatients holding no bed.
    /// </summary>
    /// <remarks>
    /// This is how a lab actually works: a rack of specimens from one ward, worked down in order.
    /// Everything published here is already visible to these roles through
    /// `GET /patients/{id}`, which carries a patient's admissions and the ward on them - this
    /// shape just saves opening one patient at a time.
    /// </remarks>
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

    /// <summary>
    /// Files a finished report. A multipart form, because it carries the scan itself: a PDF or
    /// a photograph of one, up to 10 MB.
    /// </summary>
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

    /// <summary>
    /// The report itself, served as the document it is so a browser opens a PDF rather than
    /// downloading unnamed bytes.
    /// </summary>
    [Authorize(Policy = Policies.LabReportReader)]
    [HttpGet("{id:guid}/file", Name = "downloadLabReport")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> DownloadLabReport(Guid id, CancellationToken ct = default)
    {
        var file = await _reports.GetFileAsync(id, ct);

        // Inline, and set by hand because the File overload that takes a filename sends
        // `attachment` instead. A ward wants to read the result on screen, and a forced download
        // onto a shared nursing-station machine is how results end up in somebody's Downloads
        // folder. The filename still rides along, so saving it deliberately keeps a real name.
        Response.Headers.ContentDisposition = new ContentDisposition
        {
            Inline = true,
            FileName = file.FileName
        }.ToString();

        return File(file.Content, file.ContentType);
    }
}
