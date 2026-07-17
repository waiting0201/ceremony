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

    /// <summary>
    /// 2026-07-17 使用者指定（reference/薦牌.jpg 手寫量測）：薦牌亡者/陽上矩陣改為「固定方框內動態排版」。
    /// 舊做法（固定列距 + WithBottomGap 補空格 + GroupFontPt 以列距當可用高）在上下排都有名字時，
    /// 上排名字被限制在 1.4~1.9cm 的列距內，3 字名+間隔空格會把整組字級縮到 0.36~0.47cm（字太小、
    /// 欄距相對變寬——正是使用者客訴）。改為：字級以 baseFont 起算，只有「整欄鏈（上排字數+1 格間距
    /// +下排字數）」或「單獨一欄」塞不下方框高度才整組等比縮；下排起點不再是固定座標，而是
    /// 「上排（有下排配對者）最長字數+1 個字高間距」之後動態決定 → 名字之間永遠恰好一個字高間距，
    /// 字級能保住舊系統的 0.6cm。
    /// </summary>
    /// <param name="baseFontCm">起始字級（cm），只縮不放大。</param>
    /// <param name="boxHeightCm">方框可用高（cm）。</param>
    /// <param name="columns">每欄 (上排名字, 下排名字)；下排為空表示該欄只有一列。</param>
    /// <returns>統一字級（cm）與「下排相對方框頂的位移」（cm；無任何下排名字時為 0）。</returns>
    public static (double FontCm, double BottomRowOffsetCm) MatrixLayout(
        double baseFontCm, double boxHeightCm, params (string? top, string? bottom)[] columns)
    {
        var maxTopAny = 0;        // 所有上排名字的最長列數（單欄也不能超框）
        var maxTopWithBottom = 0; // 有下排配對的上排最長列數（決定統一的下排起點）
        var maxBottom = 0;
        foreach (var (top, bottom) in columns)
        {
            // 與 Stack/GroupFontPt 一致：不 trim，空格也算一列
            var t = string.IsNullOrWhiteSpace(top) ? 0 : top!.Length;
            var b = string.IsNullOrWhiteSpace(bottom) ? 0 : bottom!.Length;
            if (t > maxTopAny) maxTopAny = t;
            if (b > 0)
            {
                if (t > maxTopWithBottom) maxTopWithBottom = t;
                if (b > maxBottom) maxBottom = b;
            }
        }
        // 最擠的「垂直單位數」：單欄 = 字數；上下排欄 = 上排最長 + 1（間距）+ 下排最長
        var units = Math.Max(maxTopAny, maxBottom > 0 ? maxTopWithBottom + 1 + maxBottom : 0);
        var fontCm = units == 0 ? baseFontCm : Math.Min(baseFontCm, boxHeightCm / units);
        var bottomOffsetCm = maxBottom > 0 ? (maxTopWithBottom + 1) * fontCm : 0;
        return (fontCm, bottomOffsetCm);
    }

    /// <summary>
    /// 2026-07-06 使用者指定：同一欄「上排」與「下排」姓名（不同人）之間要留一個全形空白間距，
    /// 不能緊貼。正下方有名字時在姓名尾端補一個全形空格（U+3000）——<see cref="Stack"/> 會把它
    /// 渲染成多一列空白，<see cref="GroupFontPt"/> 因此多算一列而統一縮字，天然在「上排文字尾端」
    /// 與「下排文字起點（下一個列距邊界）」之間空出一個字高的間距，不需另外調整任何 Top/Left 座標
    /// （不違反 printing-reports-positions.md 的零容忍偏差條款）。下方為空（沒有下排名字）則不補。
    /// </summary>
    public static string? WithBottomGap(string? name, string? below)
        => string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(below) ? name : name + "　";
}
