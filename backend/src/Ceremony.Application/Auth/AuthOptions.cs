namespace Ceremony.Application.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    // 系統內建 SuperAdmin 帳號（非 DB；不存在於 Admins 表）。取代舊系統的 weypro 後門。
    public bool SuperAdminEnabled { get; set; } = true;
    public string SuperAdminUsername { get; set; } = "sa@system.local";
    public string SuperAdminPassword { get; set; } = "Admin@123";

    public int FailedLoginThreshold { get; set; } = 5;
    public int FailedLoginLockMinutes { get; set; } = 15;
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "https://ceremony.local";
    public string Audience { get; set; } = "ceremony-client";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 600;
}
