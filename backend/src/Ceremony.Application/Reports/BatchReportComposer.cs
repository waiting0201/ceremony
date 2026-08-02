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
/// 逐筆渲染 <see cref="BatchReportPlan"/> 的 signups 並合併成單一 PDF 檔。
/// </summary>
/// <remarks>
/// Legacy: SignupForm.cs:447-653 (btnPrint_Click) + :1698-1722 (CombinePDFs PdfSharp)
/// 刻意做成 static：背景 job service 可直接注入 Singleton 的 renderer / merger 自行呼叫。
///
/// 2026-08-02 起單筆結果**逐筆落檔**而不是累積成 <c>List&lt;byte[]&gt;</c>：大量列印取消分段後
/// 又變回一次印完整批，累積在記憶體的是 O(N)（15000 筆 ≈ 2 GB）。
/// 見 docs/blueprints/print-channel-electron.md。
/// </remarks>
public static class BatchReportComposer
{
    /// <param name="workDir">
    /// 本次批次的暫存目錄，用來放逐筆的中間 PDF。函式負責建立，**離開時一律整個刪除**
    /// （成功、失敗、取消三條路都是）。
    /// </param>
    /// <param name="outputPath">
    /// 成品輸出路徑。刻意放在 <paramref name="workDir"/> **之外**：成品要活到使用者取檔為止，
    /// 而中間檔在合併完就沒有用了，兩者生命週期不同。
    /// </param>
    /// <param name="onRendered">
    /// 每渲染完一筆就以「已完成筆數」回呼（1-based）。刻意用 <see cref="Action{T}"/> 而非
    /// <see cref="IProgress{T}"/>：後者會 post 到 SynchronizationContext / thread pool，回報可能延遲或亂序；
    /// 這裡只是同步寫一個 int，直接呼叫最準。
    /// </param>
    /// <returns><paramref name="outputPath"/>，成品已寫入。</returns>
    public static string Render(
        IReportRenderer renderer,
        IPdfMerger merger,
        BatchReportPlan plan,
        string workDir,
        string outputPath,
        Action<int>? onRendered,
        CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var parts = new List<string>(plan.Signups.Count);

        Directory.CreateDirectory(workDir);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        try
        {
            for (var i = 0; i < plan.Signups.Count; i++)
            {
                // 放在 render 之前：取消時最多只多跑「當下這一筆」。
                ct.ThrowIfCancellationRequested();

                var s = plan.Signups[i];
                var bytes = plan.ReportType switch
                {
                    "datacard" => renderer.RenderDataCard(ReportModelBuilders.DataCard(s)),
                    "receipt" => renderer.RenderReceipt(ReportModelBuilders.Receipt(s, now)),
                    "tablet" => renderer.RenderTablet(ReportModelBuilders.Tablet(s)),
                    "text" => renderer.RenderText(ReportModelBuilders.Text(s)),
                    "worship" => renderer.RenderWorship(ReportModelBuilders.Worship(s)),
                    "worshipcard" => renderer.RenderWorshipCard(ReportModelBuilders.WorshipCard(s)),
                    _ => throw new InvalidOperationException(),
                };

                // 檔名補零：合併時靠檔案順序決定頁序，字串排序必須與筆序一致
                var part = Path.Combine(workDir, $"{i:D6}.pdf");
                File.WriteAllBytes(part, bytes);
                parts.Add(part);

                onRendered?.Invoke(i + 1);
            }

            ct.ThrowIfCancellationRequested();
            merger.Merge(parts, outputPath);
            return outputPath;
        }
        finally
        {
            TryDeleteDirectory(workDir);
        }
    }

    private static void TryDeleteDirectory(string dir)
    {
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch (IOException)
        {
            // 中間檔清不掉不該讓整批列印失敗；殘留由 BatchPrintJobService 的啟動掃描收拾
        }
        catch (UnauthorizedAccessException)
        {
            // 同上
        }
    }
}
