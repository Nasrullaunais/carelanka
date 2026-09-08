using CareLanka.Api.DTOs.Common;

namespace CareLanka.Api.Services.Common;

public interface ITokenService
{
    (string AccessToken, int ExpiresInSeconds) CreateAccessToken(CurrentPrincipal principal);

    (string Token, string TokenHash) CreateRefreshToken();

    string HashRefreshToken(string token);
}
