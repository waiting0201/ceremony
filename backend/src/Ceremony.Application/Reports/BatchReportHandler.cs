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
        if (req.NumberEnd < req.NumberStart)
            throw new DomainException("VALIDATION_INVALID", "編號錯誤");

        var reportType = (req.ReportType ?? string.Empty).Trim().ToLowerInvariant();
        if (reportType is not ("datacard" or "receipt" or "tablet" or "text" or "worship"))
            throw new DomainException("VALIDATION_INVALID", "報表類型錯誤");

        // Worship 防呆：限定 SignupType=4，若呼叫端沒給就強制加（比舊系統嚴格）
        var signupTypeFilter = reportType == "worship" ? 4 : req.SignupType;

        var query = new SignupRangeQuery(
            NumberStart: req.NumberStart,
            NumberEnd: req.NumberEnd,
            Year: req.Year,
            YearGte: req.YearGte,
            CeremonyCategoryId: req.CeremonyCategoryId,
            SignupType: signupTypeFilter);

        var signups = await repo.SearchByNumberRangeAsync(query, ct);
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
        var fileName = $"batch-{reportType}-{req.NumberStart}-{req.NumberEnd}.pdf";
        return (merged, fileName, signups.Count);
    }
}

public sealed record BatchReportRequest(
    string ReportType,
    int NumberStart,
    int NumberEnd,
    int? Year = null,
    bool YearGte = false,
    Guid? CeremonyCategoryId = null,
    int? SignupType = null);
