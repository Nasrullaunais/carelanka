namespace CareLanka.Api.Common.Auth;

/// <summary>
/// The shape of a patient username, in one place because the register and login
/// DTOs both have to agree with it and with what the spec publishes.
/// </summary>
public static class UsernameRules
{
    public const int MinLength = 3;
    public const int MaxLength = 50;

    /// <summary>Letters, digits, dot, underscore and hyphen.</summary>
    public const string Pattern = "^[a-zA-Z0-9._-]+$";

    /// <summary>
    /// Lower-cased, so <c>Chathura</c> and <c>chathura</c> are the same account
    /// on both the register and the login path.
    /// </summary>
    public static string Normalise(string username) => username.Trim().ToLowerInvariant();
}
