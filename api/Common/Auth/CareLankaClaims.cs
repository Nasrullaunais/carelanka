namespace CareLanka.Api.Common.Auth;

public static class CareLankaClaims
{
    public const string Subject = "sub";

    public const string Role = "role";

    // Which table Subject is in. "sub" alone is ambiguous: staff and patient-account
    // ids are both GUIDs, from different tables.
    public const string PrincipalType = "typ";

    public const string TokenId = "jti";
}
