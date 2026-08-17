using System.Reflection;
using Ceremony.Domain.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ceremony.Infrastructure.Reporting;

/// <summary>
/// 文牒 PDF — 對齊 tmpText.rdlc / tmpTextTwo.rdlc（座標取自 RDLC XML，positions §12-13）。
/// </summary>
/// <remarks>
/// 頁面 36.5×26.2cm 橫向超寬。2 變體：tmpTextTwo（恰好 2 亡）/ tmpText（其他）。
/// DeadName 在 RDLC 內以 Rectangle2 群組，座標已換算成絕對值（Rect 原點 + 相對位移）。
/// PhotoAddress 為 27×653px 直書地址 PNG（SkiaSharp 移植自 Library.DrawText），嵌入 0.75×18.13cm 窄帶。
/// 字型固定 BiauKai；DeadName / LivingName 0.8cm、HallName 0.6cm VAlign=Middle、Number 1cm Bold。
/// 2026-07-18 客訴調整（刻意偏離 RDLC 原 0.66cm）：地址加大（0.66→0.75cm）＋下移（Top 4.9，印在
/// 預印「臺灣」正下方）。2026-07-21 客訴再調（覆蓋前一輪的字級）：往者/陽上字級統一 0.8cm、同欄上下
/// 姓名間空一個全形（WithBottomGap）；往者整體右移 0.5cm（DeadShiftX）；陽上整體下移 1cm、右移 1cm
/// （LivingShift）；地址右移 0.5cm（Left 25.4→25.9）並提高 PNG 解析度（SkiaImageHelpers Supersample）。
/// 2026-07-27 客訴（覆蓋 07-21 的 WithBottomGap 與 DeadShiftX）：
/// (1) 往者/陽上「不管幾位，字級都跟一位一樣」→ 改用 <see cref="VerticalText.MatrixLayout"/>（同薦牌
///     2026-07-17），字級固定 0.8cm、下排起點動態；
/// (2) 往者最左欄離左側預印字 0.5cm → 絕對錨點 <see cref="DeadLeftX"/>，堂號左字同錨點對齊。
/// 2026-08-17 客訴（只改 Two 變體的橫向，覆蓋 07-27 第 (2) 點對「恰 2 亡」的適用）：2 位往者整組以堂號
/// 中軸置中 → <see cref="DeadTwoColumnsX"/>；Base 變體本來就在同一中軸上、不動。
/// 同輪順帶補上堂號 ≥2 字格的重心下沉（薦牌 2026-08-08 已修、文牒漏掉）→ <see cref="HallTopOf"/>。
/// </remarks>
public sealed class TextRenderer
{
    private const string FontFamily = "BiauKai";
    private const double PointsPerCm = 28.3464567;
    internal const double PageWidthCm = 36.5;
    internal const double PageHeightCm = 26.2;

    // 亡/陽姓名起始字級（cm）。RDLC 原 0.8，2026-07-18 曾依客訴加大到 0.9；2026-07-21 客訴
    // 改回「往者/陽上統一 0.8cm」。上限受欄距 0.91251cm 制約（直書字寬≈字級，再大會蓋到隔壁欄），
    // 且維持 > 地址字級（0.75cm，0.8 > 0.75 ✓）。
    // 2026-07-27 客訴「不管幾位都跟一位一樣大」後，這個值就是實際字級——MatrixLayout 只在整欄鏈
    // （上排最長 +1 間距 + 下排最長）超出欄高時才縮，跟人數無關。
    internal const double NameBaseFontCm = 0.8;

    // 2026-07-21 客訴位移（僅文牒）：往者整體下移 0.5cm；堂號在往者正上方共用 DeadShiftY 一起下移；
    // 陽上整體下移 1cm、右移 1cm。只加在各自欄位的 Left/Top 上，相對矩陣結構不變。
    // （橫向的 DeadShiftX=0.5「整體右移」已於 2026-07-27 撤除，改為下方 DeadLeftX 絕對錨點。）
    private const double DeadShiftY = 0.5;
    private const double LivingShiftX = 1.0;
    private const double LivingShiftY = 1.0;

