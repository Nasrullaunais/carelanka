namespace CareLanka.Api.Common.Auth;

public static class RateLimitPolicies
{
    // Per-IP only. The per-account half is LoginThrottle, because a botnet spread
    // across a thousand addresses trips no per-IP limit at all.
    public const string Auth = "auth";
}
