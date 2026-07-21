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
    private const double PageWidthCm = 11.5;
    // 2026-07-05 修正：使用者確認實體薦牌紙張為 11.5×25.5cm（原 RDLC 值 25.4cm 少了 0.1cm）。
    // 所有欄位座標都是從頁面左上角 (0,0) 起算的絕對值，改頁高不影響既有座標，只補足頁尾多出的
    // 0.1cm 空白，讓 PDF 頁面跟實體紙張尺寸一致（避免印表機用「符合紙張大小」縮放時整體跑位）。
    private const double PageHeightCm = 25.5;

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
                    DrawText(layers, 0.2 - marginCompensation, 0.5, 4.29646, 1.13229, 0.8 * PointsPerCm, data.Number, bold: true, vMiddle: true);
                    DrawText(layers, 6.1 - marginCompensation, 3.9, 0.7, 1.3825, 0.6 * PointsPerCm, data.HallNameSecond, vMiddle: true);
                    DrawText(layers, 6.1 - marginCompensation, 5.9, 0.7, 1.3825, 0.6 * PointsPerCm, data.HallNameFirst, vMiddle: true);

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
    // 2026-07-21 客訴：往者整個列印位置右移 0.1cm。DeadCenterX 是 1/2/3+ 位往者共用的排版中心線，
    // 移這一個常數即可讓所有往者欄位（含 2 位左右分居、3+ 位 2×3 矩陣）整體右移 0.1cm。
    // 量測基準 5.685 → 5.785；右移後 3+ 矩陣右欄右緣 ≈7.018cm 仍在窗框內緣 7.163cm 之內。
    private const double DeadCenterX = 5.785;
    private const double DeadColumnGap = 0.1; // 相鄰欄位之間的留白，避免緊貼（1-2 位亡者變體用）
    // 「故」字下緣 Y=7.5946cm、「靈」字上緣 Y=13.462cm（同一次像素量測），中間空隙 5.8674cm。
    private const double DeadGapTop = 7.5946;
    private const double DeadGapHeight = 13.462 - 7.5946; // 5.8674

    // 2026-07-17 使用者指定（reference/薦牌.jpg 手寫量測）：3+ 位亡者的 2×3 矩陣改在
    // 「故」下緣 +0.2cm 起、寬 2.8cm × 高 5.4cm 的方框內排（框底 13.1946，離「靈」上緣 13.462
    // 還有 0.27cm）；欄距取 2.8/3=0.9333cm（與舊 RDLC Rectangle 內 Left 0.1/1.0/1.9 的 0.9cm
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
                var avail = DeadGapHeight - 0.1;
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
                var f = VerticalText.GroupFontPt(paraPt, (d[0], 6.31), (d[1], 6.31));
                var fontCm = f / PointsPerCm;
                var rightX = DeadCenterX + DeadColumnGap / 2;
                var leftX = DeadCenterX - DeadColumnGap / 2 - fontCm;
                DrawText(layers, 7.5825, rightX, 0.8, 6.31, f, d[0], vertical: true);
                DrawText(layers, 7.5825, leftX, 0.8, 6.31, f, d[1], vertical: true);
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
    private const double LivingMatrixColLeft = 0.5;    // 最左欄（Three/Five）
    private const double LivingMatrixColPitch = 0.727; // 欄距（沿用 RDLC 1.56167/0.83528/0.1 的間距）

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
                var f = VerticalText.GroupFontPt(pt08, (l[0], 5.5));
                // 2026-07-21 客訴：陽上 1 位時列印位置右移 0.5cm（Left 0.83528 → 1.33528）。
                DrawText(layers, 14.00389 - marginCompensation, 1.33528, 0.8, 5.5, f, l[0], vertical: true);
                break;
            }

            case TabletTemplate.OneTwo:
            case TabletTemplate.TwoTwo:
            case TabletTemplate.UnderscoreTwo:
            {
                // 2 位陽上（0.8cm，高欄）
                var f = VerticalText.GroupFontPt(pt08, (l[0], 5.5), (l[1], 5.5));
                // 2026-07-21 客訴：陽上 2 位時列印位置右移 0.5cm（l[0] 1.2825→1.7825、l[1] 0.30611→0.80611）。
                // 附帶好處：l[1] 原 Left 0.30611 逼近印表機不可列印邊界（doc §3 標記的風險），右移後緩解。
                DrawText(layers, 14.00389, 1.7825, 0.8, 5.5, f, l[0], vertical: true);
                DrawText(layers, 14.00389, 0.80611, 0.8, 5.5, f, l[1], vertical: true);
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

public sealed record TabletData(
    string Number,
    string? HallNameFirst,
    string? HallNameSecond,
    string?[] DeadNames,      // 6 元素
    string?[] LivingNames,    // 6 元素
    double ParaFontSizeCm,    // 由 PrintTemplateSelector 決定 (0.6 or 0.8)
    TabletTemplate Template);
