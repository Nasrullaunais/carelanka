namespace CareLanka.Api.Services.Common;

/// <summary>
/// The per-account half of login rate limiting. The per-IP half is middleware and never
/// reaches a service.
/// <para>
/// Both are needed and they stop different things. Per-IP stops one machine hammering the
/// endpoint. Per-account stops a spread-out guessing run against one known email — a
/// botnet with a thousand addresses trips no per-IP limit at all.
/// </para>
/// </summary>
public interface ILoginThrottle
{
    /// <summary>Throws <c>TooManyRequestsException</c> when this account is locked out.</summary>
    void EnsureNotLockedOut(string accountKey);

    void RecordFailure(string accountKey);

    void RecordSuccess(string accountKey);
}
