using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Emergency;
using CareLanka.Api.Services.Emergency;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Emergency;

[ApiController]
[Route("api/fleet-map")]
[Tags("Ambulances")]
[Authorize(Policy = Policies.DutyManager)]
public sealed class FleetMapController(IFleetMapService fleetMap) : ControllerBase
{
    [HttpGet(Name = "getFleetMap")]
    [ProducesResponseType(typeof(FleetMap), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<FleetMap>> Get(CancellationToken ct) => Ok(await fleetMap.GetAsync(ct));
}
