using CareLanka.Api.Common.Auth;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Patient;

/// <summary>The ward register. Owned by Patient Management and read by all four components.</summary>
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

    /// <summary>List wards. Also read by Equipment Management for allocation and by Staff Management for staffing demand.</summary>
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

    /// <summary>Create a ward. The gender policy is a property of the ward, not a rule the bed agent bends under pressure.</summary>
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

        // No Location: there is no GET /wards/{id}. A ward is read through the list, or
        // through its occupancy, and inventing a route here would put one in the spec.
        return Created((string?)null, ward);
    }

    /// <summary>
    /// Occupancy and care mix for one ward. Consumed by Staff Management to work out staffing
    /// demand.
    /// </summary>
    /// <remarks>
    /// `patients_by_category` is the useful part: fifteen routine inpatients and two
    /// high-dependency patients need very different staffing, even though both are "seventeen
    /// patients". `incoming_next_2h` is what lets the staff allocation agent staff AHEAD of a
    /// rush instead of reacting to one.
    ///
    /// Counts only. No patient identities cross this boundary.
    /// </remarks>
    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet("{id:guid}/occupancy", Name = "getWardOccupancy")]
    [ProducesResponseType(typeof(WardOccupancy), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<WardOccupancy>> GetWardOccupancy(Guid id, CancellationToken ct)
        => Ok(await _capacity.GetWardOccupancyAsync(id, ct));
}
