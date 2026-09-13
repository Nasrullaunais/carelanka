using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.Services.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CareLanka.Api.Controllers.Common;

[ApiController]
[Route("api/auth")]
[Tags("Auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

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

        return Created((string?)null, tokens);
    }

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

    [Authorize]
    [HttpGet("me", Name = "getCurrentUser")]
    [ProducesResponseType(typeof(CurrentPrincipal), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<CurrentPrincipal>> GetCurrentUser(CancellationToken ct)
        => Ok(await _auth.GetCurrentPrincipalAsync(ct));
}