    // 2026-07-27 客訴：「往者要跟左邊文牒本來的文字距離 0.5 公分，堂號也是同樣對齊」。
    // 這是**間距**不是位移——07-21 誤解成「整體右移 0.5cm」，實際印出來間距變成 0.793cm。
    // 量測（reference/template/文牒.jpg，2866×2023px ↔ 36.5×26.2cm，姓名帶 y 3.6~14cm 內做
    // 逐欄墨跡掃描）：往者欄左側最近的預印字欄「鳴呼既追攀之無從…」x 10.558~11.207cm（字寬 0.662，
    // 與 RDLC 0.66cm 字級吻合）→ 右緣 11.207。最左往者欄左緣 = 11.207 + 0.5 = 11.707cm。
    // 兩個變體都以「最左欄」對齊此錨點（Base 最左欄 RDLC 11.50、Two 最左欄 RDLC 11.85），
    // 欄與欄的相對距離維持 RDLC 原值不動。
    internal const double PrePrintLeftTextRightX = 11.207;
    internal const double DeadPrePrintGapX = 0.5;
    internal const double DeadLeftX = PrePrintLeftTextRightX + DeadPrePrintGapX; // 11.707
    internal const double DeadColPitch = 0.91251;                    // RDLC 欄距（= 欄寬）
    internal const double DeadMidX = DeadLeftX + DeadColPitch;       // 12.620（RDLC 12.41251）
    internal const double DeadRightX = DeadLeftX + DeadColPitch * 2; // 13.532（RDLC 13.32502）

    // 堂號「同樣對齊」：左字（Second）左緣對齊 DeadLeftX（RDLC 亦為 11.50，與往者區塊原點同一條線），
    // 右字（First）維持 RDLC 相對距 13.53753 − 11.5 = 2.03753cm。
    internal const double HallFontCm = 0.6;
    internal const double HallSecondX = DeadLeftX;                   // 11.707
    internal const double HallFirstX = DeadLeftX + 2.03753;          // 13.744

    // 堂號墨跡中軸 13.0255。堂號是**橫排**欄位、左對齊於 0.7cm 欄寬內，SplitHallName 只會給每格
    // 1 字或 2 字（2 字在 0.7cm 欄內換行成上下兩列）→ 墨跡寬恆為 HallFontCm、中軸與字數無關。
    internal const double HallCenterX = (HallSecondX + HallFirstX + HallFontCm) / 2.0;

    // Two 變體兩高欄的相對距（RDLC 13.01299 − 11.85）。橫向位置見 DeadTwoColumnsX。
    internal const double DeadTwoColOffset = 1.16299;

    /// <summary>堂號單字格的 Top（RDLC 2.1 + <see cref="DeadShiftY"/>）。</summary>
    private const double HallNameTop = 2.1 + DeadShiftY;                  // 2.6

    /// <summary>堂號 ≥2 字格的 Top：比單字格高 0.2cm（見 <see cref="HallTopOf"/>）。</summary>
    private const double HallNameTopMultiChar = HallNameTop - 0.2;        // 2.4

