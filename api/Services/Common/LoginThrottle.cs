using CareLanka.Api.Common.Exceptions;
using Microsoft.Extensions.Caching.Memory;

namespace CareLanka.Api.Services.Common;

// In memory, so it resets on restart and is not shared between instances. Deliberate for one
// deployment: the per-IP middleware limit is the broad defence and this is the narrow one.
public sealed class LoginThrottle : ILoginThrottle
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly IMemoryCache _cache;

    public LoginThrottle(IMemoryCache cache) => _cache = cache;

    public void EnsureNotLockedOut(string accountKey)
    {
        if (_cache.TryGetValue(Key(accountKey), out int failures) && failures >= MaxFailures)
        {
            throw new TooManyRequestsException();
        }
    }

    public void RecordFailure(string accountKey)
    {
        var key = Key(accountKey);
        var failures = _cache.TryGetValue(key, out int existing) ? existing + 1 : 1;

        _cache.Set(key, failures, Window);
    }

    public void RecordSuccess(string accountKey) => _cache.Remove(Key(accountKey));

    private static string Key(string accountKey) => $"login-failures:{accountKey.ToLowerInvariant()}";
}
