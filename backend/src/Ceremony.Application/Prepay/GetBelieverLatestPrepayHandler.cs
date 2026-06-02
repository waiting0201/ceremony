using Ceremony.Application.Signups;

namespace Ceremony.Application.Prepay;

/// <summary>
/// 取某信眾「今年(含)以前最新一筆報名」的預繳資訊，供新增報名選信眾時自動帶入預繳年/法會。
/// 對應舊 <c>NewSignupForm.BelieverSelected:1102-1115</c>。
/// </summary>
/// <remarks>
/// 純唯讀；查無報名或最新報名無預繳 → 回各欄為 null 的結果（前端只在 PrepayYear 有值時才預填）。
/// </remarks>
public sealed class GetBelieverLatestPrepayHandler(ISignupRepository repo)
{
    public async Task<BelieverLatestPrepayResult> HandleAsync(Guid believerId, int year, CancellationToken ct = default)
    {
        var result = await repo.GetLatestPrepayByBelieverAsync(believerId, year, ct);
        return result ?? new BelieverLatestPrepayResult(null, null, null);
    }
}
