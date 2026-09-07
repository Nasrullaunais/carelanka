using CareLanka.Api.DTOs.Common;

namespace CareLanka.Api.Services.Common;

/// <summary>
/// Everything behind <c>/api/auth</c>. The transaction boundary and the throwing both live
/// here — there is no facade layer (ADR 4) and no <c>try/catch</c> in the controller.
/// </summary>
public interface IAuthService
{
    Task<AuthTokens> LoginStaffAsync(StaffLoginRequest request, CancellationToken ct = default);

    Task<AuthTokens> RegisterPatientAsync(PatientRegisterRequest request, CancellationToken ct = default);

    Task<AuthTokens> LoginPatientAsync(PatientLoginRequest request, CancellationToken ct = default);

    Task<AuthTokens> RefreshAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Idempotent. Logging out twice is not an error.</summary>
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Answers <c>GET /auth/me</c> from the token's own claims.</summary>
    Task<CurrentPrincipal> GetCurrentPrincipalAsync(CancellationToken ct = default);
}
