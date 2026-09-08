using CareLanka.Api.Common.Auth;
using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CareLanka.Api.Services.Common;

public sealed class AuthService : IAuthService
{
    // Verified against when the account does not exist, so an unknown email takes the same
    // ~100ms as a known one. Without it the timing alone says which addresses are real.
    private static readonly Lazy<string> DecoyHash =
        new(() => new PasswordService().Hash("not-a-real-password-b2f1c9"));

    private readonly CareLankaDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly ITokenService _tokens;
    private readonly ILoginThrottle _throttle;
    private readonly ICurrentUser _currentUser;
    private readonly JwtOptions _options;

    public AuthService(
        CareLankaDbContext db,
        IPasswordService passwords,
        ITokenService tokens,
        ILoginThrottle throttle,
        ICurrentUser currentUser,
        IOptions<JwtOptions> options)
    {
        _db = db;
        _passwords = passwords;
        _tokens = tokens;
        _throttle = throttle;
        _currentUser = currentUser;
        _options = options.Value;
    }

    public async Task<AuthTokens> LoginStaffAsync(StaffLoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        _throttle.EnsureNotLockedOut(email);

        var staff = await _db.StaffMembers.FirstOrDefaultAsync(s => s.Email == email, ct);

        if (!VerifyOrDecoy(staff?.PasswordHash, request.Password, out var needsRehash))
        {
            _throttle.RecordFailure(email);
            throw new UnauthorizedException();
        }

        if (needsRehash)
        {
            staff!.PasswordHash = _passwords.Hash(request.Password);
        }

        _throttle.RecordSuccess(email);

        return await IssueAsync(ToPrincipal(staff!), staff!.Id, PrincipalType.Staff, ct);
    }

    public async Task<AuthTokens> RegisterPatientAsync(PatientRegisterRequest request, CancellationToken ct = default)
    {
        var phone = request.PhoneNumber.Trim();

        var taken = await _db.PatientAccounts.AnyAsync(p => p.PhoneNumber == phone, ct);

        if (taken)
        {
            throw new ConflictException(MessageCode.PhoneNumberAlreadyRegistered);
        }

        var account = new PatientAccount
        {
            Id = Guid.NewGuid(),
            PhoneNumber = phone,
            PasswordHash = _passwords.Hash(request.Password),
            FullName = request.FullName.Trim(),
            LastLoginAt = DateTimeOffset.UtcNow
        };

        _db.PatientAccounts.Add(account);

        return await IssueAsync(ToPrincipal(account), account.Id, PrincipalType.Patient, ct);
    }

    public async Task<AuthTokens> LoginPatientAsync(PatientLoginRequest request, CancellationToken ct = default)
    {
        var phone = request.PhoneNumber.Trim();

        _throttle.EnsureNotLockedOut(phone);

        var account = await _db.PatientAccounts.FirstOrDefaultAsync(p => p.PhoneNumber == phone, ct);

        if (!VerifyOrDecoy(account?.PasswordHash, request.Password, out var needsRehash))
        {
            _throttle.RecordFailure(phone);
            throw new UnauthorizedException();
        }

        if (needsRehash)
        {
            account!.PasswordHash = _passwords.Hash(request.Password);
        }

        account!.LastLoginAt = DateTimeOffset.UtcNow;
        _throttle.RecordSuccess(phone);

        return await IssueAsync(ToPrincipal(account), account.Id, PrincipalType.Patient, ct);
    }

