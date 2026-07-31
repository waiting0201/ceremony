using Ceremony.Application.Signups;
using Ceremony.Domain.Exceptions;

namespace Ceremony.Application.Reports;

/// <summary>
/// 按編號範圍 + 條件批次列印同一類報表，合併成單一 PDF。
/// </summary>
/// <remarks>
/// Legacy: SignupForm.cs:447-653 (btnPrint_Click) + :1698-1722 (CombinePDFs PdfSharp)
/// Blueprint: docs/blueprints/api-endpoints/post-reports-batch.md
/// Coverage:  docs/blueprints/legacy-coverage/signup-form.md rows 16, 33
///
/// 2026-07-28 拆成 <see cref="ResolveAsync"/>（驗證＋查詢，同步）與 <see cref="BatchReportComposer.Render"/>
/// （渲染＋合併，可回報進度／可取消），供 job 版端點使用；<see cref="HandleAsync"/> 行為與簽章維持不變。
/// </remarks>
public sealed class BatchReportHandler(ISignupRepository repo, IReportRenderer renderer, IPdfMerger merger)
{
    public async Task<(byte[] Pdf, string FileName, int SignupCount)> HandleAsync(BatchReportRequest req, CancellationToken ct = default)
    {
        var plan = await ResolveAsync(req, ct);
        var merged = BatchReportComposer.Render(renderer, merger, plan, null, ct);
        return (merged, plan.FileName, plan.Signups.Count);
    }

    /// <summary>
    /// 驗證請求、查出要印的 signups、決定檔名。不做任何渲染。
    /// </summary>
    /// <exception cref="DomainException">
    /// VALIDATION_INVALID（編號錯誤／報表類型錯誤）、BATCH_NO_SIGNUPS（查無符合條件的報名資料）
    /// </exception>
    public async Task<BatchReportPlan> ResolveAsync(BatchReportRequest req, CancellationToken ct = default)
    {
        // 兩種選取模式：SignupIds（勾選的任意幾筆，不論編號是否連續）優先於編號區間
        var useIds = req.SignupIds is { Count: > 0 };

        // 編號區間（2026-07-31）：只填一端＝該端當起也當迄，只印那一筆編號；兩端皆空才算「編號錯誤」。
        var numberStart = req.NumberStart ?? req.NumberEnd;
        var numberEnd = req.NumberEnd ?? req.NumberStart;

        if (!useIds && (numberStart is null || numberEnd is null || numberEnd < numberStart))
            throw new DomainException("VALIDATION_INVALID", "編號錯誤");

        var reportType = (req.ReportType ?? string.Empty).Trim().ToLowerInvariant();
        if (reportType is not ("datacard" or "receipt" or "tablet" or "text" or "worship" or "worshipcard"))
            throw new DomainException("VALIDATION_INVALID", "報表類型錯誤");

        IReadOnlyList<SignupListItem> signups;
        string fileName;

        if (useIds)
        {
            signups = await repo.SearchByIdsAsync(req.SignupIds!, ct);
            fileName = $"batch-{reportType}-selected-{signups.Count}.pdf";
        }
        else
        {
            // 普桌不另限 SignupType — 對齊舊系統批次 case 5：只跟隨呼叫端的搜尋篩選
            var query = new SignupRangeQuery(
                NumberStart: numberStart!.Value,
                NumberEnd: numberEnd!.Value,
                Year: req.Year,
                YearGte: req.YearGte,
                CeremonyCategoryId: req.CeremonyCategoryId,
                SignupType: req.SignupType);

            signups = await repo.SearchByNumberRangeAsync(query, ct);
            fileName = $"batch-{reportType}-{numberStart}-{numberEnd}.pdf";
        }

        if (signups.Count == 0)
            throw new DomainException("BATCH_NO_SIGNUPS", "查無符合條件的報名資料");

        return new BatchReportPlan(reportType, fileName, signups);
    }
}

public sealed record BatchReportRequest(
    string ReportType,
    int? NumberStart = null,
    int? NumberEnd = null,
    int? Year = null,
    bool YearGte = false,
    Guid? CeremonyCategoryId = null,
    int? SignupType = null,
    IReadOnlyList<Guid>? SignupIds = null);
