using CareLanka.Api.Common.Auth;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Patient;

[ApiController]
[Route("api/wards")]
[Tags("Wards and Beds")]
public class WardsController : ControllerBase
{
    private readonly IWardService _wards;
    private readonly ICapacityService _capacity;

    public WardsController(IWardService wards, ICapacityService capacity)
    {
        _wards = wards;
        _capacity = capacity;
    }

    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet(Name = "listWards")]
    [ProducesResponseType(typeof(IReadOnlyList<Ward>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<Ward>>> ListWards(
        [FromQuery] WardType? wardType,
        [FromQuery] bool isActive = true,
        CancellationToken ct = default)
        => Ok(await _wards.ListAsync(wardType, isActive, ct));

    [Authorize(Policy = Policies.HospitalAdministrator)]
    [HttpPost(Name = "createWard")]
    [ProducesResponseType(typeof(Ward), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<Ward>> CreateWard(
        [FromBody] CreateWardRequest request, CancellationToken ct)
    {
        var ward = await _wards.CreateAsync(request, ct);

        return Created((string?)null, ward);
    }

    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet("{id:guid}/occupancy", Name = "getWardOccupancy")]
    [ProducesResponseType(typeof(WardOccupancy), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<WardOccupancy>> GetWardOccupancy(Guid id, CancellationToken ct)
        => Ok(await _capacity.GetWardOccupancyAsync(id, ct));
}
