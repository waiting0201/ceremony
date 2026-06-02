using Ceremony.Application.Auth;

namespace Ceremony.Application.Admins;

/// <summary>
/// 列出所有啟用的管理者。
/// </summary>
/// <remarks>
/// Legacy: AdminsForm.cs:207-213 (LoadAdmins helper)
/// Blueprint: docs/blueprints/api-endpoints/get-admins.md
/// Coverage:  docs/blueprints/legacy-coverage/admins-form.md (rows 1, 12)
/// </remarks>
public sealed class ListAdminsHandler(IAdminRepository repo)
{
    public async Task<AdminListResponse> HandleAsync(CancellationToken ct = default)
    {
        var items = await repo.GetAllEnabledAsync(ct);
        return new AdminListResponse(items, items.Count);
    }
}
