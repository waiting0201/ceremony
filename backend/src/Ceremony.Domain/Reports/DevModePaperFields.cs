namespace Ceremony.Domain.Reports;

/// <summary>
/// DEVMODE 三個紙張欄位（<c>dmPaperSize</c> / <c>dmPaperWidth</c> / <c>dmPaperLength</c>）
/// 與 <c>dmFields</c> 旗標之間的一致性規則。
/// </summary>
/// <remarks>
/// **為什麼這幾行值得獨立成一個型別**：它們是 2026-08-05 客訴「選了印表機卻跳
/// 『您的印表機已發生未預期的設定問題 0x80010105』」的根因所在。真正寫入 DEVMODE 的
/// <c>Ceremony.PrintForm</c> 是 Windows-only exe，macOS 開發機連測都跑不了；
/// 而這裡是純位元運算，跟 <see cref="PrinterFormMatcher"/> 同樣平台中立、測得到。
///
/// **不變式：旗標與值必須同進退。** <c>dmFields</c> 的某個旗標沒設，代表對應欄位「未使用」，
/// 唯一合法的未使用值是 0。留下「旗標清掉、值還在」的組合會讓 DEVMODE 處於自相矛盾的狀態——
/// v4 驅動在做 DEVMODE → PrintTicket 轉換（列印對話框開啟、進「印表機內容」時都會做）
/// 可能因此丟例外，Windows 就顯示那句 <c>RPC_E_SERVERFAULT (0x80010105)</c>。
///
/// 決策見 docs/blueprints/print-channel-electron.md 決策 9。
/// </remarks>
public static class DevModePaperFields
{
    /// <summary>dmPaperSize 有效。</summary>
    public const uint PaperSize = 0x00000002;

    /// <summary>dmPaperLength 有效（0.1mm 為單位的自訂長度）。</summary>
    public const uint PaperLength = 0x00000004;

    /// <summary>dmPaperWidth 有效（0.1mm 為單位的自訂寬度）。</summary>
    public const uint PaperWidth = 0x00000008;

    /// <summary>本型別負責的三個位元；其餘位元一律不碰。</summary>
    public const uint Mask = PaperSize | PaperLength | PaperWidth;

    /// <summary>
    /// 選定驅動表單（<c>dmPaperSize = kind</c>）之後該有的 <c>dmFields</c>。
    /// </summary>
    /// <remarks>
    /// 殘留的自訂寬高會與 <c>dmPaperSize</c> 打架（實務上寬高常會贏），那等同退回
    /// 「驅動不認得的 Custom 尺寸」——正是 v2.3.7 讓三種報表同時失準的形狀。
    /// **清掉旗標的同時，呼叫端必須把 <c>dmPaperWidth</c>/<c>dmPaperLength</c> 一併寫 0**
    /// （見 <see cref="DevModePaperFields"/> 的不變式），否則就是 0x80010105 那個 bug。
    /// </remarks>
    public static uint ForFormSelection(uint currentFields) =>
        (currentFields | PaperSize) & ~(PaperLength | PaperWidth);

    /// <summary>
    /// 還原時的 <c>dmFields</c>：只把三個紙張位元換回快照當時的值，**其餘位元保留現況**。
    /// </summary>
    /// <remarks>
    /// 不能整包 <c>dmFields</c> 蓋回去。apply 到 restore 之間，使用者完全可能在原生列印對話框按
    /// 「內容」改過方向／雙面／紙匣——那個 UI 寫的就是同一份每使用者預設 DEVMODE。
    /// 整包覆寫會把那些旗標清掉、對應的值卻留在結構裡，於是**還原這個動作本身**又生出一份
    /// 自相矛盾的 DEVMODE。這是同一個 bug 的第二個入口。
    /// </remarks>
    public static uint ForRestore(uint currentFields, uint snapshotFields) =>
        (currentFields & ~Mask) | (snapshotFields & Mask);

    /// <summary>
    /// 現況是否已經是我們要的表單，因而完全不必寫入。
    /// </summary>
    /// <remarks>
    /// 三個條件缺一不可：表單 ID 相同、<c>DM_PAPERSIZE</c> **有設**（沒設的話 <c>dmPaperSize</c>
    /// 只是一個被驅動忽略的殘值，「相同」不代表生效）、且沒有殘留的自訂寬高旗標。
    /// 回 <c>true</c> 時呼叫端會跳過 <c>SetPrinter</c>，也就不寫還原 journal——所以判斷太寬鬆
    /// 會讓真正該修的機器修不到，判斷太嚴則只是多寫一次驅動設定。
    /// </remarks>
    public static bool AlreadySelected(short currentKind, uint currentFields, short wantedKind) =>
        currentKind == wantedKind &&
        (currentFields & PaperSize) != 0 &&
        (currentFields & (PaperWidth | PaperLength)) == 0;
}
