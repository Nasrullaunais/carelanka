using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.Services.Equipment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Equipment;

[ApiController]
[Route("api/warnings")]
[Tags("Monitoring")]
[Authorize(Policy = Policies.WarningDesk)]
public class WarningsController : ControllerBase
{
    private readonly IWarningService _warnings;

    public WarningsController(IWarningService warnings) => _warnings = warnings;

    [HttpGet(Name = "listWarnings")]
    [ProducesResponseType(typeof(PagedResult<Warning>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<Warning>>> ListWarnings(
        [FromQuery] WarningStatus? status,
        [FromQuery] WarningSeverity? severity,
        [FromQuery] WarningType? type,
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _warnings.ListAsync(new WarningQuery(status, severity, type, page, pageSize), ct));

    [HttpPost("sweep", Name = "runWarningSweep")]
    [ProducesResponseType(typeof(WarningSweepResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<WarningSweepResult>> RunWarningSweep(CancellationToken ct)
        => Ok(await _warnings.SweepAsync(ct));

    [HttpPost("{id:guid}/acknowledge", Name = "acknowledgeWarning")]
    [ProducesResponseType(typeof(Warning), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<Warning>> AcknowledgeWarning(Guid id, CancellationToken ct)
        => Ok(await _warnings.AcknowledgeAsync(id, ct));

    // Taking a resolved warning off the list is the hospital administrator's, with the
    // confirmation code, the same as confirming and retiring equipment.
    [Authorize(Policy = Policies.EquipmentConfirmer)]
    [HttpPost("{id:guid}/clear", Name = "clearWarning")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> ClearWarning(
        Guid id,
        [FromHeader(Name = EquipmentOptions.ConfirmationCodeHeader)] string? confirmationCode,
        CancellationToken ct)
    {
        await _warnings.ClearAsync(id, confirmationCode, ct);

        return NoContent();
    }
}
