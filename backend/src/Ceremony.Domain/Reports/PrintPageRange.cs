namespace Ceremony.Domain.Reports;

/// <summary>
/// 把列印對話框回傳的頁面範圍，換成實際要印的頁碼清單。
/// </summary>
/// <remarks>
/// <para>
/// <b>為什麼不能省略這個功能</b>：blueprint 決策 4 把「卡紙之後的續印」整個押在頁面範圍上——
/// 現場印 5000 筆的批次卡在第 600 頁時，唯一不用重跑整份渲染的方法就是填 <c>600-1200</c>。
/// 所以 <c>PrintDlgW</c> 刻意**不設** <c>PD_NOPAGENUMS</c>。
/// </para>
/// <para>
/// 住在 Domain 的理由同 <see cref="PrintScalePolicy"/>：不需要 Win32 handle 的邏輯留在
/// net10.0-windows 的 exe 裡等於沒有測試。
/// </para>
/// </remarks>
public static class PrintPageRange
{
    /// <summary>PD_PAGENUMS —— 使用者選了「頁面範圍」而不是「全部」。</summary>
    public const uint PageNums = 0x00000002;

    /// <summary>
    /// 解析頁面範圍。<b>任何說不通的輸入一律回「全部」</b>，絕不回空集合。
    /// </summary>
    /// <remarks>
    /// ⚠️ 這條「不回空」是刻意的，而且是本函式最重要的性質：
    /// 使用者按了「確定」就是要印東西，**靜默印出零頁**是最難查的失敗
    /// （沒有錯誤訊息、沒有紙、診斷紀錄還顯示 <c>printed</c>）。
    /// 寧可多印也不要無聲無息什麼都沒發生。
    /// </remarks>
    public static IReadOnlyList<int> Resolve(uint flags, int from, int to, int pageCount)
    {
        if (pageCount <= 0) return [];

        var all = Enumerable.Range(1, pageCount).ToArray();

        // 沒勾「頁面範圍」⇒ 全部。
        if ((flags & PageNums) == 0) return all;

        // 勾了卻沒給有效值 ⇒ 也是全部（見上面那條「不回空」）。
        if (from <= 0 && to <= 0) return all;

        // 只給一邊時，另一邊補成同一頁（＝只印那一頁），而不是補成邊界。
        if (from <= 0) from = to;
        if (to <= 0) to = from;

        // 反過來填（1200-600）是現場常見的手誤，接受並修正。
        if (from > to) (from, to) = (to, from);

        from = Math.Clamp(from, 1, pageCount);
        to = Math.Clamp(to, 1, pageCount);

        return Enumerable.Range(from, to - from + 1).ToArray();
    }
}
