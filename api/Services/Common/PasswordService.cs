using Microsoft.AspNetCore.Identity;

namespace CareLanka.Api.Services.Common;

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
