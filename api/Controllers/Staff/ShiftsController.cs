using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Staff;

[ApiController]
[Route("api/shifts")]
[Tags("Shifts")]
public sealed class ShiftsController : ControllerBase
{
    private readonly IShiftService _shiftService;

    public ShiftsController(IShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    [Authorize(Roles = "hospital_administrator,duty_manager")]
    [HttpGet(Name = "listShifts")]
    [ProducesResponseType(typeof(PagedResult<ShiftSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<ShiftSummaryDto>>> ListShifts(
        [FromQuery] ListShiftsQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var result = await _shiftService.ListShiftsAsync(parameters, cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = Policies.HospitalAdministrator)]
    [HttpPost(Name = "createShift")]
    [ProducesResponseType(typeof(ShiftDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<ShiftDto>> CreateShift(
        [FromBody] CreateShiftRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _shiftService.CreateShiftAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetShift), new { id = result.Id }, result);
    }

    [Authorize(Policy = Policies.HospitalAdministrator)]
    [HttpPost("bulk", Name = "createShiftsBulk")]
    [ProducesResponseType(typeof(BulkShiftResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<BulkShiftResponse>> CreateShiftsBulk(
        [FromBody] BulkShiftRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _shiftService.CreateShiftsBulkAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [Authorize(Roles = "hospital_administrator,duty_manager")]
    [HttpGet("{id:guid}", Name = "getShift")]
    [ProducesResponseType(typeof(ShiftDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ShiftDetailDto>> GetShift(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _shiftService.GetShiftAsync(id, cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = Policies.HospitalAdministrator)]
    [HttpPut("{id:guid}", Name = "updateShift")]
    [ProducesResponseType(typeof(ShiftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ShiftDto>> UpdateShift(
        Guid id,
        [FromBody] CreateShiftRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _shiftService.UpdateShiftAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = Policies.HospitalAdministrator)]
    [HttpDelete("{id:guid}", Name = "cancelShift")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> CancelShift(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await _shiftService.CancelShiftAsync(id, cancellationToken);
        return NoContent();
    }
}
