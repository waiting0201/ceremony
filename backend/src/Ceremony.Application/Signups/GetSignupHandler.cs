using Ceremony.Domain.Exceptions;

namespace Ceremony.Application.Signups;

/// <summary>
/// 取得單筆報名完整資料（編輯預填用）。
/// </summary>
/// <remarks>
/// Legacy: EditSignupForm.cs:70-73 + :562-626 (BelieverSelected 預填)
/// Blueprint: docs/blueprints/api-endpoints/get-signup-by-id.md
/// Coverage:  docs/blueprints/legacy-coverage/edit-signup-form.md (row 2)
/// </remarks>
public sealed class GetSignupHandler(ISignupRepository repo)
{
    public async Task<SignupListItem> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var s = await repo.GetByIdAsync(id, ct);
        if (s is null)
            throw new DomainException("SIGNUP_NOT_FOUND", "找不到報名");
        return s;
    }
}
