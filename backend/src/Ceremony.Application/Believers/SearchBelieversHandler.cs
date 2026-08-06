using Ceremony.Domain.Exceptions;

namespace Ceremony.Application.Believers;

/// <summary>
/// 搜尋信眾。需要至少一個非空條件。
/// </summary>
/// <remarks>
/// Legacy: BelieverForm.cs:35-44 (btnSearch_Click) + :353-409 (LoadBelievers)
/// Blueprint: docs/blueprints/api-endpoints/get-believers.md
/// Coverage:  docs/blueprints/legacy-coverage/believer-form.md (rows 2, 13)
/// </remarks>
public sealed class SearchBelieversHandler(IBelieverRepository repo)
{
    public async Task<BelieverListResponse> HandleAsync(BelieverSearchQuery query, CancellationToken ct = default)
    {
        // Trim 全部條件
        var normalized = new BelieverSearchQuery(
            Trim(query.Name),
            // 電話一律轉半形再查：寫入端（BelieverWriteValidator / *SignupHandler）都已 ToNarrow，
            // 條件若留全形永遠撈不到。姓名/堂號/名單不轉，全形是資料本身的一部分。
            ToNarrow(Trim(query.Phone)),
            Trim(query.HallName),
            Trim(query.LivingName),
            Trim(query.DeadName),
            Trim(query.SearchKey));

        // 至少一個非空 — 對齊 BelieverForm.cs:37（searchKey 單獨給也算數，對齊舊 LoadBelievers 的 txtQ）
        if (string.IsNullOrEmpty(normalized.Name)
            && string.IsNullOrEmpty(normalized.Phone)
            && string.IsNullOrEmpty(normalized.HallName)
            && string.IsNullOrEmpty(normalized.LivingName)
            && string.IsNullOrEmpty(normalized.DeadName)
            && string.IsNullOrEmpty(normalized.SearchKey))
        {
            throw new DomainException("VALIDATION_REQUIRED", "請輸入搜尋條件");
        }

        var items = await repo.SearchAsync(normalized, ct);
        return new BelieverListResponse(items, items.Count);
    }

    private static string? Trim(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>全形 → 半形（同 BelieverWriteValidator.ToNarrow）。</summary>
    private static string? ToNarrow(string? s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return new string(s.Select(c => c switch
        {
            >= '！' and <= '～' => (char)(c - 0xFEE0),
            '　' => ' ',
            _ => c,
        }).ToArray());
    }
}
