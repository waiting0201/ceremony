using Ceremony.Application.Auth;
using Ceremony.Domain.Exceptions;

namespace Ceremony.Application.Admins;

/// <summary>
/// 軟刪除管理者（IsEnabled = 0）。
/// </summary>
/// <remarks>
/// Legacy: AdminsForm.cs:134-158 (tsmiDelete_Click)。
/// - 軟刪除而非硬刪除：對齊 :143-146 `admin.IsEnabled = false`
/// </remarks>
public sealed class DeleteAdminHandler(IAdminRepository repo)
{
    public async Task HandleAsync(int id, CancellationToken ct = default)
    {
        var ok = await repo.SoftDeleteAsync(id, ct);
        if (!ok)
            throw new DomainException("ADMIN_NOT_FOUND", "找不到管理者");
    }
}
