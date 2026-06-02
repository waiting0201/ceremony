using Ceremony.Domain.Exceptions;

namespace Ceremony.Application.Believers;

/// <summary>
/// 取得單筆信眾完整資料。
/// </summary>
/// <remarks>
/// Legacy: BelieverForm.cs:57-99 (dgvBelievers_CellClick — GetByID 預填表單)
/// Blueprint: docs/blueprints/api-endpoints/get-believer-by-id.md
/// Coverage:  docs/blueprints/legacy-coverage/believer-form.md (row 4)
/// </remarks>
public sealed class GetBelieverHandler(IBelieverRepository repo)
{
    public async Task<BelieverListItem> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var believer = await repo.GetByIdAsync(id, ct);
        if (believer is null)
            throw new DomainException("BELIEVER_NOT_FOUND", "找不到信眾");
        return believer;
    }
}
