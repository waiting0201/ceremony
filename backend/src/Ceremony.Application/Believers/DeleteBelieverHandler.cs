using Ceremony.Domain.Exceptions;

namespace Ceremony.Application.Believers;

/// <summary>
/// 刪除信眾（硬刪除，受報名衝突保護）。
/// </summary>
/// <remarks>
/// Legacy: BelieverForm.cs:211-250 (tsmiDelete_Click)
/// 注意：硬刪除而非軟刪除（Believers 表無 IsEnabled 欄位）。
/// Blueprint: docs/blueprints/api-endpoints/delete-believer.md
/// Coverage:  docs/blueprints/legacy-coverage/believer-form.md (row 8)
/// </remarks>
public sealed class DeleteBelieverHandler(IBelieverRepository repo)
{
    public async Task HandleAsync(Guid id, CancellationToken ct = default)
    {
        var name = await repo.GetNameAsync(id, ct);
        if (name is null)
            throw new DomainException("BELIEVER_NOT_FOUND", "找不到信眾");

        if (await repo.HasSignupsAsync(id, ct))
            throw new DomainException("BELIEVER_HAS_SIGNUPS", $"{name} 已有報名資料，不能刪除！");

        var deleted = await repo.DeleteAsync(id, ct);
        if (!deleted)
            throw new DomainException("BELIEVER_NOT_FOUND", "找不到信眾");
    }
}
