namespace Ceremony.Domain.Reports;

/// <summary>
/// 決定「這次列印該不該去動驅動的每使用者預設 DEVMODE」。
/// </summary>
/// <remarks>
/// <para>
/// 為什麼這個判斷值得獨立一個類別：舊系統（reference/old/Ceremony/SignupForm.cs:1770-1787）
/// 把比對到的 PaperSize 塞進**自己那份** PrintDocument 的 PageSettings——process-local、隨程式結束消失、
/// 值還是驅動自己給的現成物件。新系統送印走 Chromium PDF 檢視器 → PrintDlgEx，手上沒有 PrintDocument
/// 可綁，唯一的注入點是**每使用者預設 DEVMODE**（SetPrinter Level 9），那是全域且持久化的：
/// 寫壞會外溢到 Word/Excel，還原沒跑到就留在那裡。
/// </para>
/// <para>
/// 也就是說，同樣一個「自動選對紙」的功能，新系統的失敗代價比舊系統高一個數量級——
/// 2026-08-05 的客訴 <c>0x80010105</c> 就是這條路徑上的第一發。既然代價不對稱，門檻就必須不對稱：
/// <b>只有「確定會讓列印變好」的情況才動那份共用狀態，其餘一律不碰。</b>
/// </para>
/// <para>
/// 元教訓照 2026-08-05 辦理：Ceremony.PrintForm 是 net10.0-windows 的 exe，macOS 開發機與 CI 都跑不到，
/// 任何不需要 Win32 handle 的邏輯留在那裡等於沒有測試。這裡是純函式，
/// <c>PrinterFormPolicyTests</c> 在 macOS 上就鎖得住。
/// 決策見 docs/blueprints/print-channel-electron.md 決策 9b。
/// </para>
/// </remarks>
public static class PrinterFormPolicy
{
    /// <summary>要不要寫入每使用者預設 DEVMODE，以及不寫的話是為什麼。</summary>
    public enum FormApplyDecision
    {
        /// <summary>找到同名表單且尺寸相符——唯一會去動共用狀態的情況。</summary>
        Apply,

        /// <summary>預設印表機是虛擬印表機（Print to PDF／XPS／OneNote…）。</summary>
        SkipVirtualPrinter,

        /// <summary>驅動沒有同名表單。</summary>
        SkipNotFound,

        /// <summary>找到同名表單，但尺寸不符。</summary>
        SkipSizeMismatch,
    }

    /// <summary>
    /// 依比對結果與印表機性質決定是否寫入。
    /// </summary>
    /// <param name="match">
    /// <see cref="PrinterFormMatcher.Match(string?, IReadOnlyList{PrinterFormMatcher.DriverForm})"/> 的結果。
    /// </param>
    /// <param name="isVirtualPrinter">預設印表機是否為虛擬印表機。</param>
    /// <remarks>
    /// 三條 Skip 的理由各自不同，不要合併成一個布林：
    /// <list type="bullet">
    /// <item>
    /// <b>虛擬印表機</b>：Print to PDF 這類機器上的「紙張」跟實體套印無關，預選拿不到任何好處，
    /// 卻照樣把使用者的 PDF 輸出設定改掉。優先於比對結果判斷——就算它剛好有同名表單也不碰。
    /// </item>
    /// <item>
    /// <b>NotFound</b>：本來就沒東西可選（現況即如此，這裡只是把它寫成明確的一格）。
    /// </item>
    /// <item>
    /// <b>SizeMismatch</b>：<b>2026-08-06 推翻 2026-08-04 的決定</b>。原本的理由是「選錯尺寸的同名表單
    /// 頂多等比縮幾 %，停在 A4 則整份位移數公分完全不能用」——那個比較忽略了第三個選項：
    /// 不寫入 ≠ 停在 A4，而是停在使用者目前的預設紙，且我們會在檢視器標題請他手動選紙
    /// （他仍然選得到那張表單）。所以最壞情況是多按幾下，不是印壞。
    /// 用「多按幾下」去換掉「動全域 DEVMODE」這個高代價動作，在尺寸本來就已經錯了的前提下划算。
    /// 而且 SizeMismatch 幾乎都代表現場表單建錯（多半是舊系統留下的），正解是照 runbook 重建，
    /// 靜默替他套一張已知不對的紙只會讓那件事更晚被發現。
    /// </item>
    /// </list>
    /// </remarks>
    public static FormApplyDecision Decide(PrinterFormMatcher.FormMatch match, bool isVirtualPrinter)
    {
        if (isVirtualPrinter) return FormApplyDecision.SkipVirtualPrinter;

        return match switch
        {
            PrinterFormMatcher.FormMatch.Exact => FormApplyDecision.Apply,
            PrinterFormMatcher.FormMatch.SizeMismatch => FormApplyDecision.SkipSizeMismatch,
            PrinterFormMatcher.FormMatch.NotFound => FormApplyDecision.SkipNotFound,
            _ => FormApplyDecision.SkipNotFound,
        };
    }

    /// <summary>
    /// 決策對應到 helper 輸出的 <c>result</c> 字串。
    /// </summary>
    /// <remarks>
    /// 這些字串是 helper 與 Electron 端的契約，electron/print-form-core.ts 的 <c>HELPER_RESULTS</c>
    /// 必須逐一對得上（多一個少一個都會讓 parseHelperOutput 退成 helper-error）。
    /// <c>Apply</c> 沒有對應字串：實際寫入還可能被驅動拒絕，結果由呼叫端在寫入後決定。
    /// </remarks>
    public static string ToResult(FormApplyDecision decision) => decision switch
    {
        FormApplyDecision.SkipVirtualPrinter => "skipped-virtual",
        FormApplyDecision.SkipNotFound => "not-found",
        FormApplyDecision.SkipSizeMismatch => "mismatch",
        _ => throw new ArgumentOutOfRangeException(
            nameof(decision), decision, "Apply 的結果要看實際寫入成敗，不由本函式決定"),
    };
}
