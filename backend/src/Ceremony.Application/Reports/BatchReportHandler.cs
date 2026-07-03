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
/// </remarks>
public sealed class BatchReportHandler(ISignupRepository repo, IReportRenderer renderer, IPdfMerger merger)
{
    public async Task<(byte[] Pdf, string FileName, int SignupCount)> HandleAsync(BatchReportRequest req, CancellationToken ct = default)
    {
        // 兩種選取模式：SignupIds（勾選的任意幾筆，不論編號是否連續）優先於編號區間
        var useIds = req.SignupIds is { Count: > 0 };

        if (!useIds && (req.NumberStart is null || req.NumberEnd is null || req.NumberEnd < req.NumberStart))
            throw new DomainException("VALIDATION_INVALID", "編號錯誤");

        var reportType = (req.ReportType ?? string.Empty).Trim().ToLowerInvariant();
        if (reportType is not ("datacard" or "receipt" or "tablet" or "text" or "worship"))
            throw new DomainException("VALIDATION_INVALID", "報表類型錯誤");

        IReadOnlyList<SignupListItem> signups;
        string fileName;

        if (useIds)
        {
            signups = await repo.SearchByIdsAsync(req.SignupIds!, ct);
            // Worship 防呆：跟編號區間模式一致，混選時只印其中 SignupType=4 的部分
            if (reportType == "worship")
                signups = signups.Where(s => s.SignupType == 4).ToList();
            fileName = $"batch-{reportType}-selected-{signups.Count}.pdf";
        }
        else
        {
            // Worship 防呆：限定 SignupType=4，若呼叫端沒給就強制加（比舊系統嚴格）
            var signupTypeFilter = reportType == "worship" ? 4 : req.SignupType;

            var query = new SignupRangeQuery(
                NumberStart: req.NumberStart!.Value,
                NumberEnd: req.NumberEnd!.Value,
                Year: req.Year,
                YearGte: req.YearGte,
                CeremonyCategoryId: req.CeremonyCategoryId,
                SignupType: signupTypeFilter);

            signups = await repo.SearchByNumberRangeAsync(query, ct);
            fileName = $"batch-{reportType}-{req.NumberStart}-{req.NumberEnd}.pdf";
        }

        if (signups.Count == 0)
            throw new DomainException("BATCH_NO_SIGNUPS", "查無符合條件的報名資料");

        var now = DateTime.Now;
        var pdfs = new List<byte[]>(signups.Count);
        foreach (var s in signups)
        {
            pdfs.Add(reportType switch
            {
                "datacard" => renderer.RenderDataCard(ReportModelBuilders.DataCard(s)),
                "receipt" => renderer.RenderReceipt(ReportModelBuilders.Receipt(s, now)),
                "tablet" => renderer.RenderTablet(ReportModelBuilders.Tablet(s)),
                "text" => renderer.RenderText(ReportModelBuilders.Text(s)),
                "worship" => renderer.RenderWorship(ReportModelBuilders.Worship(s)),
                _ => throw new InvalidOperationException(),
            });
        }

        var merged = merger.Merge(pdfs);
        return (merged, fileName, signups.Count);
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
