namespace Ceremony.Domain.Reports;

/// <summary>
/// 決定「這次還要不要**接觸**這台印表機的驅動」——比 <see cref="PrinterFormPolicy"/> 更前面的一道閘門：
/// 那一道問的是「該不該寫入」，這一道問的是「該不該連問都不問」。
/// </summary>
/// <remarks>
/// <para>
/// <b>為什麼需要它</b>：2026-08-10 客訴，KYOCERA PA2000 在 v2.4.5（決策 9c 的預檢）之後症狀從
/// 「跳 <c>0x80010105</c>」變成「按下檢視器的列印鈕之後整個 app 卡死，選不了別台印表機、
/// 關不掉預覽，只能重啟」。預檢是 fail-closed 的，所以那台機器上我們**已經不寫入**了——
/// 換句話說，剩下能怪的只有「接觸」本身：
/// </para>
/// <list type="bullet">
/// <item>
/// <c>PTOpenProvider</c> / <c>PTConvertDevModeToPrintTicket</c> 會叫起 v4 驅動的設定模組
/// （多半跑在 <c>PrintIsolationHost.exe</c>）。而 Electron 端原本給 helper 的預算是 3 秒、
/// 逾時直接 <c>SIGKILL</c>——**在 COM 呼叫進行到一半把 client 殺掉**，對方留在什麼狀態不由我們決定；
/// 之後 <c>PrintDlgEx</c> 去問同一個 provider 就可能永遠等不到回應（＝現場看到的卡死）。
/// </item>
/// <item>
/// 而且我們**每次列印都問一次**。一台已經明講「我不接受」的驅動，再問一百次也不會變好，
/// 卻把上面那個機率乘以一百。
/// </item>
/// </list>
/// <para>
/// 所以規則改成：<b>只要在某台印表機上失敗過一次，就再也不碰它</b>（呼叫端把失敗記在
/// <c>print-form-printers.json</c>，下次連 <c>DeviceCapabilities</c> 都不呼叫）。代價是那台機器
/// 退回「每次手動選紙」——與預檢擋下時的代價完全相同，本來就已經是那個狀態了。
/// </para>
/// <para>
/// 判定住在 Domain 的理由同 <see cref="PrinterFormPolicy"/>：Ceremony.PrintForm 是
/// net10.0-windows 的 exe，macOS 開發機與 CI 跑不到，留在那裡等於沒有測試。
/// 決策見 docs/blueprints/print-channel-electron.md 決策 9d。
/// </para>
/// </remarks>
public static class PrinterContactPolicy
{
    /// <summary>呼叫端說這台印表機已在黑名單——沒有做任何驅動呼叫就結束。</summary>
    public const string BlockedResult = "skipped-printer-blocked";

    /// <summary>
    /// 呼叫端早就不等了（超過它給的預算），所以**不寫入**。
    /// </summary>
    /// <remarks>
    /// 這一格是「不再中途 kill helper」的配套：呼叫端逾時後改成放著讓它自己跑完
    /// （殺在 Win32／COM 呼叫中間才是危險的事），但那樣一來「沒有還原 journal 就不會有寫入」
    /// 這條不變式就得由 helper 自己守——它必須在 <c>SetPrinter</c> 之前確認呼叫端還在聽。
    /// </remarks>
    public const string OverBudgetResult = "skipped-over-budget";

    private static readonly string[] Empty = [];

    /// <summary>
    /// 解析 <c>--blocked</c> 參數（逗號分隔的印表機名稱雜湊）。
    /// </summary>
    /// <remarks>
    /// 一律轉小寫並去重：雜湊是 <c>PrinterFormApplier.HashPrinterName</c> 產的小寫十六進位，
    /// 但這份清單會被呼叫端寫進 JSON 再讀回來、也可能被現場的人手動編輯過。
    /// 比對條件放寬的代價是零（頂多多跳過一台），比對不到的代價是整個閘門失效。
    /// </remarks>
    public static IReadOnlyList<string> ParseBlocked(string? arg)
    {
        if (string.IsNullOrWhiteSpace(arg)) return Empty;

        return arg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>這台印表機是否在黑名單內。</summary>
    public static bool IsBlocked(string? printerHash, IReadOnlyCollection<string> blocked)
    {
        if (blocked.Count == 0 || string.IsNullOrWhiteSpace(printerHash)) return false;
        return blocked.Contains(printerHash.Trim().ToLowerInvariant(), StringComparer.Ordinal);
    }

    /// <summary>
    /// 呼叫端是否還在等（＝現在寫入還有意義）。<paramref name="budgetMs"/> 為 null ＝ 沒給預算，一律放行。
    /// </summary>
    /// <remarks>
    /// 刻意用 <c>&gt;=</c>：預算到點的那一刻呼叫端就已經 resolve 了，邊界上寧可不寫。
    /// </remarks>
    public static bool WithinBudget(long elapsedMs, int? budgetMs)
    {
        if (budgetMs is not { } budget || budget <= 0) return true;
        return elapsedMs < budget;
    }
}
