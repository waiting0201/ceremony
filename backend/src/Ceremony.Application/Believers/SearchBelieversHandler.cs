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
            Trim(query.Phone),
            Trim(query.HallName),
            Trim(query.LivingName),
            Trim(query.DeadName));

        // 至少一個非空 — 對齊 BelieverForm.cs:37
        if (string.IsNullOrEmpty(normalized.Name)
            && string.IsNullOrEmpty(normalized.Phone)
            && string.IsNullOrEmpty(normalized.HallName)
            && string.IsNullOrEmpty(normalized.LivingName)
            && string.IsNullOrEmpty(normalized.DeadName))
        {
            throw new DomainException("VALIDATION_REQUIRED", "請輸入搜尋條件");
        }

        var items = await repo.SearchAsync(normalized, ct);
        return new BelieverListResponse(items, items.Count);
    }

    private static string? Trim(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
