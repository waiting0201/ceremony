using Microsoft.Extensions.Caching.Memory;

namespace Ceremony.Application.Auth;

/// <summary>
/// In-memory per-username login failure counter.
/// </summary>
/// <remarks>
/// Resets on process restart; single-instance only. See post-auth-login.md "風險" section.
/// </remarks>
public sealed class LoginFailureTracker(IMemoryCache cache)
{
    public int IncrementAndGet(string username, TimeSpan window)
    {
        var key = $"login-fail:{username.ToLowerInvariant()}";
        var current = cache.TryGetValue<int>(key, out var v) ? v : 0;
        current++;
        cache.Set(key, current, window);
        return current;
    }

    public void Reset(string username)
    {
        cache.Remove($"login-fail:{username.ToLowerInvariant()}");
    }
}
