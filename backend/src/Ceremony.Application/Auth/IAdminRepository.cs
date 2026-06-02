using Ceremony.Application.Admins;
using Ceremony.Domain.Entities;

namespace Ceremony.Application.Auth;

public interface IAdminRepository
{
    Task<Admin?> GetByUsernameAsync(string username, CancellationToken ct = default);

    Task<IReadOnlyList<AdminListItem>> GetAllEnabledAsync(CancellationToken ct = default);

    Task<bool> UsernameExistsAsync(string username, int? excludeId, CancellationToken ct = default);

    Task<int> InsertAsync(string username, string password, string? name, CancellationToken ct = default);

    /// <summary>更新管理者（password 為 null 時不變更密碼）；username 不可變更</summary>
    Task<bool> UpdateAsync(int id, string? password, string? name, CancellationToken ct = default);

    /// <summary>軟刪除（IsEnabled = 0）— 對齊 AdminsForm.cs:143-146</summary>
    Task<bool> SoftDeleteAsync(int id, CancellationToken ct = default);

    /// <summary>單筆讀取，含啟用狀態（給 update / delete handler 驗證用）</summary>
    Task<AdminListItem?> GetByIdAsync(int id, CancellationToken ct = default);
}
