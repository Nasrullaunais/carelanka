using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.Services.Equipment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Equipment;

/// <summary>The bed register. Equipment Management owns the frame; Patient Management owns whoever is in it.</summary>
[ApiController]
[Route("api/beds")]
[Tags("Beds")]
public class BedsController : ControllerBase
{
    private readonly IBedService _beds;

    public BedsController(IBedService beds) => _beds = beds;

    /// <summary>List beds. Also read by Patient Management, which joins this register with its own BedAssignment rows to build its bed agent's candidate list.</summary>
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

    /// <summary>Create a bed. ward_id references Patient Management's Ward table; we store the reference and never write that table.</summary>
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

        // No Location header: the contract publishes no GET /beds/{id} to point at.
        return Created((string?)null, bed);
    }

    /// <summary>Update a bed's condition or details. Moving to out_of_service is refused with 409 while Patient Management reports the bed occupied or held, checked inside this request every time.</summary>
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

    /// <summary>Retire a bed permanently. Same occupancy check as an update that withdraws it, and there is no un-retire — a dedicated endpoint so the one-way nature is visible in the API surface.</summary>
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
