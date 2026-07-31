namespace Ceremony.Application.Signups;

/// <summary>
/// 報名搜尋。
/// </summary>
/// <remarks>
/// Legacy: SignupForm.cs:71-74 (btnSearch_Click) + :807-864 (LoadSearchSignups PredicateBuilder AND/OR)
/// Blueprint: docs/blueprints/api-endpoints/get-signups.md
/// Coverage:  docs/blueprints/legacy-coverage/signup-form.md (rows 1, 2, 24)
/// </remarks>
public sealed class SearchSignupsHandler(ISignupRepository repo)
{
    public async Task<SignupListResponse> HandleAsync(SignupSearchQuery query, CancellationToken ct = default)
    {
        var normalized = Normalize(query);
        var items = await repo.SearchAsync(normalized, ct);
        return new SignupListResponse(items, items.Count);
    }

    private static SignupSearchQuery Normalize(SignupSearchQuery q)
    {
        // 編號區間：0 視同未填（沿用舊系統語意）；只填一端 → 兩端同值＝只查那一筆編號。
        // 起 > 迄 不在這裡擋（前端已擋並提示「編號錯誤」，對齊批次列印）；真的送進來就是 >=/<= 交集為空。
        var start = q.NumberStart is 0 ? null : q.NumberStart;
        var end = q.NumberEnd is 0 ? null : q.NumberEnd;
        start ??= end;
        end ??= start;

        // 對齊舊系統的「空值/sentinel = 不加 WHERE」邏輯
        return q with
        {
            SearchKey = string.IsNullOrWhiteSpace(q.SearchKey) ? null : q.SearchKey.Trim(),
            CeremonyCategoryId = q.CeremonyCategoryId == Guid.Empty ? null : q.CeremonyCategoryId,
            SignupType = q.SignupType is -1 ? null : q.SignupType,
            NumberStart = start,
            NumberEnd = end,
        };
    }
}
