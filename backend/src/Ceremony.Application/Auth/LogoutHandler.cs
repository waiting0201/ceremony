using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Ceremony.Application.Auth;

/// <summary>
/// 撤銷 JWT — 把 jti 加進黑名單，TTL 設為 token 剩餘壽命。
/// </summary>
/// <remarks>
/// Blueprint: docs/blueprints/api-endpoints/post-auth-logout.md
/// </remarks>
public sealed class LogoutHandler(IJwtBlacklist blacklist)
{
    public void Handle(ClaimsPrincipal user)
    {
        var jti = user.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (string.IsNullOrWhiteSpace(jti)) return;

        var expClaim = user.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
        var ttl = TimeSpan.FromMinutes(600);  // fallback：與預設 access token 壽命同（10 小時）
        if (long.TryParse(expClaim, out var expSec))
        {
            var expUtc = DateTimeOffset.FromUnixTimeSeconds(expSec);
            var remaining = expUtc - DateTimeOffset.UtcNow;
            if (remaining > TimeSpan.Zero) ttl = remaining;
            else return;  // 已過期，不必加黑名單
        }

        blacklist.Revoke(jti, ttl);
    }
}
