namespace Ceremony.Application.Auth;

/// <summary>
/// 已撤銷的 JWT (by jti) 黑名單；命中即視為失效。
/// </summary>
/// <remarks>
/// Blueprint: docs/blueprints/api-endpoints/post-auth-logout.md
/// 單機 in-memory 實作；多 instance 後續換 Redis。
/// </remarks>
public interface IJwtBlacklist
{
    /// <summary>撤銷指定 jti；ttl 應 = 該 token 剩餘壽命，過期後黑名單可釋放。</summary>
    void Revoke(string jti, TimeSpan ttl);

    /// <summary>檢查 jti 是否在黑名單。</summary>
    bool IsRevoked(string jti);
}
