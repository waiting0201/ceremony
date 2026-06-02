using Ceremony.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ceremony.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(LoginHandler login, LogoutHandler logout) : ControllerBase
{
    /// <summary>管理者登入（明文密碼比對 + 後門帳號 + 失敗鎖定）</summary>
    /// <remarks>
    /// Legacy: LoginForm.cs:31-81 (btnConfirm_Click + ValidateUser)
    /// Blueprint: docs/blueprints/api-endpoints/post-auth-login.md
    /// Coverage:  docs/blueprints/legacy-coverage/login-form.md (rows 2-3)
    /// </remarks>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var result = await login.HandleAsync(request, ct);
        return Ok(result);
    }

    /// <summary>登出 — 撤銷當前 JWT；後續同 token 請求 401</summary>
    /// <remarks>
    /// 新需求；舊系統無對應（WinForms close form 即結束 session）
    /// Blueprint: docs/blueprints/api-endpoints/post-auth-logout.md
    /// </remarks>
    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        logout.Handle(User);
        return Ok(new { ok = true });
    }
}
