using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Patient;

/// <summary>
/// The one thing Patient Management publishes about a single bed: whether anyone is in it.
/// </summary>
/// <remarks>
/// Equipment Management owns beds and has its own <c>/api/beds</c> controller for creating,
/// updating and retiring them. This route hangs off theirs because occupancy is not on their
/// row - it is the presence or absence of one of our assignments, so only we can answer it.
///
/// Two controllers under one route prefix is fine; two actions at the same address is not, and
/// no operation here collides with one of theirs. The class is <c>BedOccupancyController</c>
/// and not <c>BedsController</c> for the same reason: theirs already has that name, and one
/// name for two classes is a thing to trip over later for no gain.
/// </remarks>
[ApiController]
[Route("api/beds")]
[Tags("Wards and Beds")]
public class BedOccupancyController : ControllerBase
{
    private readonly IBedAssignmentService _assignments;

    public BedOccupancyController(IBedAssignmentService assignments) => _assignments = assignments;

    /// <summary>Is anyone in this bed?</summary>
    /// <remarks>
    /// Equipment Management calls this **before** taking a bed out of service for repair or
    /// maintenance. Servicing a bed is their operation on their own table, but a bed with a
    /// patient in it must not be withdrawn, and only we know whether it is occupied.
    ///
    /// `occupied` is true when a live assignment exists: status `occupied`, or `reserved` with
    /// a `reserved_until` still in the future. A lapsed hold does not block servicing.
    ///
    /// The hard rule this exists to enforce: **maintenance never evicts a patient.** If the
    /// answer is occupied, Equipment waits for discharge.
    /// </remarks>
    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet("{id:guid}/occupancy", Name = "getBedOccupancy")]
    [ProducesResponseType(typeof(BedOccupancyStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<BedOccupancyStatus>> GetBedOccupancy(Guid id, CancellationToken ct)
        => Ok(await _assignments.GetBedOccupancyAsync(id, ct));
}