    /// <summary>
    /// 依該格字數決定堂號 Top。逐格獨立判斷：`SplitHallName` 2 字→1+1、4 字→2+2、3 字或 5 字以上
    /// →整串進 First（左格空），所以 3 字堂號也會走 ≥2 字這條。
    /// </summary>
    /// <remarks>
    /// 2026-08-17：把薦牌 2026-08-08 那輪的補償搬到文牒（兩張報表的堂號格**完全同型**：0.7×1.3825cm、
    /// 0.6cm 字、橫排 + vMiddle）。根因同：0.6cm 字塞 0.7cm 欄寬，第 2 個字被 QuestPDF 換行往下長，
    /// 但 <see cref="DrawText"/> 的 vMiddle 只用**單字高**算位移（`top + (height − fontCm)/2`），
    /// 不知道實際渲染成兩列 → 整塊視覺重心下沉。這裡以 Top 補回，不動 vMiddle 公式（改公式會連帶
    /// 影響編號欄）。這同時是**對齊 RDLC 1:1**：RDLC 的 VerticalAlign=Middle 是把整個換行後的文字塊
    /// 置中，我們原本比 RDLC 低。
    /// ⚠️ 補償量取薦牌那個**客戶實體套印認可過的 0.2cm**，不是幾何全補的 0.3cm
    /// （(n−1)×0.6/2；格高 1.3825、字 0.6，2 列的幾何中心差正是 0.3）——兩張報表格子同型，
    /// 客戶在薦牌上眼睛看過的值直接沿用比自己算一個新數字安全，也讓兩報表行為一致。
    /// 同樣沿用薦牌的已知不足：3 字以上（整串進 First）也只補 0.2cm。
    /// 必須用 <see cref="VerticalText.ElementCount"/> 而非 <c>.Length</c>——增補平面造字（`𡍼` 等）
    /// 在 <c>.Length</c> 算兩格會誤判成多字（見 gotchas「一個字不等於一個 char」）。
    /// </remarks>
    internal static double HallTopOf(string? segment)
        => VerticalText.ElementCount(segment) >= 2 ? HallNameTopMultiChar : HallNameTop;

    // 開發用列印位置檢視工具的樣板照片（EmbeddedResource）；只在 debugOverlay:true 時載入使用，
    // 不進生產列印路徑。詳見 docs/blueprints/printing-reports.md「開發用列印位置檢視工具」。
    private static readonly byte[] TemplateImage = LoadTemplate("text-template.jpg");

    public byte[] Render(TextData data, bool debugOverlay = false)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size((float)PageWidthCm, (float)PageHeightCm, Unit.Centimetre);
                page.Margin(0);
                page.DefaultTextStyle(t => t.FontFamily(FontFamily).FontColor(Colors.Black));

