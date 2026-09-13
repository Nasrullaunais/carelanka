using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Patient;

[ApiController]
[Route("api/capacity")]
[Tags("Integration")]
public class CapacityController : ControllerBase
{
    private readonly ICapacityService _capacity;

    public CapacityController(ICapacityService capacity) => _capacity = capacity;

    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet("wards", Name = "getWardCapacity")]
    [ProducesResponseType(typeof(WardCapacitySummary), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<WardCapacitySummary>> GetWardCapacity(CancellationToken ct)
        => Ok(await _capacity.GetWardCapacityAsync(ct));
}
