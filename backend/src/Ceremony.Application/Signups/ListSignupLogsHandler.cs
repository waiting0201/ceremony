namespace Ceremony.Application.Signups;

/// <summary>
/// 取得某筆報名的變更紀錄。
/// </summary>
/// <remarks>
/// Legacy: SignupLogForm.cs:26-45 (constructor + LoadSignupLog)
/// Blueprint: docs/blueprints/api-endpoints/get-signup-logs.md
/// Coverage:  docs/blueprints/legacy-coverage/signup-log-form.md (rows 1, 2)
/// </remarks>
public sealed class ListSignupLogsHandler(ISignupLogRepository repo)
{
    public async Task<SignupLogListResponse> HandleAsync(Guid signupId, CancellationToken ct = default)
    {
        var items = await repo.GetBySignupIdAsync(signupId, ct);
        return new SignupLogListResponse(items, items.Count);
    }
}