                page.Content().Layers(layers =>
                {
                    layers.PrimaryLayer().Background("#FFFFFF");

                    if (debugOverlay)
                    {
                        // 2026-07-05 修正：FitArea() 保留原圖比例、置中留白，樣板照片掃描比例跟頁面
                        // cm 尺寸對不上時會縮小留白（見 TabletRenderer 同一輪修正的說明）。改用
                        // FitUnproportionally() 直接拉伸填滿容器，符合「疊圖＝座標系統」的比對用途。
                        layers.Layer()
                            .TranslateX(0, Unit.Centimetre)
                            .TranslateY(0, Unit.Centimetre)
                            .Width((float)PageWidthCm, Unit.Centimetre)
                            .Height((float)PageHeightCm, Unit.Centimetre)
                            .Image(TemplateImage).FitUnproportionally();
                    }

                    // Number (Top 3.8, Left 31.49729, 1cm Bold)
                    DrawText(layers, 3.8, 31.49729, 4.74896, 1.10272, 1.0 * PointsPerCm, data.Number, bold: true);

                    // HallName (Top 2.1, VAlign=Middle, 0.6cm)。2026-07-21 客訴：堂號在往者正上方，
                    // 跟著往者一起下移 DeadShiftY 維持上下對齊；2026-07-27 客訴：左右字改用 HallSecondX /
                    // HallFirstX 錨在往者最左欄同一條線上（見常數註解）。
                    // 2026-08-17：該格 ≥2 字時上移 0.2cm（見 HallTopOf）。兩格各自判斷。
                    DrawText(layers, HallTopOf(data.HallNameSecond), HallSecondX, 0.7, 1.3825, HallFontCm * PointsPerCm, data.HallNameSecond, vMiddle: true);
                    DrawText(layers, HallTopOf(data.HallNameFirst), HallFirstX, 0.7, 1.3825, HallFontCm * PointsPerCm, data.HallNameFirst, vMiddle: true);

                    // LivingNames 6 位（0.8cm）— tmpText.rdlc；主欄 lv[0] Left 21.87382、次欄 20.96131 /
                    // 20.0488，上排 Top 15.2748、整欄高 6.72806cm。2026-07-21 客訴：整體下移 LivingShiftY／
                    // 右移 LivingShiftX。2026-07-27 客訴「不管幾位字級都跟一位一樣」：改用 MatrixLayout ——
                    // 字級固定 NameBaseFontCm、下排起點＝「上排（有配對者）最長字數 +1 個字高間距」動態算，
                    // 取代舊的「固定列距 1.98436 + WithBottomGap 補空格 + GroupFontPt 以列距當可用高」
                    // （那組合在上下排都有名字時會把 3 字名縮到 1.98436/4 ≈ 0.50cm，正是客訴的「字變小」）。
                    const double livingFull = 6.72806;
                    var lv = data.LivingNames;
                    // 第 6 位（lv[5]）補在下排右欄 L21.87382（主欄 lv[0] 正下方），使矩陣對稱（座標確認見 business-rules-implicit §18）。
                    var (livingFontCm, livingBottomOffset) = VerticalText.MatrixLayout(
                        NameBaseFontCm, livingFull, (lv[0], lv[5]), (lv[1], lv[3]), (lv[2], lv[4]));
                    var fl = livingFontCm * PointsPerCm;

                    const double lY0 = 15.2748 + LivingShiftY;
                    var lY1 = lY0 + livingBottomOffset;
                    DrawText(layers, lY0, 21.87382 + LivingShiftX, 0.91251, livingFull, fl, lv[0], vertical: true);
                    DrawText(layers, lY0, 20.96131 + LivingShiftX, 0.91251, livingFull, fl, lv[1], vertical: true);
                    DrawText(layers, lY0, 20.0488 + LivingShiftX, 0.91251, livingFull, fl, lv[2], vertical: true);
                    DrawText(layers, lY1, 20.96131 + LivingShiftX, 0.91251, livingFull, fl, lv[3], vertical: true);
                    DrawText(layers, lY1, 20.0488 + LivingShiftX, 0.91251, livingFull, fl, lv[4], vertical: true);
                    DrawText(layers, lY1, 21.87382 + LivingShiftX, 0.91251, livingFull, fl, lv[5], vertical: true); // Six（補：下排右欄，主欄正下方）

                    // DeadName（Rectangle2 群組；欄 Left 改錨在 DeadLeftX，Top 維持 RDLC + DeadShiftY）。
                    // 往生／陽上**各自獨立**算字級——兩者共用同一個 NameBaseFontCm 基準，一般資料兩組都
                    // 不需縮字、自然一樣大；只有當某一組自己整欄鏈塞不下方框高時才各自縮小，不會因為另
                    // 一組縮小而連帶被拖小（見 docs/gotchas.md「往生字級被拖累」條）。
                    DrawDeadNames(layers, data);

                    // PhotoAddress（垂直地址 PNG）。RDLC 原 Top 4.1 Left 25.4 W 0.66 H 16.8；
                    // 2026-07-18 客訴：字加大——帶寬 0.66→0.75cm，搭配 VerticalAddress canvas
                    // 27×653px（27/653 ≈ 0.75/18.13，等比→FitArea 不再被高度壓小），每字約 0.75cm
                    // （仍 < 姓名 0.9cm）。
                    // 同日二、三輪使用者回饋定位：印在預印「臺灣」二字正下方——Top 4.1→4.9（「臺灣」
                    // 疊圖量測 y 3.30~4.64cm，下緣 +0.26cm 安全距）；Left 維持 25.4（曾右移 0.4 又移回：
                    // 「臺灣」x 25.51~26.04 中心 25.775，帶 25.4~26.15 恰好置中）。帶高 16.8→18.13
                    // （canvas 高同步 ×27/25，維持 ~23 字容量；帶尾 23.03cm，該欄 4.7cm 以下至頁底無預印字）。
                    // 同日四輪：超過單欄容量折兩欄（右欄先讀、平均拆），帶依 canvas 欄數等比加寬並
                    // **往左擴**——右欄固定在「臺灣」正下方（右緣恆 26.15cm）；左欄區 x 24.4~25.15
                    // 在 y≈22.5cm 前無預印字（22.8 起是「人氏奉」尾字，45+ 字極端地址才可能碰到）。
                    if (!string.IsNullOrEmpty(data.Address))
                    {
                        const double pxToCm = 0.75 / SkiaImageHelpers.AddressColWidthPx; // 等比：AddressColWidthPx px ↔ 0.75cm
                        var bandW = (SkiaImageHelpers.AddressColumns(data.Address) == 1
                            ? SkiaImageHelpers.AddressColWidthPx
                            : SkiaImageHelpers.AddressColWidthPx * 2 + SkiaImageHelpers.AddressColGapPx) * pxToCm;
                        const double bandRight = 25.9 + 0.75; // 2026-07-21 客訴右移 0.5cm（單欄時 Left = 25.9）

                        layers.Layer()
                            .TranslateX((float)(bandRight - bandW), Unit.Centimetre)
                            .TranslateY(4.9f, Unit.Centimetre)
                            .Width((float)bandW, Unit.Centimetre)
                            .Height(18.13f, Unit.Centimetre)
                            .Image(SkiaImageHelpers.VerticalAddress(data.Address)).FitArea();
                    }
                });
            });
        }).GeneratePdf();
    }

    /// <summary>往生欄可用高（RDLC Rectangle2 整欄高，兩變體相同）。</summary>
    private const double DeadFullHeight = 10.50374;

    /// <summary>
    /// 往生姓名。字級與下排起點由 <see cref="VerticalText.MatrixLayout"/> 決定（2026-07-27 客訴
    /// 「不管幾位，字級都跟一位一樣」）：以 <see cref="NameBaseFontCm"/> 起算，只有「上排最長字數
    /// ＋1 個字高間距＋下排最長字數」超出欄高時才整組等比縮，取代舊的「固定列距 2.06375 +
    /// WithBottomGap + GroupFontPt 以列距當可用高」（3 字名會被縮到 ≈0.50cm）。
    /// 與 LivingName **不跨組對齊**：往生擠到需要縮小時只縮往生自己（見 docs/gotchas.md）。
    /// 橫向欄位錨在 <see cref="DeadLeftX"/>（離左側預印字 0.5cm），縱向維持 RDLC Top + DeadShiftY。
    /// </summary>
    private static void DrawDeadNames(LayersDescriptor layers, TextData data)
    {
        var d = data.DeadNames;
        if (data.Template == TextTemplate.Two)
        {
            // tmpTextTwo — 恰 2 亡，皆整欄高並排、無上下排配對（bottom 傳 null → offset 0）。
            var (twoFontCm, _) = VerticalText.MatrixLayout(
                NameBaseFontCm, DeadFullHeight, (d[0], null), (d[1], null));
            var twoPt = twoFontCm * PointsPerCm;
            // 橫向必須在字級算完之後才回推（見 DeadTwoColumnsX 與 docs/gotchas.md「置中不可用固定位移量」）。
            var (twoLeftX, twoRightX) = DeadTwoColumnsX(twoFontCm);
            DrawText(layers, 3.65889 + DeadShiftY, twoRightX, 0.91251, DeadFullHeight, twoPt, d[0], vertical: true);
            DrawText(layers, 3.62361 + DeadShiftY, twoLeftX, 0.91251, DeadFullHeight, twoPt, d[1], vertical: true);
            return;
        }

        // tmpText — 6 格矩陣：主欄 d[0] 中間、d[1] 右上 / d[2] 左上 / d[3] 右下 / d[4] 左下、
        // 第 6 位 d[5] 補在主欄正下方使矩陣對稱（座標確認見 business-rules-implicit §18）。
        var (fontCm, bottomOffset) = VerticalText.MatrixLayout(
            NameBaseFontCm, DeadFullHeight, (d[0], d[5]), (d[1], d[3]), (d[2], d[4]));
        var fontPt = fontCm * PointsPerCm;
        const double topY = 3.65889 + DeadShiftY;
        var bottomY = topY + bottomOffset;

        DrawText(layers, topY, DeadMidX, 0.91251, DeadFullHeight, fontPt, d[0], vertical: true);    // One（主欄）
        DrawText(layers, topY, DeadRightX, 0.91251, DeadFullHeight, fontPt, d[1], vertical: true);  // Two
        DrawText(layers, topY, DeadLeftX, 0.91251, DeadFullHeight, fontPt, d[2], vertical: true);   // Three
        DrawText(layers, bottomY, DeadRightX, 0.91251, DeadFullHeight, fontPt, d[3], vertical: true); // Four
        DrawText(layers, bottomY, DeadLeftX, 0.91251, DeadFullHeight, fontPt, d[4], vertical: true);  // Five
        DrawText(layers, bottomY, DeadMidX, 0.91251, DeadFullHeight, fontPt, d[5], vertical: true);   // Six（補：下排中央，主欄正下方）
    }

    /// <summary>
    /// 恰 2 亡（<see cref="TextTemplate.Two"/>）兩高欄的 Left —— 整組以堂號墨跡中軸
    /// <see cref="HallCenterX"/> 置中（組寬 = <see cref="DeadTwoColOffset"/> + 一個字寬，直書字寬≈字級）。
    /// 2026-08-17 客訴「文牒往生者 2 位時名字要往右一點，落在兩邊堂號的置中位置」：Two 變體原本把最左欄
    /// 硬錨在 <see cref="DeadLeftX"/>（2026-07-27 的 0.5cm 間距錨點），兩欄相對距又只有 1.16299（比 Base
    /// 三欄的 2×0.91251 窄），整組墨跡中心落在 12.6885、比堂號中軸偏左 0.337cm。Base 變體本來就在中軸上
    /// （1 位 13.020、3~6 位 13.0195，與 13.0255 差 0.006cm），本修正是把 Two 拉回同一條線、不是新規則。
    /// ⚠️ 副作用（刻意）：最左欄離左側預印字自 0.5cm 變成 0.837cm。0.5cm 是「不要壓到預印字」的**下界**，
    /// 往右移是遠離它，**不是**退回 2026-07-21 那個被推翻的 DeadShiftX；<see cref="DeadLeftX"/> 錨點本身
    /// 未動（Base 三欄與堂號左字仍靠它）。
    /// 依**實際**字級回推而非寫死常數：docs/gotchas.md「套印置中不可用固定位移量，必須中軸 − 組寬/2，
    /// 且排在字級算完之後」（直書字寬≈字級，MatrixLayout 一縮字組寬就變）。
    /// </summary>
    internal static (double LeftX, double RightX) DeadTwoColumnsX(double fontCm)
    {
        var leftX = HallCenterX - (DeadTwoColOffset + fontCm) / 2.0;
        return (leftX, leftX + DeadTwoColOffset);
    }

    private static void DrawText(LayersDescriptor layers, double top, double left, double width, double height, double fontPt, string? text, bool bold = false, bool vMiddle = false, bool vertical = false)
    {
        if (string.IsNullOrEmpty(text)) return;

        // 直書姓名欄（同薦牌）：顯式每字一行（免 QuestPDF 窄欄丟字）+ 不約束寬度。
        // 字級由呼叫端用 VerticalText.GroupFontPt 算好「整組統一字級」後傳入，這裡不再逐格縮。
        var content = vertical ? VerticalText.Stack(text) : text;

        var fontCm = fontPt / PointsPerCm;
        var y = vMiddle ? top + (height - fontCm) / 2.0 : top;

        var layer = layers.Layer()
            .TranslateX((float)left, Unit.Centimetre)
            .TranslateY((float)y, Unit.Centimetre);
        if (!vertical) layer = layer.Width((float)width, Unit.Centimetre);

        var span = layer.Text(content).FontSize((float)fontPt).FontFamily(FontFamily).LineHeight(1f);
        if (bold) span.Bold();
    }

    private static byte[] LoadTemplate(string fileName)
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames().Single(n => n.EndsWith(fileName, StringComparison.Ordinal));
        using var stream = asm.GetManifestResourceStream(name)!;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}

public sealed record TextData(
    string Number,
    string? HallNameFirst,
    string? HallNameSecond,
    string?[] DeadNames,
    string?[] LivingNames,
    string? Address,
    TextTemplate Template);
