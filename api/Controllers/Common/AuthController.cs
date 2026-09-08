using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.Services.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CareLanka.Api.Controllers.Common;

/// <summary>Token issuing for both identities, staff and patient.</summary>
[ApiController]
[Route("api/auth")]
[Tags("Auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Staff login. The same 401 for a wrong password, an unknown email and a deactivated account, so the endpoint cannot be used to discover which emails exist.</summary>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost("login", Name = "login")]
    [ProducesResponseType(typeof(AuthTokens), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<ActionResult<AuthTokens>> Login(
        [FromBody] StaffLoginRequest request, CancellationToken ct)
        => Ok(await _auth.LoginStaffAsync(request, ct));

    /// <summary>Patient self-registration. Creates a login, not a medical record: no Patient row is created and none is linked. 409 when the phone number already has an active account.</summary>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost("patient/register", Name = "registerPatientAccount")]
    [ProducesResponseType(typeof(AuthTokens), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<ActionResult<AuthTokens>> RegisterPatientAccount(
        [FromBody] PatientRegisterRequest request, CancellationToken ct)
    {
        var tokens = await _auth.RegisterPatientAsync(request, ct);

        // No Location: what was created is a session, and no endpoint reads an account by id.
        return Created((string?)null, tokens);
    }

    /// <summary>Patient login, by phone number. Same indistinguishable 401 as staff login.</summary>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost("patient/login", Name = "loginPatient")]
    [ProducesResponseType(typeof(AuthTokens), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<ActionResult<AuthTokens>> LoginPatient(
        [FromBody] PatientLoginRequest request, CancellationToken ct)
        => Ok(await _auth.LoginPatientAsync(request, ct));

    /// <summary>Exchange a refresh token for a new access token. Rotating, so the presented token is single-use; presenting an already-revoked one revokes the whole chain and returns 401.</summary>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost("refresh", Name = "refreshToken")]
    [ProducesResponseType(typeof(AuthTokens), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<ActionResult<AuthTokens>> RefreshToken(
        [FromBody] RefreshTokenRequest request, CancellationToken ct)
        => Ok(await _auth.RefreshAsync(request.RefreshToken, ct));

    /// <summary>End the session by revoking the presented refresh token. Idempotent — logging out twice is a 204.</summary>
    [Authorize]
    [HttpPost("logout", Name = "logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        await _auth.LogoutAsync(request.RefreshToken, ct);

        return NoContent();
    }

    /// <summary>Who this token belongs to. Resolved from the sub claim, so there is no id parameter to change.</summary>
    [Authorize]
    [HttpGet("me", Name = "getCurrentUser")]
    [ProducesResponseType(typeof(CurrentPrincipal), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<CurrentPrincipal>> GetCurrentUser(CancellationToken ct)
        => Ok(await _auth.GetCurrentPrincipalAsync(ct));
}
