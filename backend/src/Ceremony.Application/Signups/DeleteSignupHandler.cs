using Ceremony.Domain.Exceptions;

namespace Ceremony.Application.Signups;

/// <summary>
/// 刪除報名（硬刪除）。
/// </summary>
/// <remarks>
/// Legacy: SignupForm.cs:405-426 (tsmiDelete_Click)
/// Blueprint: docs/blueprints/api-endpoints/delete-signup.md
/// Coverage:  docs/blueprints/legacy-coverage/signup-form.md (row 14)
/// </remarks>
public sealed class DeleteSignupHandler(ISignupRepository repo)
{
    public async Task HandleAsync(Guid id, CancellationToken ct = default)
    {
        var deleted = await repo.DeleteAsync(id, ct);
        if (!deleted)
            throw new DomainException("SIGNUP_NOT_FOUND", "找不到報名");
    }
}
