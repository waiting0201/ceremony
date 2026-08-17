namespace Ceremony.Domain.Reports;

/// <summary>
/// 把一頁 PDF 放到印表機頁面上的**目標矩形**——決定 11 的送印路徑唯一的幾何決策點。
/// </summary>
/// <remarks>
/// <para>
/// <b>為什麼這段必須住在 Domain</b>：<c>Ceremony.PrintForm</c> 是 net10.0-windows 的 exe，
/// macOS 開發機與 CI 都跑不到它。決策 9a 的元教訓寫得很清楚——那次拆了「比對」卻沒拆
/// 「位元運算」，於是 <c>0x80010105</c> 的根因在 macOS 與 CI 上完全隱形。
/// **任何不需要 Win32 handle 的邏輯都該住在這裡。**
/// </para>
/// <para>
/// <b>⚠️ 這是本專案最貴的一段程式碼</b>：docs/gotchas.md 的首要元教訓是
/// 「**改列印通道會靜默作廢整份已驗收的座標表**」（v2.3.7 前例：改了 margins／scaleFactor／
/// pageSize，三種報表同時失準，而風險當時已被寫下卻沒做對照組就上線）。
/// 現行 ±0.05cm 的實體套印座標，全部是在 **Chromium 的 fit-to-printable-area 等比縮放置中**
/// 之下驗收的；<see cref="ScaleMode.Fit"/> 的存在就是為了讓那份座標表**在構造上不作廢**。
/// </para>
/// <para>
/// 但「構造上不作廢」不等於「已驗證不作廢」——上線前必須做六報表對照組
/// （同機、同紙、同資料，兩條路徑各印一張疊放透光量測），見
/// docs/blueprints/printing-reports-positions.md 的對位驗收 Checklist。
/// </para>
/// <para>
/// <b>座標系</b>：GDI 印表機 DC 的原點 <c>(0,0)</c> 是**可列印區**的左上角，不是紙的左上角。
/// 紙的左上角在 <c>(-PHYSICALOFFSETX, -PHYSICALOFFSETY)</c>。這一點是 <see cref="ScaleMode.Fit"/>
/// 與 <see cref="ScaleMode.StretchPhysical"/> 差異的全部來源。
/// </para>
/// </remarks>
public static class PrintScalePolicy
{
    /// <summary>PDF 的長度單位是 point（1/72 吋）。</summary>
    private const double PointsPerInch = 72.0;

    public enum ScaleMode
    {
        /// <summary>
        /// **等比**縮放到剛好塞進可列印區，並置中——複製 Chromium PDF 檢視器的行為。
        /// </summary>
        /// <remarks>
        /// 這是預設，也是唯一會用在正式送印上的模式。刻意**不**把倍率夾在 1.0 以下：
        /// fit 的定義就是「填滿到其中一邊頂到」，紙比內容大的時候該放大就放大，
        /// 否則就不是在複製現行行為，而是發明第三種語意。
        /// </remarks>
        Fit,

        /// <summary>
        /// **非等比**拉滿整張實體紙——舊系統 <c>DrawImage(img, PageBounds)</c> 的語意。
        /// </summary>
        /// <remarks>
        /// ⚠️ **這個模式只給排障用，不得接到任何使用者可見的設定。**
        /// 它存在的唯一理由是：萬一六報表對照組量出偏差，可以用二分法快速定位
        /// 「是縮放模式的問題，還是別的地方」。
        /// 把它接上 UI 就是 v2.3.8 那三個送印軸（margins／scaleFactor／pageSize）的復活，
        /// 而那次的代價是整份座標表作廢。
        /// </remarks>
        StretchPhysical,
    }

    /// <summary>
    /// 一台印表機的頁面度量，全部來自 <c>GetDeviceCaps</c>，單位是**裝置像素**。
    /// </summary>
    /// <param name="PrintableWidthPx">HORZRES</param>
    /// <param name="PrintableHeightPx">VERTRES</param>
    /// <param name="PhysicalWidthPx">PHYSICALWIDTH</param>
    /// <param name="PhysicalHeightPx">PHYSICALHEIGHT</param>
    /// <param name="OffsetXPx">PHYSICALOFFSETX（左側不可列印邊界）</param>
    /// <param name="OffsetYPx">PHYSICALOFFSETY（上緣不可列印邊界）</param>
    /// <param name="DpiX">LOGPIXELSX</param>
    /// <param name="DpiY">LOGPIXELSY —— 點陣機常見 X≠Y，不可假設是方的</param>
    public readonly record struct DeviceMetrics(
        int PrintableWidthPx,
        int PrintableHeightPx,
        int PhysicalWidthPx,
        int PhysicalHeightPx,
        int OffsetXPx,
        int OffsetYPx,
        int DpiX,
        int DpiY);

    /// <summary>要把該頁畫進去的矩形，座標系為印表機 DC（原點＝可列印區左上角）。</summary>
    public readonly record struct DestRect(int X, int Y, int Width, int Height)
    {
        public bool IsEmpty => Width <= 0 || Height <= 0;
    }

    /// <summary>
    /// 算出目標矩形。輸入不合理（尺寸或 DPI ≤ 0）時回傳空矩形，呼叫端必須當成「不要畫」。
    /// </summary>
    /// <remarks>
    /// 回空而不是丟例外：這條路上唯一比「印歪」更糟的是「整個列印流程炸掉」，
    /// 而 <see cref="DestRect.IsEmpty"/> 讓呼叫端有機會記一行診斷再跳過該頁。
    /// </remarks>
    public static DestRect Compute(double pageWidthPt, double pageHeightPt, DeviceMetrics d, ScaleMode mode)
    {
        if (pageWidthPt <= 0 || pageHeightPt <= 0) return default;
        if (d.DpiX <= 0 || d.DpiY <= 0) return default;

        if (mode == ScaleMode.StretchPhysical)
        {
            if (d.PhysicalWidthPx <= 0 || d.PhysicalHeightPx <= 0) return default;

            // 紙的左上角在 DC 座標的負象限——這正是舊系統 PageBounds 的位置。
            return new DestRect(-d.OffsetXPx, -d.OffsetYPx, d.PhysicalWidthPx, d.PhysicalHeightPx);
        }

        if (d.PrintableWidthPx <= 0 || d.PrintableHeightPx <= 0) return default;

        // 先把 PDF 的 point 換成這台裝置的像素（X/Y 各用各的 DPI，別假設是方的）。
        double pageWidthPx = pageWidthPt / PointsPerInch * d.DpiX;
        double pageHeightPx = pageHeightPt / PointsPerInch * d.DpiY;

        double scale = Math.Min(d.PrintableWidthPx / pageWidthPx, d.PrintableHeightPx / pageHeightPx);

        int w = (int)Math.Round(pageWidthPx * scale);
        int h = (int)Math.Round(pageHeightPx * scale);
        if (w <= 0 || h <= 0) return default;

        // 四捨五入之後可能比可列印區大 1px，夾一下免得最外圈被驅動裁掉。
        w = Math.Min(w, d.PrintableWidthPx);
        h = Math.Min(h, d.PrintableHeightPx);

        return new DestRect((d.PrintableWidthPx - w) / 2, (d.PrintableHeightPx - h) / 2, w, h);
    }

    /// <summary>把 cm 換成 point，給 <see cref="ReportPageSizes"/> 的值直接餵進 <see cref="Compute"/>。</summary>
    public static double CmToPoints(double cm) => cm / 2.54 * PointsPerInch;
}
