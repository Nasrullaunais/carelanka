using CareLanka.Api.DTOs.Common;
using CareLanka.Api.Services.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Common;

/// <summary>
/// Sign in, renew, sign out. Group-owned; every other controller in the app depends on
/// the token this one issues.
///
/// Note there is no try/catch anywhere in here. Failures are thrown by the service and
/// shaped by GlobalExceptionHandler — a controller that catches an error produces a
/// response the generated clients cannot classify.
/// </summary>
[ApiController]
[Route("auth")]
[Produces("application/json")]
public class AuthController(IAuthService auth) : ControllerBase
{
    /// <summary>Exchange an email and password for an access token and a refresh token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthTokens), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthTokens>> Login(LoginRequest request, CancellationToken ct)
        => Ok(await auth.LoginAsync(request, ct));

    /// <summary>
    /// Trade a refresh token for a new pair. The old refresh token is revoked in the same
    /// call, so each one works exactly once.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthTokens), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthTokens>> Refresh(RefreshRequest request, CancellationToken ct)
        => Ok(await auth.RefreshAsync(request.RefreshToken, ct));

    /// <summary>Revoke a refresh token. Idempotent — logging out twice is not an error.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await auth.LogoutAsync(request.RefreshToken, ct);
        return NoContent();
    }

    /// <summary>Who am I? Reads the JWT, touches no database.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(AuthenticatedStaff), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public ActionResult<AuthenticatedStaff> Me([FromServices] ICurrentUser currentUser)
        => Ok(new AuthenticatedStaff
        {
            Id = currentUser.StaffMemberId!.Value,
            Email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? string.Empty,
            FullName = User.Identity?.Name ?? string.Empty,
            Role = currentUser.Role!.Value,
            Department = null
        });
}
