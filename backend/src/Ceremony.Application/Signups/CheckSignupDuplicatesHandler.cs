namespace Ceremony.Application.Signups;

/// <summary>
/// 重複報名警示查詢：某信眾在同一 (Year, CeremonyCategoryID) 是否已有報名（忽略 SignupType）。
/// 純警示用途，不阻擋報名；查無回空清單、不丟例外。
/// </summary>
/// <remarks>
/// Legacy: 無對應（舊系統不檢查信眾重複報名）。新版刻意增強。
/// Blueprint: docs/blueprints/api-endpoints/get-signup-duplicates.md
/// </remarks>
public sealed class CheckSignupDuplicatesHandler(ISignupRepository repo)
{
    public async Task<SignupDuplicateListResponse> HandleAsync(
        int year,
        Guid ceremonyCategoryId,
        Guid believerId,
        Guid? excludeSignupId,
        CancellationToken ct = default)
    {
        // 缺任一鍵 → 視為無從判定，回空清單（前端不該打到，但保險）
        if (year <= 0 || ceremonyCategoryId == Guid.Empty || believerId == Guid.Empty)
            return new SignupDuplicateListResponse([], 0);

        var normalizedExclude = excludeSignupId == Guid.Empty ? null : excludeSignupId;
        var items = await repo.FindDuplicatesByBelieverAsync(year, ceremonyCategoryId, believerId, normalizedExclude, ct);
        return new SignupDuplicateListResponse(items, items.Count);
    }
}
