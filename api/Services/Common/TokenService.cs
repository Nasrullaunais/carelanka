using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CareLanka.Api.Data.Entities.Common;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CareLanka.Api.Services.Common;

public class TokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private readonly JwtOptions _jwt = options.Value;

    public (string Token, int ExpiresInSeconds) CreateAccessToken(StaffMember staff)
    {
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(_jwt.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, staff.Id.ToString()),
            new(ClaimTypes.Email, staff.Email),
            new(ClaimTypes.Name, $"{staff.FirstName} {staff.LastName}"),
            // The role claim is what every [Authorize(Roles = ...)] in all four
            // components reads. Enum name, not the snake_case wire form.
            new(ClaimTypes.Role, staff.Role.ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), _jwt.AccessTokenMinutes * 60);
    }

    public (string Raw, string Hash) CreateRefreshToken()
    {
        // Opaque random string, not a JWT. Nothing is encoded in it; it is only a lookup
        // key, which is what lets us revoke it server-side.
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return (raw, Hash(raw));
    }

    public string Hash(string rawRefreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawRefreshToken)));
}
