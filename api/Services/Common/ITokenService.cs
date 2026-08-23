using CareLanka.Api.Data.Entities.Common;

namespace CareLanka.Api.Services.Common;

public interface ITokenService
{
    /// <summary>Signs a short-lived access token carrying the staff id and role claim.</summary>
    (string Token, int ExpiresInSeconds) CreateAccessToken(StaffMember staff);

    /// <summary>Returns the raw refresh token for the client, and its hash for the database.</summary>
    (string Raw, string Hash) CreateRefreshToken();

    string Hash(string rawRefreshToken);
}
