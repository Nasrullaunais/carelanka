using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.Services.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Common;

[ApiController]
[Route("api/device-tokens")]
[Tags("Device tokens")]
[Authorize(Policy = Policies.AnyStaff)]
public sealed class DeviceTokensController(IDeviceTokenService devices) : ControllerBase
{
    [HttpPut(Name = "registerDevice")]
    [ProducesResponseType(typeof(DeviceRegistration), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<DeviceRegistration>> Register(RegisterDeviceRequest request, CancellationToken ct)
        => Ok(await devices.RegisterAsync(request, ct));

    [HttpDelete("{id:guid}", Name = "unregisterDevice")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Unregister(Guid id, CancellationToken ct)
    {
        await devices.UnregisterAsync(id, ct);
        return NoContent();
    }
}
