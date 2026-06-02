using Ceremony.Domain.Exceptions;

namespace Ceremony.Application.Believers;

/// <summary>
/// 編輯信眾（全欄位覆寫）。
/// </summary>
/// <remarks>
/// Legacy: BelieverForm.cs:154-185 (btnConfirm_Click edit path)
/// Blueprint: docs/blueprints/api-endpoints/put-believer.md
/// Coverage:  docs/blueprints/legacy-coverage/believer-form.md (row 5)
/// </remarks>
public sealed class UpdateBelieverHandler(IBelieverRepository repo)
{
    public async Task<BelieverListItem> HandleAsync(Guid id, BelieverUpsertRequest req, CancellationToken ct = default)
    {
        var write = BelieverWriteValidator.ValidateAndNormalize(req);
        var updated = await repo.UpdateAsync(id, write, ct);
        if (!updated)
            throw new DomainException("BELIEVER_NOT_FOUND", "找不到信眾");
        return await repo.GetByIdAsync(id, ct)
            ?? throw new DomainException("INTERNAL_ERROR", "更新後無法讀回該筆資料");
    }
}
