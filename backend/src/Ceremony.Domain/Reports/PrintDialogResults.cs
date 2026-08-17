namespace Ceremony.Domain.Reports;

/// <summary>
/// <c>Ceremony.PrintForm.exe print</c> 的 <c>result</c> 字彙——跨語言契約的 C# 這一半。
/// </summary>
/// <remarks>
/// <para>
/// <b>為什麼另立一份而不是併進既有的 <c>HELPER_RESULTS</c></b>（<c>frontend/electron/print-form-core.ts</c>）：
/// 那份清單被 <c>blockScope()</c>（失敗印表機黑名單記帳）、<c>needsRestore()</c>（還原 journal）、
/// <c>viewerTitle()</c> 三個函式共用，語意全都綁在「<c>SetPrinter</c> 寫每使用者預設 DEVMODE」那條路。
/// 把 <c>printed</c> / <c>cancelled</c> 塞進去，等於讓黑名單邏輯開始對一組它從沒設計過的值做判斷。
/// TS 端對應的是新開的 <c>print-dialog-core.ts</c>，兩份平行存在。
/// （<c>error</c> 兩邊都有沒關係——parser 不同，不會互相汙染。）
/// </para>
/// <para>
/// 兩邊仍需人工同步（沒有 codegen），所以各有一支「集合恰好等於這些值」的測試——
/// 改一邊就會有測試紅。這是既有 <c>HELPER_RESULTS</c> ↔ C# 對應目前**沒有**的保障，順手補上。
/// </para>
/// </remarks>
public static class PrintDialogResults
{
    /// <summary>使用者按了確定，工作已經交給 spooler。</summary>
    public const string Printed = "printed";

    /// <summary>使用者按了取消或關掉對話框。**這不是錯誤**，呼叫端不得顯示紅字。</summary>
    public const string Cancelled = "cancelled";

    /// <summary>這台機器上沒有任何可用的印表機。</summary>
    public const string NoDefaultPrinter = "no-default-printer";

    /// <summary>
    /// <c>PrintDlgW</c> 自己失敗了（<c>CommDlgExtendedError</c> 非 0）——對話框根本沒開起來。
    /// </summary>
    /// <remarks>
    /// 這一格如果在現場出現，就是決策 11 的前提被推翻的訊號：
    /// 我們挑 comdlg32 舊版對話框的全部理由就是它不走 DEVMODE→PrintTicket 轉換。
    /// **看到它要當成要查的訊號，不是重試的理由。**
    /// </remarks>
    public const string DialogFailed = "dialog-failed";

    /// <summary>PDF 讀不開或某一頁算不出來（PDFium 那一段）。</summary>
    public const string RenderFailed = "render-failed";

    /// <summary>對話框過了，但 GDI 的 <c>StartDoc</c>／<c>StartPage</c>／<c>EndDoc</c> 中途失敗。</summary>
    public const string DriverRejected = "driver-rejected";

    /// <summary>其他未預期的例外。</summary>
    public const string Error = "error";

    /// <summary>全部合法值。TS 端 <c>PRINT_RESULTS</c> 必須與這個集合完全相同。</summary>
    public static readonly IReadOnlyList<string> All =
    [
        Printed, Cancelled, NoDefaultPrinter, DialogFailed, RenderFailed, DriverRejected, Error,
    ];
}
