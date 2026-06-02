using Microsoft.Extensions.Caching.Memory;

namespace Ceremony.Application.Auth;

/// <summary>
/// IMemoryCache 實作的 JWT 黑名單；單機 only。
/// </summary>
public sealed class MemoryJwtBlacklist(IMemoryCache cache) : IJwtBlacklist
{
    private const string Prefix = "jwt-blacklist:";

    public void Revoke(string jti, TimeSpan ttl)
    {
        if (string.IsNullOrWhiteSpace(jti)) return;
        if (ttl <= TimeSpan.Zero) return;  // 已過期 token 無須加入
        cache.Set(Prefix + jti, true, ttl);
    }

    public bool IsRevoked(string jti)
        => !string.IsNullOrWhiteSpace(jti) && cache.TryGetValue(Prefix + jti, out _);
}
