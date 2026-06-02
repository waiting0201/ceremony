using Ceremony.Application.Auth;
using Ceremony.Domain.Exceptions;

namespace Ceremony.Application.Admins;

/// <summary>
/// 更新管理者（修改 name 與選擇性修改 password；username 不可變更）。
/// </summary>
/// <remarks>
/// Legacy: AdminsForm.cs:88-122 (btnConfirm_Click update path)。
/// - username 不變更：對齊 :84 `txtUsername.Enabled = false`
/// - 修改 name + password：對齊 :108-114
/// - 密碼最多 20 字：對齊 Domain Admin schema
/// </remarks>
public sealed class UpdateAdminHandler(IAdminRepository repo)
{
    public async Task<AdminListItem> HandleAsync(int id, UpdateAdminRequest req, CancellationToken ct = default)
    {
        var existing = await repo.GetByIdAsync(id, ct);
        if (existing is null)
            throw new DomainException("ADMIN_NOT_FOUND", "找不到管理者");

        var name = req.Name?.Trim();
        if (name is { Length: > 50 })
            throw new DomainException("VALIDATION_LENGTH", "姓名最多 50 個字");

        string? password = null;
        if (req.Password is not null)
        {
            var trimmed = req.Password.Trim();
            if (string.IsNullOrEmpty(trimmed))
                throw new DomainException("VALIDATION_REQUIRED", "請輸入密碼");
            if (trimmed.Length > 20)
                throw new DomainException("VALIDATION_LENGTH", "密碼最多 20 個字");
            password = trimmed;
        }

        var ok = await repo.UpdateAsync(id, password, name, ct);
        if (!ok)
            throw new DomainException("ADMIN_NOT_FOUND", "找不到管理者");

        return new AdminListItem(id, existing.Username, name);
    }
}
