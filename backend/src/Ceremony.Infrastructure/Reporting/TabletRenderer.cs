using System.Reflection;
using Ceremony.Domain.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ceremony.Infrastructure.Reporting;

/// <summary>
/// 薦牌（牌位）PDF — 對齊 tmpTablet*.rdlc 9 變體。
/// </summary>
/// <remarks>
/// 頁面 11.5×25.5cm 窄長牌位；標楷體；Number 0.8cm Bold；座標**直接取自 RDLC XML**（含 Tablix / Rectangle
/// 巢狀換算成絕對值），對齊 docs/blueprints/printing-reports-positions.md §3-11。
/// 變體 + DeadName 字級（ParaFontSize）由 Domain.Services.PrintTemplateSelector.ChooseTablet 決定。
/// tmpTabletOneOne 特例：Page Top/Bottom margin 各 2cm（其餘 0）。
/// </remarks>
public sealed class TabletRenderer
{
    private const string FontFamily = "BiauKai";
    private const double PointsPerCm = 28.3464567;
    internal const double PageWidthCm = 11.5;
    // 2026-07-05 修正：使用者確認實體薦牌紙張為 11.5×25.5cm（原 RDLC 值 25.4cm 少了 0.1cm）。
    // 所有欄位座標都是從頁面左上角 (0,0) 起算的絕對值，改頁高不影響既有座標，只補足頁尾多出的
    // 0.1cm 空白，讓 PDF 頁面跟實體紙張尺寸一致（避免印表機用「符合紙張大小」縮放時整體跑位）。
    internal const double PageHeightCm = 25.5;

    // 2026-08-06 使用者指定：「薦牌列印位置整個左移 0.25cm」（收回同年 08-05 一度指定的 2.5cm，
    // 那個量會讓編號與全部陽上欄位的 X 變負值 → 落在 PDF 頁面外整組不印，實測已證實）。
    // 9 變體、全部欄位（編號／堂號／往者／陽上）共用這一個全域水平位移，套在 DrawText 的最後
    // 一步，欄與欄的相對關係完全不變，日後要回調或改量只動這個常數。
    // 位移後落點（2026-08-06 的欄位級右移、2026-08-08 的陽上 1/2 位左移已反映在下列來源值）：
    // 編號 0.8→0.55；堂號 4.4/6.4→4.15/6.15；往者中心線 5.735→5.485；陽上 1 位 1.43528→1.18528；
    // 陽上 2 位 1.8825/0.90611→1.6325/0.65611；陽上矩陣 0.7/1.427/2.154→0.45/1.177/1.904。
    // 全部為正＝都在 MediaBox 內。
    // ⚠️ 實體套印仍要特別看：往者 3+ 矩陣左欄左緣 = 5.485−0.9333−0.3 ≈ 4.252cm，距樣板雕花
    //    窗框內緣 4.191cm 只剩 0.061cm（右側 6.718 < 7.163 仍寬鬆）。再往左就會壓到窗框。
    //    （原先「編號與陽上矩陣左欄落到 0.25cm、低於 0.5cm 不可列印邊界下界」的風險，已由
    //     08-06 的編號 +0.3／陽上 +0.2 右移緩解：編號 0.55、陽上矩陣左欄 0.45。）
    internal const double GlobalShiftX = -0.25;

    // 開發用列印位置檢視工具的樣板照片（EmbeddedResource）；只在 debugOverlay:true 時載入使用，
    // 不進生產列印路徑。詳見 docs/blueprints/printing-reports.md「開發用列印位置檢視工具」。
    private static readonly byte[] TemplateImage = LoadTemplate("tablet-template.jpg");

