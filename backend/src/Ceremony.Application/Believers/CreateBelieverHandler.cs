using Ceremony.Domain.Exceptions;

namespace Ceremony.Application.Believers;

/// <summary>
/// 新增信眾。
/// </summary>
/// <remarks>
/// Legacy: BelieverForm.cs:101-152 (btnConfirm_Click insert path)
/// Blueprint: docs/blueprints/api-endpoints/post-believers.md
/// Coverage:  docs/blueprints/legacy-coverage/believer-form.md (rows 3, 5, 12)
/// </remarks>
public sealed class CreateBelieverHandler(IBelieverRepository repo)
{
    public async Task<BelieverListItem> HandleAsync(BelieverUpsertRequest req, CancellationToken ct = default)
    {
        var write = BelieverWriteValidator.ValidateAndNormalize(req);
        var newId = await repo.InsertAsync(write, ct);
        var created = await repo.GetByIdAsync(newId, ct)
            ?? throw new DomainException("INTERNAL_ERROR", "新建後無法讀回該筆資料");
        return created;
    }
}