    public async Task<AuthTokens> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = _tokens.HashRefreshToken(refreshToken);
        var now = DateTimeOffset.UtcNow;

        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash, ct);

        if (stored is null)
        {
            throw new UnauthorizedException(MessageCode.RefreshTokenInvalid);
        }

        // A single-use token presented twice means two clients hold it and one is not the owner,
        // so every live session for this principal dies — not just this one.
        if (stored.RevokedAt is not null)
        {
            await RevokeAllForPrincipalAsync(stored, "reuse_detected", now, ct);
            throw new UnauthorizedException(MessageCode.RefreshTokenInvalid);
        }

        if (stored.ExpiresAt <= now)
        {
            throw new UnauthorizedException(MessageCode.RefreshTokenInvalid);
        }

        // Conditional UPDATE rather than read-then-write, so two refreshes arriving together
        // cannot both win. The loser changes zero rows and is treated as reuse.
        var rotated = await _db.RefreshTokens
            .Where(r => r.Id == stored.Id && r.RevokedAt == null)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(r => r.RevokedAt, now)
                    .SetProperty(r => r.RevokedReason, "rotated"),
                ct);

        if (rotated == 0)
        {
            await RevokeAllForPrincipalAsync(stored, "reuse_detected", now, ct);
            throw new UnauthorizedException(MessageCode.RefreshTokenInvalid);
        }

        // Comes back null if the account was deactivated mid-session: both query filters exclude it.
        var principal = stored.PrincipalType == PrincipalType.Staff
            ? ToPrincipalOrNull(await _db.StaffMembers.FirstOrDefaultAsync(s => s.Id == stored.StaffMemberId, ct))
            : ToPrincipalOrNull(await _db.PatientAccounts.FirstOrDefaultAsync(p => p.Id == stored.PatientAccountId, ct));

        if (principal is null)
        {
            throw new UnauthorizedException(MessageCode.RefreshTokenInvalid);
        }

        return await IssueAsync(principal, principal.Id, stored.PrincipalType, ct);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = _tokens.HashRefreshToken(refreshToken);

        await _db.RefreshTokens
            .Where(r => r.TokenHash == hash && r.RevokedAt == null)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(r => r.RevokedAt, DateTimeOffset.UtcNow)
                    .SetProperty(r => r.RevokedReason, "logout"),
                ct);
    }

    public async Task<CurrentPrincipal> GetCurrentPrincipalAsync(CancellationToken ct = default)
    {
        var id = _currentUser.Id;

        var principal = _currentUser.PrincipalType == PrincipalType.Staff
            ? ToPrincipalOrNull(await _db.StaffMembers.FirstOrDefaultAsync(s => s.Id == id, ct))
            : ToPrincipalOrNull(await _db.PatientAccounts.FirstOrDefaultAsync(p => p.Id == id, ct));

        return principal ?? throw new UnauthorizedException(MessageCode.NotAuthenticated);
    }

    private bool VerifyOrDecoy(string? storedHash, string password, out bool needsRehash)
    {
        var matched = _passwords.Verify(storedHash ?? DecoyHash.Value, password, out needsRehash);

        if (storedHash is null)
        {
            needsRehash = false;
            return false;
        }

        return matched;
    }

    private async Task<AuthTokens> IssueAsync(
        CurrentPrincipal principal, Guid principalId, PrincipalType type, CancellationToken ct)
    {
        var (accessToken, expiresIn) = _tokens.CreateAccessToken(principal);
        var (refreshToken, refreshHash) = _tokens.CreateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            PrincipalType = type,
            StaffMemberId = type == PrincipalType.Staff ? principalId : null,
            PatientAccountId = type == PrincipalType.Patient ? principalId : null,
            TokenHash = refreshHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenDays)
        });

        await _db.SaveChangesAsync(ct);

        return new AuthTokens
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresIn = expiresIn,
            RefreshToken = refreshToken,
            Principal = principal
        };
    }

    private async Task RevokeAllForPrincipalAsync(
        RefreshToken stored, string reason, DateTimeOffset now, CancellationToken ct)
    {
        var staffMemberId = stored.StaffMemberId;
        var patientAccountId = stored.PatientAccountId;

        await _db.RefreshTokens
            .Where(r => r.RevokedAt == null
                        && r.StaffMemberId == staffMemberId
                        && r.PatientAccountId == patientAccountId)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(r => r.RevokedAt, now)
                    .SetProperty(r => r.RevokedReason, reason),
                ct);
    }

    private static CurrentPrincipal ToPrincipal(StaffMember staff) => new()
    {
        Id = staff.Id,
        PrincipalType = PrincipalType.Staff,
        Role = ToPrincipalRole(staff.Role),
        DisplayName = staff.FullName,
        Email = staff.Email,
        PhoneNumber = null,
        PatientId = null
    };

    private static CurrentPrincipal ToPrincipal(PatientAccount account) => new()
    {
        Id = account.Id,
        PrincipalType = PrincipalType.Patient,
        Role = PrincipalRole.Patient,
        DisplayName = account.FullName,
        Email = null,
        PhoneNumber = account.PhoneNumber,

        PatientId = null
    };

    private static CurrentPrincipal? ToPrincipalOrNull(StaffMember? staff)
        => staff is null ? null : ToPrincipal(staff);

    private static CurrentPrincipal? ToPrincipalOrNull(PatientAccount? account)
        => account is null ? null : ToPrincipal(account);

    // By name, not by position: reordering either enum must not promote a nurse to an administrator.
    private static PrincipalRole ToPrincipalRole(StaffRole role)
        => Enum.Parse<PrincipalRole>(role.ToString());
}
