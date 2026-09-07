using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.DTOs.Common;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CareLanka.Api.Services.Common;

/// <inheritdoc cref="ITokenService"/>
public sealed class TokenService : ITokenService
{
    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public (string AccessToken, int ExpiresInSeconds) CreateAccessToken(CurrentPrincipal principal)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(CareLankaClaims.Subject, principal.Id.ToString()),
            new(CareLankaClaims.Role, EnumWire.ToWire(principal.Role)),
            new(CareLankaClaims.PrincipalType, EnumWire.ToWire(principal.PrincipalType)),
            new(CareLankaClaims.TokenId, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), _options.AccessTokenMinutes * 60);
    }

    public (string Token, string TokenHash) CreateRefreshToken()
    {
        // 256 bits of randomness. The token is an opaque secret, not a signed structure —
        // there is nothing in it to read, and the database row is what says it is alive.
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        return (token, HashRefreshToken(token));
    }

    public string HashRefreshToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
