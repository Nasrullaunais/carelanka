using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Messages;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.DTOs.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CareLanka.Api.Services.Common;

/// <summary>
/// Group-owned. Every [Authorize] endpoint in all four components depends on this, so
/// treat it like Ward: change it only with the group.
/// </summary>
public class AuthService(
    CareLankaDbContext db,
    ITokenService tokens,
    IOptions<JwtOptions> options,
    TimeProvider clock) : IAuthService
{
    private readonly JwtOptions _jwt = options.Value;

    public async Task<AuthTokens> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var staff = await db.StaffMembers.FirstOrDefaultAsync(s => s.Email == email, ct);

        // Deliberately the same error whether the email is unknown or the password is
        // wrong. Two different messages tell an attacker which emails are real.
        // Verify a dummy hash when the user is missing so both paths take the same time.
        var passwordOk = staff is not null
            ? BCrypt.Net.BCrypt.Verify(request.Password, staff.PasswordHash)
            : BCrypt.Net.BCrypt.Verify(request.Password, DummyHash);

        if (staff is null || !passwordOk)
            throw new BadRequestException(MessageCode.cl_err_100_invalid_credentials);

        return await IssueAsync(staff, ct);
    }

    public async Task<AuthTokens> RefreshAsync(string rawRefreshToken, CancellationToken ct = default)
    {
        var hash = tokens.Hash(rawRefreshToken);
        var now = clock.GetUtcNow();

        var stored = await db.RefreshTokens
            .Include(t => t.StaffMember)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null || !stored.IsUsable(now) || stored.StaffMember is null)
            throw new BadRequestException(MessageCode.cl_err_102_refresh_token_invalid);

        // Rotate: the old token dies the moment it is used, so a stolen one is good for
        // at most a single call.
        stored.RevokedAt = now;

        return await IssueAsync(stored.StaffMember, ct);
    }

    public async Task LogoutAsync(string rawRefreshToken, CancellationToken ct = default)
    {
        var hash = tokens.Hash(rawRefreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        // Logging out twice is not an error.
        if (stored is null || stored.RevokedAt is not null) return;

        stored.RevokedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    private async Task<AuthTokens> IssueAsync(StaffMember staff, CancellationToken ct)
    {
        var (accessToken, expiresIn) = tokens.CreateAccessToken(staff);
        var (raw, hash) = tokens.CreateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            StaffMemberId = staff.Id,
            TokenHash = hash,
            ExpiresAt = clock.GetUtcNow().AddDays(_jwt.RefreshTokenDays)
        });

        await db.SaveChangesAsync(ct);

        return new AuthTokens
        {
            AccessToken = accessToken,
            RefreshToken = raw,
            TokenType = "Bearer",
            ExpiresIn = expiresIn,
            StaffMember = new AuthenticatedStaff
            {
                Id = staff.Id,
                Email = staff.Email,
                FullName = $"{staff.FirstName} {staff.LastName}",
                Role = staff.Role,
                Department = staff.Department
            }
        };
    }

    // A real BCrypt hash of a value nobody will ever submit. Only used to keep the
    // unknown-email path as slow as the wrong-password path.
    private const string DummyHash =
        "$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy";
}
