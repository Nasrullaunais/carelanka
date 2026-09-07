using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.Services.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CareLanka.Api.Controllers.Common;

/// <summary>
/// Token issuing for both identities — <c>specs/common-spec.yaml</c>, tag <c>Auth</c>.
/// <para>
/// Thin on purpose: bind, delegate, return. There is no business logic and no
/// <c>try/catch</c> here — the central exception handler turns a thrown
/// <c>ApiException</c> into <c>application/problem+json</c>, and a controller that
/// reshaped an error would produce a response the generated clients cannot classify.
/// </para>
/// </summary>
[ApiController]
[Route("api/auth")]
[Tags("Auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Staff login.</summary>
    /// <remarks>
    /// Unauthenticated. Email and password for a <c>StaffMember</c>.
    ///
    /// Returns 401 for a wrong password, an unknown email **and** a deactivated account —
    /// the same response for all three, so the endpoint cannot be used to discover which
    /// emails exist at the hospital.
    /// </remarks>
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

    /// <summary>Patient self-registration.</summary>
    /// <remarks>
    /// Unauthenticated. Creates a <c>PatientAccount</c> — a **login**, not a medical
    /// record. No <c>Patient</c> row is created and none is linked; staff link the two
    /// later through <c>POST /patients/{id}/link-account</c> after checking identity.
    ///
    /// 409 when the phone number already has an active account.
    /// </remarks>
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

        // 201 with no Location: the thing created is a session, and there is no endpoint
        // that reads an account by id. GET /auth/me is how a client reads it back.
        return Created((string?)null, tokens);
    }

    /// <summary>Patient login.</summary>
    /// <remarks>
    /// Unauthenticated. Phone number and password for a <c>PatientAccount</c>. Same
    /// indistinguishable 401 as staff login, for the same reason.
    /// </remarks>
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

    /// <summary>Exchange a refresh token for a new access token.</summary>
    /// <remarks>
    /// Unauthenticated — the refresh token is the credential, so an expired access token
    /// must not block this call.
    ///
    /// **Rotating.** The presented token is revoked and a new one issued, so a refresh
    /// token is single-use. Presenting an already-revoked token revokes the whole chain for
    /// that principal and returns 401.
    /// </remarks>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost("refresh", Name = "refreshToken")]
    [ProducesResponseType(typeof(AuthTokens), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<AuthTokens>> RefreshToken(
        [FromBody] RefreshTokenRequest request, CancellationToken ct)
        => Ok(await _auth.RefreshAsync(request.RefreshToken, ct));

    /// <summary>End the session.</summary>
    /// <remarks>
    /// Revokes the presented refresh token server-side, so a stolen token is dead even
    /// though the access token is stateless and still inside its lifetime.
    ///
    /// Idempotent — logging out twice is a 204, not an error.
    /// </remarks>
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

    /// <summary>Who is this token.</summary>
    /// <remarks>
    /// Any authenticated principal, staff or patient.
    ///
    /// Resolved entirely from the <c>sub</c> claim — there is no id parameter, so no
    /// principal can read another's identity by changing one. Both clients call this on
    /// startup to decide which navigation to render.
    /// </remarks>
    [Authorize]
    [HttpGet("me", Name = "getCurrentUser")]
    [ProducesResponseType(typeof(CurrentPrincipal), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<CurrentPrincipal>> GetCurrentUser(CancellationToken ct)
        => Ok(await _auth.GetCurrentPrincipalAsync(ct));
}
