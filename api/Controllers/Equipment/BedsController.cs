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
[Route("api/beds")]
[Tags("Beds")]
public class BedsController : ControllerBase
{
    private readonly IBedService _beds;

    public BedsController(IBedService beds) => _beds = beds;

    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet(Name = "listBeds")]
    [ProducesResponseType(typeof(PagedResult<Bed>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<PagedResult<Bed>>> ListBeds(
        [FromQuery] Guid? wardId,
        [FromQuery] BedCondition? condition,
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _beds.ListAsync(wardId, condition, page, pageSize, ct));

    [Authorize(Policy = Policies.EquipmentManager)]
    [HttpPost(Name = "createBed")]
    [ProducesResponseType(typeof(Bed), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<Bed>> CreateBed(
        [FromBody] CreateBedRequest request, CancellationToken ct)
    {
        var bed = await _beds.CreateAsync(request, ct);

        return Created((string?)null, bed);
    }

    [Authorize(Policy = Policies.EquipmentManager)]
    [HttpPatch("{id:guid}", Name = "updateBed")]
    [ProducesResponseType(typeof(Bed), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<Bed>> UpdateBed(
        Guid id, [FromBody] UpdateBedRequest request, CancellationToken ct)
        => Ok(await _beds.UpdateAsync(id, request, ct));

    [Authorize(Policy = Policies.EquipmentManager)]
    [HttpPost("{id:guid}/retire", Name = "retireBed")]
    [ProducesResponseType(typeof(Bed), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<Bed>> RetireBed(Guid id, CancellationToken ct)
        => Ok(await _beds.RetireAsync(id, ct));
}
