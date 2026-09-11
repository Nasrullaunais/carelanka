using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Patient;

/// <summary>How full the hospital is. Owned by Patient Management, read across the group.</summary>
[ApiController]
[Route("api/capacity")]
[Tags("Integration")]
public class CapacityController : ControllerBase
{
    private readonly ICapacityService _capacity;

    public CapacityController(ICapacityService capacity) => _capacity = capacity;

    /// <summary>
    /// Free bed counts across all wards. Consumed by Emergency Service's dispatch and routing
    /// agent to choose where to send an ambulance.
    /// </summary>
    /// <remarks>
    /// `free_beds` counts beds that are usable, unoccupied, and not under a live hold. A hold
    /// past its `reserved_until` counts as free, and that expiry rule lives in this service so
    /// no other component re-implements it differently.
    ///
    /// Counts only. No patient data crosses this boundary.
    /// </remarks>
    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet("wards", Name = "getWardCapacity")]
    [ProducesResponseType(typeof(WardCapacitySummary), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<WardCapacitySummary>> GetWardCapacity(CancellationToken ct)
        => Ok(await _capacity.GetWardCapacityAsync(ct));
}
