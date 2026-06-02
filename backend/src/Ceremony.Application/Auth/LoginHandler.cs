using System.Security.Cryptography;
using System.Text;
using Ceremony.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace Ceremony.Application.Auth;

/// <summary>
/// 處理管理者登入。
/// </summary>
/// <remarks>
/// Legacy: LoginForm.cs:31-81 (btnConfirm_Click + ValidateUser)
/// Blueprint: docs/blueprints/api-endpoints/post-auth-login.md
/// Coverage:  docs/blueprints/legacy-coverage/login-form.md (rows 2-3)
/// </remarks>
public sealed class LoginHandler(
    IAdminRepository repo,
    JwtTokenService tokens,
    LoginFailureTracker failures,
    IOptions<AuthOptions> authOptions)
{
    private readonly AuthOptions _auth = authOptions.Value;

    public async Task<LoginResponse> HandleAsync(LoginRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Username))
            throw new DomainException("VALIDATION_REQUIRED", "請輸入帳號！");
        if (string.IsNullOrWhiteSpace(req.Password))
            throw new DomainException("VALIDATION_REQUIRED", "請輸入密碼！");

        var window = TimeSpan.FromMinutes(_auth.FailedLoginLockMinutes);

        // 後門帳號（沿用舊系統）— LoginForm.cs:60-65
        if (_auth.BackdoorEnabled
            && req.Username == _auth.BackdoorUsername
            && FixedTimeEquals(req.Password, _auth.BackdoorPassword))
        {
            failures.Reset(req.Username);
            var backdoorToken = tokens.Issue(adminId: 0, username: _auth.BackdoorUsername, name: "Administrator");
            return new LoginResponse(backdoorToken, new LoginUser(0, _auth.BackdoorUsername, "Administrator"));
        }

        // DB 查詢 — LoginForm.cs:67-78
        var admin = await repo.GetByUsernameAsync(req.Username, ct);
        if (admin is null || !admin.IsEnabled || !FixedTimeEquals(admin.Password, req.Password))
        {
            var attempts = failures.IncrementAndGet(req.Username, window);
            if (attempts >= _auth.FailedLoginThreshold)
                throw new DomainException("AUTH_ACCOUNT_LOCKED",
                    $"登入失敗次數過多，請 {_auth.FailedLoginLockMinutes} 分鐘後再試");
            throw new DomainException("AUTH_INVALID_CREDENTIALS", "帳號或密碼錯誤！");
        }

        failures.Reset(req.Username);
        var token = tokens.Issue(admin.AdminId, admin.Username, admin.Name);
        return new LoginResponse(token, new LoginUser(admin.AdminId, admin.Username, admin.Name));
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
