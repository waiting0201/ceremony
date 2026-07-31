using Ceremony.Application.Signups;

namespace Ceremony.Application.Reports;

/// <summary>
/// 批次列印的「解析結果」：已確認報表類型合法、已查到要印的 signups、已決定檔名。
/// 由 <see cref="BatchReportHandler.ResolveAsync"/> 產出，交給 <see cref="BatchReportComposer"/> 渲染。
/// </summary>
/// <remarks>
/// 拆成兩段是為了讓「驗證＋查詢」留在同步的 HTTP request（錯誤碼/訊息不變、Total 立刻可知），
/// 只有耗時的 render+merge 進背景 job。Blueprint: docs/blueprints/api-endpoints/post-reports-batch-jobs.md
/// </remarks>
public sealed record BatchReportPlan(
    string ReportType,
    string FileName,
    IReadOnlyList<SignupListItem> Signups);

/// <summary>
/// <c>POST /reports/batch/plan</c> 的回應：這批要印哪些報名，但不渲染。
/// </summary>
/// <remarks>
/// 給前端做「大量列印分段」用：前端拿到有序清單後切成固定大小的段，逐段建 job 送印，
/// 峰值記憶體因此與總筆數無關，中途卡紙也只需重印那一段。
/// 選取的權威仍在 <see cref="BatchReportHandler.ResolveAsync"/>——前端只負責切，不重新查一次，
/// 否則「印出來的筆數」與「批次條件算出的筆數」有機會不一致。
/// Blueprint: docs/blueprints/api-endpoints/post-reports-batch-plan.md
/// </remarks>
public sealed record BatchReportPlanResponse(
    string ReportType,
    string FileName,
    int Total,
    IReadOnlyList<BatchReportPlanItem> Items);

/// <param name="Number">
/// 報名編號，可能為 null（尚未配號）。前端用來顯示「第 7 段：編號 1201–1400」——
/// 卡紙時使用者要能把段對上手裡那疊紙。
/// </param>
public sealed record BatchReportPlanItem(Guid Id, int? Number);

/// <summary>
/// 逐筆渲染 <see cref="BatchReportPlan"/> 的 signups 並合併成單一 PDF。
/// </summary>
/// <remarks>
/// Legacy: SignupForm.cs:447-653 (btnPrint_Click) + :1698-1722 (CombinePDFs PdfSharp)
/// 刻意做成 static：<see cref="BatchReportHandler"/> 的建構子不必多吃相依（既有測試不動），
/// 而背景 job service 可直接注入 Singleton 的 renderer / merger 自行呼叫。
/// </remarks>
public static class BatchReportComposer
{
    /// <param name="onRendered">
    /// 每渲染完一筆就以「已完成筆數」回呼（1-based）。刻意用 <see cref="Action{T}"/> 而非
    /// <see cref="IProgress{T}"/>：後者會 post 到 SynchronizationContext / thread pool，回報可能延遲或亂序；
    /// 這裡只是同步寫一個 int，直接呼叫最準。
    /// </param>
    public static byte[] Render(
        IReportRenderer renderer,
        IPdfMerger merger,
        BatchReportPlan plan,
        Action<int>? onRendered,
        CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var pdfs = new List<byte[]>(plan.Signups.Count);

        for (var i = 0; i < plan.Signups.Count; i++)
        {
            // 放在 render 之前：取消時最多只多跑「當下這一筆」。
            ct.ThrowIfCancellationRequested();

            var s = plan.Signups[i];
            pdfs.Add(plan.ReportType switch
            {
                "datacard" => renderer.RenderDataCard(ReportModelBuilders.DataCard(s)),
                "receipt" => renderer.RenderReceipt(ReportModelBuilders.Receipt(s, now)),
                "tablet" => renderer.RenderTablet(ReportModelBuilders.Tablet(s)),
                "text" => renderer.RenderText(ReportModelBuilders.Text(s)),
                "worship" => renderer.RenderWorship(ReportModelBuilders.Worship(s)),
                "worshipcard" => renderer.RenderWorshipCard(ReportModelBuilders.WorshipCard(s)),
                _ => throw new InvalidOperationException(),
            });

            onRendered?.Invoke(i + 1);
        }

        ct.ThrowIfCancellationRequested();
        return merger.Merge(pdfs);
    }
}
