using System.Net.Mime;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.Services.Equipment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Equipment;

// The pharmacy stands on equipment_manager, for the reason Policies gives the laboratory: StaffRole
// has no pharmacist value.
[ApiController]
[Route("api/prescriptions")]
[Tags("Pharmacy")]
[Authorize(Policy = Policies.EquipmentManager)]
public class PrescriptionsController : ControllerBase
{
    private readonly IPrescriptionService _prescriptions;

    public PrescriptionsController(IPrescriptionService prescriptions) => _prescriptions = prescriptions;

    [HttpGet(Name = "listPrescriptions")]
    [ProducesResponseType(typeof(List<Prescription>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<Prescription>>> ListPrescriptions(
        [FromQuery] PrescriptionStatus status, CancellationToken ct)
        => Ok(await _prescriptions.ListAsync(status, ct));

    [HttpGet("{id:guid}/file", Name = "downloadPrescription")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> DownloadPrescription(Guid id, CancellationToken ct)
    {
        var file = await _prescriptions.GetFileAsync(id, ct);

        Response.Headers.ContentDisposition = new ContentDisposition
        {
            Inline = true,
            FileName = file.FileName
        }.ToString();

        return File(file.Content, file.ContentType);
    }

    [HttpPost("{id:guid}/ready", Name = "markPrescriptionReady")]
    [ProducesResponseType(typeof(Prescription), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<Prescription>> MarkPrescriptionReady(Guid id, CancellationToken ct)
        => Ok(await _prescriptions.MarkReadyAsync(id, ct));

    [HttpPost("{id:guid}/deliver", Name = "markPrescriptionDelivered")]
    [ProducesResponseType(typeof(Prescription), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<Prescription>> MarkPrescriptionDelivered(Guid id, CancellationToken ct)
        => Ok(await _prescriptions.MarkDeliveredAsync(id, ct));

    [HttpPost("{id:guid}/reject", Name = "rejectPrescription")]
    [ProducesResponseType(typeof(Prescription), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<Prescription>> RejectPrescription(
        Guid id, [FromBody] RejectPrescriptionRequest request, CancellationToken ct)
        => Ok(await _prescriptions.RejectAsync(id, request.Reason, ct));
}
