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
        // 對齊舊系統的「空值/sentinel = 不加 WHERE」邏輯
        return q with
        {
            SearchKey = string.IsNullOrWhiteSpace(q.SearchKey) ? null : q.SearchKey.Trim(),
            CeremonyCategoryId = q.CeremonyCategoryId == Guid.Empty ? null : q.CeremonyCategoryId,
            SignupType = q.SignupType is -1 ? null : q.SignupType,
            Number = q.Number is 0 ? null : q.Number,
        };
    }
}
