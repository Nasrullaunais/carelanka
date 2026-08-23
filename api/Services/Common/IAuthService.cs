using CareLanka.Api.DTOs.Common;

namespace CareLanka.Api.Services.Common;

public interface IAuthService
{
    Task<AuthTokens> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthTokens> RefreshAsync(string rawRefreshToken, CancellationToken ct = default);
    Task LogoutAsync(string rawRefreshToken, CancellationToken ct = default);
}
