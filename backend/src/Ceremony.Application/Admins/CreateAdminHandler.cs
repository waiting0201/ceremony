using Ceremony.Application.Auth;
using Ceremony.Domain.Exceptions;

namespace Ceremony.Application.Admins;

/// <summary>
/// 新增管理者。
/// </summary>
/// <remarks>
/// Legacy: AdminsForm.cs:88-105 (btnConfirm_Click insert path) + :160-187 (username uniqueness) + :189-196 (password required)
/// Blueprint: docs/blueprints/api-endpoints/post-admins.md
/// Coverage:  docs/blueprints/legacy-coverage/admins-form.md (rows 3, 6, 9, 10)
/// </remarks>
public sealed class CreateAdminHandler(IAdminRepository repo)
{
    public async Task<AdminListItem> HandleAsync(CreateAdminRequest req, CancellationToken ct = default)
    {
        var username = req.Username?.Trim() ?? string.Empty;
        var password = req.Password?.Trim() ?? string.Empty;
        var name = req.Name?.Trim();

        // 驗證 — 對齊 AdminsForm.cs:162 (username) / :191 (password)
        if (string.IsNullOrEmpty(username))
            throw new DomainException("VALIDATION_REQUIRED", "請輸入帳號");
        if (string.IsNullOrEmpty(password))
            throw new DomainException("VALIDATION_REQUIRED", "請輸入密碼");
        if (password.Length > 20)
            throw new DomainException("VALIDATION_LENGTH", "密碼最多 20 個字");
        if (username.Length > 50)
            throw new DomainException("VALIDATION_LENGTH", "帳號最多 50 個字");
        if (name is { Length: > 50 })
            throw new DomainException("VALIDATION_LENGTH", "姓名最多 50 個字");

        // 唯一性 — 對齊 AdminsForm.cs:173
        var exists = await repo.UsernameExistsAsync(username, excludeId: null, ct);
        if (exists)
            throw new DomainException("ADMIN_DUPLICATE_USERNAME", "帳號重複，請重新確認！");

        var newId = await repo.InsertAsync(username, password, name, ct);
        return new AdminListItem(newId, username, name);
    }
}
