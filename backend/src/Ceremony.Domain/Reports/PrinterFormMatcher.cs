namespace Ceremony.Domain.Reports;

/// <summary>
/// 把「報表類型」對應到「印表機驅動裡的自訂表單」——復刻舊系統唯一會主動設定的那一格。
/// </summary>
/// <remarks>
/// 舊系統 reference/old/Ceremony/SignupForm.cs:1770-1787 在跳出 Windows 原生列印對話框之前，
/// 會拿報表的中文名去驅動回報的 PaperSizes 撈同名表單：
///
///     foreach (PaperSize ps in printDialog.PrinterSettings.PaperSizes)
///         if (ps.PaperName == paperSize.PaperName) { pss = ps; break; }   // 註解原文：取得印表機尺寸設定
///
/// 撈到的物件帶著驅動的 RawKind（＝ DEVMODE 的 dmPaperSize 表單 ID），驅動因此會自動套用該表單
/// 綁定的尺寸與紙匣。這就是客訴「舊系統送出列印會自動找到印表機的設定」的全部機制。
///
/// 本類別只做「比對」這件純邏輯（平台中立，macOS 開發機測得到）；真正把結果寫進
/// 每使用者預設 DEVMODE 的 Win32 呼叫在 Ceremony.PrintForm（Windows-only）。
///
/// 決策見 docs/blueprints/print-channel-electron.md 決策 9。
/// </remarks>
public static class PrinterFormMatcher
{
    /// <summary>
    /// 尺寸相符的容差（單邊 mm，寬高各自判定）。
    /// </summary>
    /// <remarks>
    /// 下界：驅動表單尺寸經 System.Drawing 的 PaperSize 回報時單位是 1/100 吋，量化步階 0.254mm；
    /// 0.5mm ≈ 2 個步階，正確建立的表單不會被誤判成不符。
    /// 上界：必須小於 1.0mm，才抓得到舊系統留下的薦牌表單（254.0mm vs 現在的 255mm，差 −1.0mm）。
    /// 這個值恰好把 5 張舊表單切成 docs/design/infrastructure.md runbook 要求的兩組
    /// （收據／普桌本來就對；資料卡／薦牌／文牒必須重建）——PrinterFormMatcherTests 用真實舊值鎖住。
    /// </remarks>
    public const double ToleranceMm = 0.5;

    /// <summary>驅動回報的一張表單。WidthMm/HeightMm 由呼叫端從 1/100 吋換算而來。</summary>
    public readonly record struct DriverForm(string Name, short Kind, double WidthMm, double HeightMm);

    public enum FormMatch
    {
        /// <summary>找到同名表單，尺寸也在容差內。</summary>
        Exact,

        /// <summary>找到同名表單，但尺寸不符（多半是舊系統留下的錯誤 form）。</summary>
        SizeMismatch,

        /// <summary>驅動沒有同名表單。</summary>
        NotFound,
    }

    /// <param name="Match">比對結果。</param>
    /// <param name="FormName">我們要找的表單名（不論找到與否都會填，診斷紀錄要用）。</param>
    /// <param name="Form">命中的驅動表單；NotFound 時為 null。</param>
    /// <param name="WidthDiffMm">實際寬 − 期望寬；SizeMismatch 時才有意義。</param>
    /// <param name="HeightDiffMm">實際高 − 期望高；SizeMismatch 時才有意義。</param>
    public readonly record struct MatchResult(
        FormMatch Match,
        string FormName,
        DriverForm? Form,
        double WidthDiffMm,
        double HeightDiffMm);

    /// <summary>
    /// 依報表類型在驅動表單清單裡找對應的表單。
    /// </summary>
    /// <remarks>
    /// 幾個刻意的行為：
    /// - 同名多張只取第一張（逐行對齊舊系統的 <c>break</c>）。
    /// - 找不到就是 NotFound，**絕不**退回尺寸相近的別張表單：datacard 與 worshipcard 尺寸完全相同、
    ///   名稱卻不同，靜默代換會變成「印在別種報表的紙上而沒有任何訊號」。
    /// - 尺寸不符**仍然回傳該表單**（呼叫端會選它）：選錯尺寸的同名表單頂多等比縮幾 %，
    ///   停在 A4 則是整份位移數公分完全不能用。代價是可見度（標題警告 + 診斷紀錄），不是拒絕動作。
    /// </remarks>
    /// <exception cref="ArgumentException">reportType 不在 ReportPageSizes 表內。</exception>
    public static MatchResult Match(string? reportType, IReadOnlyList<DriverForm> forms)
    {
        if (!ReportPageSizes.TryGet(reportType, out var size))
            throw new ArgumentException($"未知的報表類型：{reportType}", nameof(reportType));

        ArgumentNullException.ThrowIfNull(forms);

        foreach (var form in forms)
        {
            // 驅動表單名以序數比對：這是要送進 DEVMODE 的識別字串，不做文化相關的正規化。
            if (!string.Equals(form.Name, size.FormName, StringComparison.Ordinal)) continue;

            var dw = form.WidthMm - size.WidthMm;
            var dh = form.HeightMm - size.HeightMm;
            var ok = Math.Abs(dw) <= ToleranceMm && Math.Abs(dh) <= ToleranceMm;
            return new MatchResult(ok ? FormMatch.Exact : FormMatch.SizeMismatch, size.FormName, form, dw, dh);
        }

        return new MatchResult(FormMatch.NotFound, size.FormName, null, 0, 0);
    }
}
