namespace Ceremony.Application.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public bool BackdoorEnabled { get; set; } = true;
    public string BackdoorUsername { get; set; } = "weypro";
    public string BackdoorPassword { get; set; } = "weypro12ab";

    public int FailedLoginThreshold { get; set; } = 5;
    public int FailedLoginLockMinutes { get; set; } = 15;
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "https://ceremony.local";
    public string Audience { get; set; } = "ceremony-client";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 30;
}
