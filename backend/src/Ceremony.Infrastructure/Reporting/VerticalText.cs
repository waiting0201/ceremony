using System.Text;

namespace Ceremony.Infrastructure.Reporting;

/// <summary>
/// 直書（一字一列）共用排版工具。薦牌 / 文牒 / 普桌 的姓名欄皆為窄欄直書，
/// 共用同一套規則，避免各 renderer 各自重複（並各自踩同樣的雷）。
/// </summary>
/// <remarks>
/// 兩個雷（見 docs/gotchas.md）：
/// 1. 全形標楷體字寬 ≈ 欄寬，靠「窄欄自動換行」會被 QuestPDF **靜默丟字** → 改用 <see cref="Stack"/> 顯式每字一行、且不要 .Width()。
/// 2. 次要姓名格 RDLC `CanGrow=true`，名目格高很矮；可用高度要取「到下一格的列距」而非名目格高，
///    否則 <see cref="FitFontPt"/> 會過度縮小 → 用列距才不過縮又不重疊。
/// </remarks>
internal static class VerticalText
{
    private const double PointsPerCm = 28.3464567;

    /// <summary>每字一行（以 \n 分隔），讓 QuestPDF 逐字垂直堆疊，而非靠窄欄自動換行（會丟字）。</summary>
    public static string Stack(string text)
    {
        var sb = new StringBuilder(text.Length * 2);
        foreach (var ch in text)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>
    /// 一組（同類，如「所有往生」或「所有陽上」）直書姓名的**統一字級**。
    /// 起點為舊系統字級（薦牌 ParaFontSize / 文牒固定 0.8cm）；只有當某格的名字在該字級下
    /// 會超出其可用高（n字×字級 &gt; 可用高 → 溢出重疊）時，才把**整組**一起縮到「最擠那格剛好塞下」。
    /// → 全組同大小（不會有大有小），且最長的也塞得下、不重疊；不需要時完全等於舊字級。
    /// 縮字級而非壓行高 → 字與字不互相重疊。
    /// </summary>
    /// <param name="cells">該組每個欄位的 (名字, 可用高cm)。可用高用 <see cref="Avail"/> 取得。</param>
    public static double GroupFontPt(double baseFontPt, params (string? name, double availCm)[] cells)
    {
        var fit = baseFontPt;
        foreach (var (name, avail) in cells)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            // 列數必須與 Stack 的渲染列數一致：Stack 逐字一列、**不 trim**（含開頭/結尾/中間空格）。
            // 用 name.Length（非 Trim().Length）—— 否則開頭/結尾全形空格（U+3000，常用來把名字往下推作排版）
            // 會被這裡 trim 掉而少算一列 → 字級沒縮 → Stack 多渲染的那列溢出、蓋到下一格（疊字）。
            var n = name!.Length;
            if (n <= 0) continue;
            var maxPt = avail / n * PointsPerCm;
            if (maxPt < fit) fit = maxPt;
        }
        return fit;
    }

    /// <summary>
    /// 次要格的可用高度：**只有當正下方那格有名字時**才受「列距」限制（避免壓到下一格）；
    /// 下方為空 → 可用整欄高、不必縮（對齊舊系統「沒下一個就不縮」）。
    /// </summary>
    public static double Avail(string? below, double rowPitchCm, double fullHeightCm)
        => string.IsNullOrWhiteSpace(below) ? fullHeightCm : rowPitchCm;
}
