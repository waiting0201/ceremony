namespace Ceremony.Domain.Reports;

/// <summary>
/// 寫入每使用者預設 DEVMODE **之前**的最後一道閘門：先自己做一次 Windows 列印 UI 會做的
/// 「DEVMODE → PrintTicket」轉換，轉不過就不要寫。
/// </summary>
/// <remarks>
/// <para>
/// <b>為什麼需要這道閘門</b>：<see cref="DevModePaperFields"/> 只能保證「我們沒違反**已知的**那條
/// 旗標／值不變式」，但驅動吃不吃我們手工組的 DEVMODE，終究是**猜的**。2026-08-08 客訴
/// （KYOCERA PA2000 在 v2.4.2 修好旗標一致性之後仍噴 <c>0x80010105</c>）指出這個猜測會落空：
/// v4 驅動的使用者預設**本體是 PrintTicket**，DEVMODE 只是一層要即時轉換的相容介面，
/// 而轉換由驅動廠商實作，品質不在我們手上。
/// </para>
/// <para>
/// 而舊系統從來不必面對這件事——它把比對到的 PaperSize 塞進自己那份 PrintDocument，
/// <c>PrintDialog</c> 帶著**自己那份 hDevMode**（非 null）進 PrintDlgEx，初值根本不取每使用者預設
/// （reference/old/Ceremony/SignupForm.cs:1764-1798）。「舊系統沒這問題」的完整解釋見 docs/gotchas.md。
/// </para>
/// <para>
/// 所以改成當場驗證：<c>PTConvertDevModeToPrintTicket</c> 正是列印 UI 失敗時會吐
/// <c>RPC_E_SERVERFAULT</c> 的那個轉換。我們自己先跑一次，**它過我們才寫**——把「猜驅動會不會吐」
/// 換成「問驅動會不會吐」。
/// </para>
/// <para>
/// 決策見 docs/blueprints/print-channel-electron.md 決策 9c。純函式住在 Domain 的理由同
/// <see cref="PrinterFormPolicy"/>：Ceremony.PrintForm 是 net10.0-windows 的 exe，macOS 與 CI 跑不到。
/// </para>
/// </remarks>
public static class PrintTicketPreflight
{
    /// <summary>預檢結果。</summary>
    public enum PreflightOutcome
    {
        /// <summary>轉換成功——唯一准許寫入的情況。</summary>
        Pass,

        /// <summary>轉換失敗：驅動明確吐回錯誤。寫下去就是使用者會看到的 <c>0x80010105</c>。</summary>
        Rejected,

        /// <summary>檢查本身跑不起來（prntvpt.dll 缺席、provider 開不了、例外）。</summary>
        Unavailable,
    }

    /// <summary>
    /// 由兩段 HRESULT 判定結果。<c>null</c> ＝ 那一段根本沒跑到。
    /// </summary>
    /// <param name="providerHResult"><c>PTOpenProvider</c> 的 HRESULT。</param>
    /// <param name="convertHResult"><c>PTConvertDevModeToPrintTicket</c> 的 HRESULT。</param>
    /// <remarks>
    /// 分成兩格不是為了讓呼叫端分流（兩者都不寫入），而是為了**診斷紀錄分得出來**：
    /// 「這台驅動不接受」與「這台機器連檢查都做不了」要修的東西完全不同，混成一格就查不下去。
    /// </remarks>
    public static PreflightOutcome Classify(int? providerHResult, int? convertHResult)
    {
        if (providerHResult is not { } open || open < 0) return PreflightOutcome.Unavailable;
        if (convertHResult is not { } convert) return PreflightOutcome.Unavailable;
        return convert < 0 ? PreflightOutcome.Rejected : PreflightOutcome.Pass;
    }

    /// <summary>
    /// 只有 <see cref="PreflightOutcome.Pass"/> 准許寫入——**檢查跑不起來也不寫**。
    /// </summary>
    /// <remarks>
    /// 這是刻意的 fail-closed，理由同決策 9b 的「只有確定會變好才動那份共用狀態」：
    /// 檢查失敗代表我們**不知道**這次寫入會不會變好，而不知道時的預設值必須是不動。
    /// 代價是那台機器退回「每次手動選紙」（多按幾下），換掉的是「留一份壞掉的共用設定在使用者機器上」。
    /// 若現場出現大量 <see cref="PreflightOutcome.Unavailable"/>，那是要查的訊號，不是要放寬的理由。
    /// </remarks>
    public static bool MayWrite(PreflightOutcome outcome) => outcome == PreflightOutcome.Pass;

    /// <summary>
    /// 結果對應到 helper 輸出的 <c>result</c> 字串。
    /// </summary>
    /// <remarks>
    /// 與 <see cref="PrinterFormPolicy.ToResult"/> 同屬跨語言契約，
    /// electron/print-form-core.ts 的 <c>HELPER_RESULTS</c> 必須逐一對得上。
    /// </remarks>
    public static string ToResult(PreflightOutcome outcome) => outcome switch
    {
        PreflightOutcome.Rejected => "skipped-printticket-reject",
        PreflightOutcome.Unavailable => "skipped-printticket-unavailable",
        _ => throw new ArgumentOutOfRangeException(
            nameof(outcome), outcome, "Pass 不是一個「沒寫入」的結果，字串要由寫入成敗決定"),
    };
}
