using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Patient;

[ApiController]
[Route("api/beds")]
[Tags("Wards and Beds")]
public class BedOccupancyController : ControllerBase
{
    private readonly IBedAssignmentService _assignments;

    public BedOccupancyController(IBedAssignmentService assignments) => _assignments = assignments;

    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet("{id:guid}/occupancy", Name = "getBedOccupancy")]
    [ProducesResponseType(typeof(BedOccupancyStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<BedOccupancyStatus>> GetBedOccupancy(Guid id, CancellationToken ct)
        => Ok(await _assignments.GetBedOccupancyAsync(id, ct));
}
