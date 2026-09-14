using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DischargeResponse = CareLanka.Api.DTOs.Patient.Discharge;

namespace CareLanka.Api.Controllers.Patient;

[ApiController]
[Route("api/discharges")]
[Tags("Discharge")]
public class DischargesController : ControllerBase
{
    private readonly IDischargeService _discharges;

    public DischargesController(IDischargeService discharges)
    {
        _discharges = discharges;
    }

    [Authorize(Policy = Policies.DischargeBoard)]
    [HttpGet("candidates", Name = "listDischargeCandidates")]
    [ProducesResponseType(typeof(PagedResult<DischargeCandidate>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<DischargeCandidate>>> ListDischargeCandidates(
        [FromQuery] Guid? wardId,
        [FromQuery] bool includeDischarged = false,
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _discharges.ListCandidatesAsync(wardId, includeDischarged, page, pageSize, ct));

    [Authorize(Policy = Policies.DischargeChecklist)]
    [HttpPatch("{admissionId:guid}/checklist", Name = "updateDischargeChecklist")]
    [ProducesResponseType(typeof(DischargeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DischargeResponse>> UpdateDischargeChecklist(
        Guid admissionId, [FromBody] ChecklistUpdateRequest request, CancellationToken ct)
        => Ok(await _discharges.UpdateChecklistAsync(admissionId, request, ct));

    [Authorize(Policy = Policies.DischargeConfirmer)]
    [HttpPost("{admissionId:guid}/confirm", Name = "confirmDischarge")]
    [ProducesResponseType(typeof(DischargeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DischargeResponse>> ConfirmDischarge(
        Guid admissionId, [FromBody] ConfirmDischargeRequest? request, CancellationToken ct)
        => Ok(await _discharges.ConfirmAsync(admissionId, request ?? new ConfirmDischargeRequest(), ct));
}
