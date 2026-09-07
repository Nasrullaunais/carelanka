using Microsoft.AspNetCore.Identity;

namespace CareLanka.Api.Services.Common;

/// <summary>
/// ASP.NET Core's <see cref="PasswordHasher{TUser}"/>: PBKDF2 with a per-password random
/// salt and a version byte embedded in the hash, so the work factor can be raised later
/// without invalidating anything already stored.
/// <para>
/// <strong>Do not replace this with hand-written crypto.</strong> Not a style preference —
/// rolling your own password hashing is the single most common way to lose the security
/// marks, and there is no upside here.
/// </para>
/// </summary>
public sealed class PasswordService : IPasswordService
{
    // The generic parameter is only a type tag; the hasher never touches the object.
    private readonly PasswordHasher<object> _hasher = new();
    private static readonly object Dummy = new();

    public string Hash(string password) => _hasher.HashPassword(Dummy, password);

    public bool Verify(string storedHash, string password, out bool needsRehash)
    {
        var result = _hasher.VerifyHashedPassword(Dummy, storedHash, password);

        needsRehash = result == PasswordVerificationResult.SuccessRehashNeeded;

        return result is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
