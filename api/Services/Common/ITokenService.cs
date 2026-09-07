using CareLanka.Api.DTOs.Common;

namespace CareLanka.Api.Services.Common;

/// <summary>Issues the access token and mints refresh tokens.</summary>
public interface ITokenService
{
    /// <summary>Signs a short-lived JWT for this principal.</summary>
    (string AccessToken, int ExpiresInSeconds) CreateAccessToken(CurrentPrincipal principal);

    /// <summary>
    /// A new refresh token. Returns the value the client keeps and the hash the database
    /// keeps — <strong>the raw token is never stored.</strong>
    /// </summary>
    (string Token, string TokenHash) CreateRefreshToken();

    /// <summary>Hashes a token the client presented, so it can be looked up.</summary>
    string HashRefreshToken(string token);
}
