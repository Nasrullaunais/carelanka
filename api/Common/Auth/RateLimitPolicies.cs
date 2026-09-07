namespace CareLanka.Api.Common.Auth;

/// <summary>
/// Rate limiter policy names, registered once in <c>Program.cs</c>. Same reasoning as
/// <see cref="Policies"/>: a constant fails at compile time, a typed string fails at
/// runtime and only under load.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>
    /// Per-IP fixed window on the unauthenticated auth endpoints. The per-account half of
    /// the limit lives in <c>LoginThrottle</c>, because a botnet spread across a thousand
    /// addresses trips no per-IP limit at all.
    /// </summary>
    public const string Auth = "auth";
}