    public byte[] Render(TabletData data, bool debugGrid = false, bool debugOverlay = false)
    {
        var paraPt = data.ParaFontSizeCm * PointsPerCm;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size((float)PageWidthCm, (float)PageHeightCm, Unit.Centimetre);
                if (data.Template == TabletTemplate.OneOne)
                {
                    // tmpTabletOneOne：上下各 2cm Page Margin（RDLC Page Margin，非 padding）
                    page.MarginTop(2, Unit.Centimetre);
                    page.MarginBottom(2, Unit.Centimetre);
                    page.MarginLeft(0);
                    page.MarginRight(0);
                }
                else
                {
                    page.Margin(0);
                }
                page.DefaultTextStyle(t => t.FontFamily(FontFamily).FontColor(Colors.Black));

                if (debugOverlay)
                {
                    // 2026-07-05：疊圖改畫在 page.Background()，不是 page.Content() 底下的 Layer。
                    // page.Content() 的座標系統／可視範圍會被 page.Margin(...) 裁掉（OneOne 變體上下各
                    // 2cm），連用負值 TranslateY 位移都會被整層裁掉（實測驗證，見下方 Tablet_DebugOverlay_
                    // DumpsCalibrationPdf 註解）。page.Background() 則是畫在「整張實體紙」的座標系統，
                    // 不受 Margin 影響，樣板照片（含牌位圖案在 margin 區域內的部分）才能完整顯示，
                    // 才看得出「這個變體選擇只在中段印字」相對於整張牌位圖案是否合理。
                    // FitArea() 保留原圖比例、置中留白，樣板照片實際比例（掃描誤差）跟頁面 cm 比例對不上
                    // 時會留白（使用者反映「template變比較小」正是這個）；改用 FitUnproportionally()
                    // 直接拉伸填滿整張紙——這個工具本來就是假設樣板照片＝我們的 cm 座標系統。
                    page.Background().Image(TemplateImage).FitUnproportionally();
                }

                page.Content().Layers(layers =>
                {
                    // PrimaryLayer 一定要建立（決定 Layers 容器尺寸），但 debugOverlay 時改用透明色，
                    // 否則白底會蓋掉 page.Background() 疊的樣板照片。
                    layers.PrimaryLayer().Background(debugOverlay ? Colors.Transparent : "#FFFFFF");

                    // 共用：Number（左上角，2026-07-05 使用者指定往下 0.1cm、往右 0.1cm，0.8cm Bold，VAlign=Middle）
                    // + HallName（6.1，0.6cm，VAlign=Middle）
                    // 2026-07-05 再修正：OneOne 有 2cm Page Margin，content 座標系統起點比其他無 margin
                    // 變體（例如 TwoOne/UnderscoreOne 用同一個 LivingNameOne Top=14.00389）多降 2cm，
                    // 導致 Number/HallName 印出來的實體頁面位置比其他變體低了 2cm（實測：content-Y=0.1
                    // 實際印在 true-page-Y≈2.3cm，不是預期的 0.1cm 附近）。試著扣掉 margin 補償看是否會被
                    // QuestPDF 裁掉（見下方 DrawDeadNames/DrawLivingNames 同一輪修正的實測結果）。
                    var marginCompensation = data.Template == TabletTemplate.OneOne ? 2.0 : 0.0;
                    // 2026-07-17 使用者反映編號超出列印範圍（reference/薦牌.jpg「郵27」的「郵」左半被
                    // 印表機不可列印邊界裁掉）：Left 0.1 → 0.5 內縮到可列印區；Top 維持 0.1（照片上緣未被裁）。
                    // 2026-07-21 客訴：編號往下移 0.1cm → Top 0.1 → 0.2（9 變體共用；OneOne 2cm Margin 補償不變）。
                    // 2026-08-06 使用者指定：編號右移 0.3cm（Left 0.5 → 0.8；含 GlobalShiftX 後實際落點
                    // 0.25 → 0.55，重新回到 07-17 為避開印表機不可列印邊界設的 0.5cm 下界之上）。
                    DrawText(layers, 0.2 - marginCompensation, 0.8, 4.29646, 1.13229, 0.8 * PointsPerCm, data.Number, bold: true, vMiddle: true);
                    // 2026-08-06 使用者指定：堂號右移 0.5cm（Left 3.9/5.9 → 4.4/6.4）、下移 0.2cm
                    // （Top 6.1 → 6.3）。兩字相對距離 2.0cm 不變；右字含 GlobalShiftX 後右緣
                    // 6.4-0.25+0.7=6.85cm，仍在頁寬 11.5cm 內。
                    // 2026-08-08 使用者指定：該格 ≥2 字時上移 0.2cm（見 HallTopOf）。兩格各自判斷。
                    DrawText(layers, HallTopOf(data.HallNameSecond) - marginCompensation, 4.4, 0.7, 1.3825, 0.6 * PointsPerCm, data.HallNameSecond, vMiddle: true);
                    DrawText(layers, HallTopOf(data.HallNameFirst) - marginCompensation, 6.4, 0.7, 1.3825, 0.6 * PointsPerCm, data.HallNameFirst, vMiddle: true);

                    DrawDeadNames(layers, data, paraPt);
                    DrawLivingNames(layers, data);

                    // 現場對位校正用：疊 1cm 刻度格線（不進生產列印路徑，debugGrid 預設 false）。
                    // 用途：reference/薦牌問題.pdf 反映實體牌位座插入後文字對不準視窗，但沒有實測
                    // 尺寸可校正座標。印這張帶格線版本、插入同一個牌位座，回報視窗上緣/下緣對到
                    // 第幾條刻度線，才能算出精確修正量（見 docs/gotchas.md「薦牌實體對位」條）。
                    if (debugGrid) DrawCalibrationGrid(layers);
                });
            });
        }).GeneratePdf();
    }

    /// <summary>堂號單字格的 Top（1.3825cm 高格內 vMiddle 置中）。</summary>
    private const double HallNameTop = 6.3;

    /// <summary>
    /// 堂號 ≥2 字格的 Top：比單字格高 0.2cm。
    /// </summary>
    /// <remarks>
    /// 2026-08-08 使用者指定「堂號一邊有兩個字時上移 0.2cm」。根因：堂號是**橫排**欄位（非直書），
    /// 0.6cm 字塞 0.7cm 欄寬，第 2 個字被 QuestPDF 換行往下長；但 <see cref="DrawText"/> 的 vMiddle
    /// 只用**單字高**算置中位移（`top + (height − fontCm)/2`），不知道實際渲染成兩列 → 整塊視覺重心
    /// 下沉。這裡以 Top 補回，不動 vMiddle 公式（改公式會連帶影響編號欄）。
    /// </remarks>
    private const double HallNameTopMultiChar = 6.1;

    /// <summary>
    /// 依該格字數決定堂號 Top。逐格獨立判斷：`SplitHallName` 2 字→1+1、4 字→2+2、3 字或 5 字以上
    /// →整串進 First（左格空），所以 3 字堂號也會走 ≥2 字這條。
    /// </summary>
    /// <remarks>
    /// 必須用 <see cref="VerticalText.ElementCount"/> 而非 <c>.Length</c>——增補平面造字（`𡍼` 等）
    /// 在 <c>.Length</c> 算兩格會誤判成多字（見 gotchas「一個字不等於一個 char」）。
    /// </remarks>
    internal static double HallTopOf(string? segment)
        => VerticalText.ElementCount(segment) >= 2 ? HallNameTopMultiChar : HallNameTop;

    private static void DrawCalibrationGrid(LayersDescriptor layers)
    {
        const string gridColor = "#FF00FF"; // 桃紅：跟黑色文字/牌位雕花都能明顯區分
        for (var x = 0; x <= 11; x++)
        {
            layers.Layer()
                .TranslateX(x, Unit.Centimetre)
                .Height((float)PageHeightCm, Unit.Centimetre)
                .LineVertical(0.3f).LineColor(gridColor);
            layers.Layer()
                .TranslateX(x + 0.05f, Unit.Centimetre)
                .Text($"{x}").FontSize(6).FontColor(gridColor);
        }
        for (var y = 0; y <= 25; y++)
        {
            layers.Layer()
                .TranslateY(y, Unit.Centimetre)
                .Width((float)PageWidthCm, Unit.Centimetre)
                .LineHorizontal(0.3f).LineColor(gridColor);
            layers.Layer()
                .TranslateX(11.1f, Unit.Centimetre)
                .TranslateY(y + 0.02f, Unit.Centimetre)
                .Text($"{y}").FontSize(6).FontColor(gridColor);
        }
    }

    // 2026-07-05 改版（使用者指定）：以「故」「靈位」兩組靜態字（樣板紙預印，非本程式繪製）的字符
    // 中心線為基準排亡者，取代之前各變體各自沿用 RDLC 固定座標、對不齊中心線的做法。
    // 中心線 X 座標從 reference/template/薦牌.jpg（200 DPI）像素量測：「故」字 bounding box 中心
    // 5.6769cm、「靈位」bounding box 中心 5.696cm，兩者幾乎重合（誤差 0.02cm），取平均 5.685cm；
    // 這個值也跟窗框內緣量測寬度的幾何中心（(4.191+7.163)/2=5.677cm）幾乎一致，互相印證。
    // 位置在算出 GroupFontPt 的共用字級「之後」才動態算，而非像之前用編譯期常數——因為要置中的是
    // 「實際渲染寬度」（＝字級，直書 CJK 字寬≈字級），縮字後置中位置也要跟著變，否則會偏一邊。
    // DeadCenterX 是 1/2/3+ 位往者共用的排版中心線，移這一個常數即可讓所有往者欄位
    // （含 2 位左右分居、3+ 位 2×3 矩陣）整體水平位移，各變體之間的相對關係不變。
    // 位移沿革：量測基準 5.685 →（2026-07-21 客訴右移 0.1）5.785 →（2026-07-31 客訴左移 0.05）
    // 5.735，即相對量測基準淨右移 0.05cm。邊界複核（3+ 矩陣、0.6cm 字級）：右欄右緣
    // ≈6.968cm < 窗框內緣 7.163cm、左欄左緣 ≈4.502cm > 窗框內緣 4.191cm，兩側皆安全。
    private const double DeadCenterX = 5.735;
    private const double DeadColumnGap = 0.1; // 相鄰欄位之間的留白，避免緊貼（1-2 位亡者變體用）
    // 「故」字下緣 Y=7.5946cm、「靈」字上緣 Y=13.462cm（同一次像素量測），中間空隙 5.8674cm。
    internal const double DeadGapTop = 7.5946;

    /// <summary>
    /// 往者文字的**下界**：1 位／2 位／3+ 位矩陣三種排法共用，任何字數都不得越過。
    /// </summary>
    /// <remarks>
    /// 2026-08-06 客訴（reference 客戶實印照片）：往者的字壓在預印的「靈位」上。根因是**三種排法各自帶
    /// 一個可用高常數、來源還不同**——1 位用 `DeadGapHeight−0.1`（下緣 13.362）、2 位用 RDLC 遺留值
    /// `6.31`（下緣 **13.89**，早就越過「靈」上緣）、3+ 位用手寫量測方框 5.4（下緣 13.1946）。
    /// 客戶那筆是 **2 位往生者、每格用全形空格把兩個人名塞成上下兩段**（8 個字素）＝走 2 位分支，
    /// 8 × (6.31/8) 剛好吃滿錯誤的可用高 → 壓字 0.43cm。用同一筆資料重現、逐像素對得上照片。
    ///
    /// 取值 13.1946 ＝ 3+ 位矩陣沿用至今的框底（`DeadMatrixTop + 5.4`）：那條線已在生產跑過而且**沒有
    /// 客訴**，是目前唯一有實務背書的下界；1 位／2 位一律收齊到同一條線，矩陣本身數值不變。
    /// 距實體紙上「靈」字上緣（13.373 ＝樣板量測 13.462 − 照片紙外留白 0.089）尚有 0.18cm。
    ///
    /// 日後要再往上收（等現場格線校正版回報），**只動這一個常數**，三種排法同步——不要再回到
    /// 「同語意散成三個常數、錯一個沒人發現」的狀態。
    /// </remarks>
    internal const double DeadTextBottom = DeadMatrixTop + DeadMatrixHeight; // 13.1946

    /// <summary>1 位／2 位往者的可用高（3+ 位走 <see cref="DeadMatrixHeight"/>，起點低 0.2cm）。</summary>
    private const double DeadGapHeight = DeadTextBottom - DeadGapTop;        // 5.6

    // 2026-07-17 使用者指定（reference/薦牌.jpg 手寫量測）：3+ 位亡者的 2×3 矩陣改在
    // 「故」下緣 +0.2cm 起、寬 2.8cm × 高 5.4cm 的方框內排（框底 13.1946＝現在的 DeadTextBottom，
    // 離實體紙上「靈」字上緣 13.373 還有 0.18cm——2026-08-06 修正：原註記的 0.27cm 是拿樣板照片座標
    // 13.462 算的，而照片含 0.089cm 紙外留白）；欄距取 2.8/3=0.9333cm（與舊 RDLC Rectangle 內 Left 0.1/1.0/1.9 的 0.9cm
    // 欄距幾乎一致），中間欄置中在故/靈位中心線上。下排起點與字級由 VerticalText.MatrixLayout
    // 動態決定（取代固定列距 1.8639 + WithBottomGap——那組合會把 3 字名縮到 0.47cm，客訴「字太小」）。
    private const double DeadMatrixTop = DeadGapTop + 0.2;   // 7.7946
    private const double DeadMatrixHeight = 5.4;
    private const double DeadMatrixColPitch = 2.8 / 3.0;     // 0.9333

    private static void DrawDeadNames(LayersDescriptor layers, TabletData data, double paraPt)
    {
        var d = data.DeadNames;
        switch (data.Template)
        {
            case TabletTemplate.OneOne:
            case TabletTemplate.OneTwo:
            case TabletTemplate.One:
            {
                // 1 位亡者：水平置中在「故／靈位」中心線 X 上；垂直方向緊接在「故」正下方起排
                // （2026-07-05 兩輪修正：一開始貼著故下緣沒問題，中途誤改成「垂直置中在故～靈位
                // 整個空隙」導致文字飄到中間、離故太遠，使用者糾正後改回「故正下方」＝DeadGapTop）。
                // avail 從原本 6.466（RDLC 值，比實測空隙 5.8674 大，極端長名字理論上會超出「靈」字
                // 上緣）收緊到 DeadGapHeight 扣一點安全邊界，確保縮字上限不會超出這個空隙。
                // 2026-07-05 三度修正：DeadGapTop 是從樣板照片（整張實體紙）量到的 true-page 座標，
                // 但 OneOne 有 2cm Page Margin，content 座標系統起點比 true page 低 2cm——直接拿
                // DeadGapTop 當 content-Y 用，會讓文字實際印到比「故」下緣低 2cm 的地方，蓋過「靈位」。
                // OneTwo/One 沒有這個 margin，DeadGapTop 可以直接當 content-Y 用。
                var marginCompensation = data.Template == TabletTemplate.OneOne ? 2.0 : 0.0;
                // 2026-08-06：可用高改由 DeadTextBottom 推導（原本是 DeadGapHeight−0.1＝5.7674，
                // 下緣 13.362；收齊到 13.1946 這條三種排法共用的線）。
                var avail = DeadGapHeight;
                var f = VerticalText.GroupFontPt(paraPt, (d[0], avail));
                var fontCm = f / PointsPerCm;
                DrawText(layers, DeadGapTop - marginCompensation, DeadCenterX - fontCm / 2, 0.8, avail, f, d[0], vertical: true);
                break;
            }

            case TabletTemplate.TwoOne:
            case TabletTemplate.TwoTwo:
            case TabletTemplate.Two:
            {
                // 2 位亡者：以中心線對稱分居左右（One 右、Two 左，中間留 DeadColumnGap 不貼在一起）
                // 2026-08-06 客訴修正（客戶實印照片壓到「靈位」的根因就在這兩行）：
                //  (a) 可用高 6.31 → DeadGapHeight。6.31 是 RDLC 遺留值，比實際空隙大 0.43cm，等於把
                //      GroupFontPt 的保護上限設在真實邊界**之外**，自動縮字形同失效——8 個字素起
                //      （客戶用全形空格把兩個人名塞在同一格就是 8 字素）文字下緣落到 13.89，壓進「靈位」。
                //  (b) Top 7.5825 → DeadGapTop（7.5946）。7.5825 是 RDLC 值，與 1 位分支用的量測值
                //      差 0.012cm；兩者本來就該是同一條「故」字下緣。
                var avail = DeadGapHeight;
                var f = VerticalText.GroupFontPt(paraPt, (d[0], avail), (d[1], avail));
                var fontCm = f / PointsPerCm;
                var rightX = DeadCenterX + DeadColumnGap / 2;
                var leftX = DeadCenterX - DeadColumnGap / 2 - fontCm;
                DrawText(layers, DeadGapTop, rightX, 0.8, avail, f, d[0], vertical: true);
                DrawText(layers, DeadGapTop, leftX, 0.8, avail, f, d[1], vertical: true);
                break;
            }

            default:
            {
                // Base / UnderscoreOne / UnderscoreTwo — 3+ 位亡者，2×3 矩陣：
                // 1st 中間上、2nd 右邊上、3rd 左邊上、4th 右邊下、5th 左邊下、6th 中間下。
                // 2026-07-17 使用者指定改版（reference/薦牌.jpg 手寫量測，取代固定列距 1.8639 +
                // WithBottomGap 的做法——上下排都有名字時那會把 3 字名縮到 0.47cm、4 字名 0.37cm，
                // 客訴「五位時字太小」）：整個矩陣排在「故」下 0.2cm 起的 2.8×5.4cm 方框內，
                // 字級以 ParaFontSize（3+ 亡固定 0.6cm）起算，塞不下整欄鏈才整組等比縮；
                // 下排起點動態＝上排（有配對者）最長字數 +1 個字高間距，不再是固定 9.4464。
                var (fontCm, bottomOffset) = VerticalText.MatrixLayout(
                    data.ParaFontSizeCm, DeadMatrixHeight,
                    (d[0], d[5]), (d[1], d[3]), (d[2], d[4]));
                var f = fontCm * PointsPerCm;
                var centerX = DeadCenterX - fontCm / 2;                       // 中間欄置中在故/靈位中心線
                var rightX = DeadCenterX + DeadMatrixColPitch - fontCm / 2;  // 右欄＝中心線 + 欄距
                var leftX = DeadCenterX - DeadMatrixColPitch - fontCm / 2;   // 左欄＝中心線 − 欄距
                var bottomTop = DeadMatrixTop + bottomOffset;
                DrawText(layers, DeadMatrixTop, centerX, 0.6, DeadMatrixHeight, f, d[0], vertical: true); // One（主，中間上）
                DrawText(layers, DeadMatrixTop, rightX, 0.6, DeadMatrixHeight, f, d[1], vertical: true);  // Two（右邊上）
                DrawText(layers, DeadMatrixTop, leftX, 0.6, DeadMatrixHeight, f, d[2], vertical: true);   // Three（左邊上）
                DrawText(layers, bottomTop, rightX, 0.6, DeadMatrixHeight, f, d[3], vertical: true);      // Four（右邊下）
                DrawText(layers, bottomTop, leftX, 0.6, DeadMatrixHeight, f, d[4], vertical: true);       // Five（左邊下）
                DrawText(layers, bottomTop, centerX, 0.6, DeadMatrixHeight, f, d[5], vertical: true);     // Six（中間下，主欄正下方）
                break;
            }
        }
    }

    // 2026-07-17 使用者指定（reference/薦牌.jpg 手寫量測「1cm」註記 + 客訴「陽上間距太寬、超出
    // 列印範圍」）：3-6 位陽上矩陣改版。
    // - 起點：樣板預印「陽上」標籤下緣（reference/template/薦牌.jpg 量測 y=13.579cm）再往下 1cm
    //   ＝14.579（舊值 14.00389 只離標籤 0.43cm，太貼）。
    // - 左界：0.5cm——舊 RDLC 最左欄 Left=0.1 落在印表機不可列印邊界內，實印時整欄消失
    //   （客訴照片 5 位陽上只印出 3 位），全部欄位往右移到可列印區內。
    // - 欄距維持 RDLC 的 0.727cm；右欄右緣 0.5+2×0.727+0.6=2.554，仍在標籤帶左側雕花內緣
    //   （量測最窄 2.70cm @y14-14.5）之內。
    // - 下界：維持原 14.00389+5.5=19.504（拜薦 預印字上緣 20.49 之上）→ 方框高 4.925cm。
    // - 字級/下排起點：VerticalText.MatrixLayout 動態決定（同亡者矩陣；舊固定列距 1.43785 +
    //   WithBottomGap 會把 3 字名縮到 0.36cm——客訴「字太小」「間距（相對）太寬」的根因）。
    private const double LivingMatrixTop = 14.579;
    private const double LivingMatrixHeight = 19.504 - LivingMatrixTop; // 4.925
    // 2026-08-06 使用者指定「陽上右移 0.2cm」：1 位／2 位／3-6 位矩陣三種排法一起右移，
    // 最左欄 0.5 → 0.7（含 GlobalShiftX 後 0.45），右欄右緣 0.7+2×0.727+0.6=2.754、
    // 含位移後 2.504cm，仍在標籤帶左側雕花內緣（量測最窄 2.70cm @y14~14.5）之內。
    private const double LivingMatrixColLeft = 0.7;    // 最左欄（Three/Five）
    private const double LivingMatrixColPitch = 0.727; // 欄距（沿用 RDLC 1.56167/0.83528/0.1 的間距）

    // 2026-08-08 使用者指定：陽上 **1 位／2 位** 左移 0.1cm、下移 0.2cm（3-6 位矩陣不動）。
    // 同時把原本散在 DrawText 呼叫參數裡的裸字面值（14.00389 / 5.5 / 1.53528 / 1.9825 / 1.00611）
    // 收成具名常數——這正是 2026-08-06 往者客訴「同語意散成多個常數、錯一個沒人發現」的同型風險。
    //
    // ⚠️ **1/2 位的下界（19.70389）刻意比 3-6 位矩陣框底（LivingMatrixTop + LivingMatrixHeight
    //    ＝19.504）低 0.2cm**：使用者這次選的是「整塊下移、可用高維持 5.5」而非「守住下界、縮可用高」。
    //    這是刻意的，不是漏改一半——別比照 DeadTextBottom 把它「修正」成同一條線。
    //
    // 邊界複核（含 GlobalShiftX = -0.25）：
    //   最左緣 l[1]  0.90611-0.25 = 0.65611 > 0.5（印表機不可列印邊界），餘裕 0.156cm
    //   最右緣 l[0]  1.8825-0.25+0.8 = 2.4325 < 2.70（雕花內緣量測最窄 @y14~14.5），餘裕 0.268cm
    //   文字下緣     14.20389+5.5 = 19.70389 < 20.49（預印「拜薦」上緣），餘裕 0.786cm
    //   距預印「陽上」標籤下緣 13.579：0.625cm（原 0.425cm，往下拉開）
    internal const double LivingPairTop = 14.20389;       // 原 14.00389
    internal const double LivingPairHeight = 5.5;         // 可用高不變（＝整塊下移）
    internal const double LivingOneLeft = 1.43528;        // 原 1.53528
    internal const double LivingTwoLeftFirst = 1.8825;    // 原 1.9825   （l[0]）
    internal const double LivingTwoLeftSecond = 0.90611;  // 原 1.00611  （l[1]）

    private static void DrawLivingNames(LayersDescriptor layers, TabletData data)
    {
        var l = data.LivingNames;
        var pt08 = 0.8 * PointsPerCm;
        switch (data.Template)
        {
            case TabletTemplate.OneOne:
            case TabletTemplate.TwoOne:
            case TabletTemplate.UnderscoreOne:
            {
                // 1 位陽上（0.8cm，高欄）
                // 2026-07-05 修正：OneOne 有 2cm Page Margin，跟 TwoOne/UnderscoreOne 共用同一個
                // Top=14.00389 座標，但 OneOne 的 content 座標系統起點比 true page 低 2cm，實測印出來
                // 比 TwoOne/UnderscoreOne（同一個 Top 值）低了 2cm，跟樣板紙預印的「陽上」標籤距離
                // 明顯拉大。扣掉 margin 補償讓三個變體印在同一個實體頁面高度。
                var marginCompensation = data.Template == TabletTemplate.OneOne ? 2.0 : 0.0;
                var f = VerticalText.GroupFontPt(pt08, (l[0], LivingPairHeight));
                // 2026-07-21 客訴：陽上 1 位時列印位置右移 0.5cm（Left 0.83528 → 1.33528）。
                // 2026-08-06 使用者指定：陽上再右移 0.2cm（1.33528 → 1.53528）。
                // 2026-08-08 使用者指定：左移 0.1cm（1.53528 → 1.43528）、下移 0.2cm（Top 見 LivingPairTop）。
                DrawText(layers, LivingPairTop - marginCompensation, LivingOneLeft, 0.8, LivingPairHeight, f, l[0], vertical: true);
                break;
            }

            case TabletTemplate.OneTwo:
            case TabletTemplate.TwoTwo:
            case TabletTemplate.UnderscoreTwo:
            {
                // 2 位陽上（0.8cm，高欄）
                var f = VerticalText.GroupFontPt(pt08, (l[0], LivingPairHeight), (l[1], LivingPairHeight));
                // 2026-07-21 客訴：陽上 2 位時列印位置右移 0.5cm（l[0] 1.2825→1.7825、l[1] 0.30611→0.80611）。
                // 附帶好處：l[1] 原 Left 0.30611 逼近印表機不可列印邊界（doc §3 標記的風險），右移後緩解。
                // 2026-08-06 使用者指定：陽上再右移 0.2cm（l[0] 1.7825→1.9825、l[1] 0.80611→1.00611）。
                // 2026-08-08 使用者指定：左移 0.1cm（1.9825→1.8825、1.00611→0.90611）、下移 0.2cm。
                DrawText(layers, LivingPairTop, LivingTwoLeftFirst, 0.8, LivingPairHeight, f, l[0], vertical: true);
                DrawText(layers, LivingPairTop, LivingTwoLeftSecond, 0.8, LivingPairHeight, f, l[1], vertical: true);
                break;
            }

            // 3-6 位陽上（0.6cm 起算）2×3 矩陣：One 右欄、Two 中欄、Three 左欄；下排 Four 中、
            // Five 左、Six 右（主欄正下方）。Two/One/Base 三個變體舊 RDLC 只差 l[0] 的 Left 微調
            // （1.52639/1.56167），2026-07-17 改版後統一為同一組欄位座標（見 LivingMatrix* 常數註解），
            // 變體「選擇」邏輯不變（PrintTemplateSelector），僅繪製座標統一。
            default:
            {
                var (fontCm, bottomOffset) = VerticalText.MatrixLayout(
                    0.6, LivingMatrixHeight,
                    (l[0], l[5]), (l[1], l[3]), (l[2], l[4]));
                var f = fontCm * PointsPerCm;
                var leftX = LivingMatrixColLeft;                              // Three/Five
                var midX = LivingMatrixColLeft + LivingMatrixColPitch;        // Two/Four
                var rightX = LivingMatrixColLeft + 2 * LivingMatrixColPitch;  // One/Six（主欄）
                var bottomTop = LivingMatrixTop + bottomOffset;
                DrawText(layers, LivingMatrixTop, rightX, 0.7, LivingMatrixHeight, f, l[0], vertical: true);
                DrawText(layers, LivingMatrixTop, midX, 0.7, LivingMatrixHeight, f, l[1], vertical: true);
                DrawText(layers, LivingMatrixTop, leftX, 0.7, LivingMatrixHeight, f, l[2], vertical: true);
                DrawText(layers, bottomTop, midX, 0.7, LivingMatrixHeight, f, l[3], vertical: true);
                DrawText(layers, bottomTop, leftX, 0.7, LivingMatrixHeight, f, l[4], vertical: true);
                DrawText(layers, bottomTop, rightX, 0.7, LivingMatrixHeight, f, l[5], vertical: true);
                break;
            }
        }
    }

    private static void DrawText(LayersDescriptor layers, double top, double left, double width, double height, double fontPt, string? text, bool bold = false, bool vMiddle = false, bool vertical = false)
    {
        if (string.IsNullOrEmpty(text)) return;

        // 直書姓名欄：RDLC 用窄欄 + 自動換行達成「一字一列」。改用顯式換行（每字一行）並
        // 不約束寬度——否則標楷體全形字寬 ≈ 欄寬，QuestPDF 因放不下不可斷的單字而**靜默丟字**
        // （PingFang fallback 時字較窄剛好塞進去，換成真標楷體就消失）。
        // 直書姓名欄：顯式每字一行（免 QuestPDF 窄欄丟字）+ 不約束寬度。
        // 字級由呼叫端用 VerticalText.GroupFontPt 算好「整組統一字級」後傳入，這裡不再逐格縮。
        var content = vertical ? VerticalText.Stack(text) : text;

        var fontCm = fontPt / PointsPerCm;
        // vMiddle：以 translate 位移模擬 VerticalAlign=Middle（橫向單字，如 Number / 堂號）。
        var y = vMiddle ? top + (height - fontCm) / 2.0 : top;

        // GlobalShiftX：9 變體所有欄位共用的整體水平位移（2026-08-06 使用者指定左移 0.25cm）。
        // 收在這裡而不是加進各欄常數，是為了讓「整體位移」與「量測基準座標」分離——上面每個
        // Left 常數都還是樣板量測／RDLC 的原始語意，位移只有一個改點。
        var layer = layers.Layer()
            .TranslateX((float)(left + GlobalShiftX), Unit.Centimetre)
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

public sealed record TabletData(
    string Number,
    string? HallNameFirst,
    string? HallNameSecond,
    string?[] DeadNames,      // 6 元素
    string?[] LivingNames,    // 6 元素
    double ParaFontSizeCm,    // 由 PrintTemplateSelector 決定 (0.6 or 0.8)
    TabletTemplate Template);
