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
/// 字型固定 BiauKai；DeadName / LivingName 0.9cm、HallName 0.6cm VAlign=Middle、Number 1cm Bold。
/// 2026-07-18 客訴調整（刻意偏離 RDLC 原 0.8cm/0.66cm）：地址加大（0.66→0.75cm）＋下移 0.8cm
/// （Top 4.9，印在預印「臺灣」正下方、水平置中）、亡/陽姓名加大（0.8→0.9cm，仍 &lt; 欄距 0.91251
/// 不重疊），且姓名必須比地址大。
/// </remarks>
public sealed class TextRenderer
{
    private const string FontFamily = "BiauKai";
    private const double PointsPerCm = 28.3464567;
    private const double PageWidthCm = 36.5;
    private const double PageHeightCm = 26.2;

    // 亡/陽姓名起始字級（cm）。RDLC 原 0.8，2026-07-18 依客訴加大；上限受欄距 0.91251cm 制約
    // （直書字寬≈字級，再大會蓋到隔壁欄），且必須維持 > 地址字級（0.75cm）。
    private const double NameBaseFontCm = 0.9;

    // 開發用列印位置檢視工具的樣板照片（EmbeddedResource）；只在 debugOverlay:true 時載入使用，
    // 不進生產列印路徑。詳見 docs/blueprints/printing-reports.md「開發用列印位置檢視工具」。
    private static readonly byte[] TemplateImage = LoadTemplate("text-template.jpg");

    public byte[] Render(TextData data, bool debugOverlay = false)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(36.5f, 26.2f, Unit.Centimetre);
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

                    // HallName (Top 2.1, VAlign=Middle, 0.6cm)
                    DrawText(layers, 2.1, 11.5, 0.7, 1.3825, 0.6 * PointsPerCm, data.HallNameSecond, vMiddle: true);
                    DrawText(layers, 2.1, 13.53753, 0.7, 1.3825, 0.6 * PointsPerCm, data.HallNameFirst, vMiddle: true);

                    // LivingNames 5 位（0.9cm，2026-07-18 客訴加大）— tmpText.rdlc；矩陣上排(Top15.2748)
                    // →下排(Top17.25916) 列距 1.98436cm。統一字級（整組同大小、最擠的塞得下才不重疊）；
                    // 上排僅當下方有名字才以列距為界。
                    var pt08l = NameBaseFontCm * PointsPerCm;
                    const double livingPitch = 17.25916 - 15.2748; // 1.98436
                    const double livingFull = 6.72806;
                    var lv = data.LivingNames;
                    // 第 6 位（lv[5]）補在下排右欄 L21.87382（主欄 lv[0] 正下方），使矩陣對稱（座標確認見 business-rules-implicit §18）。
                    var fl = VerticalText.GroupFontPt(pt08l,
                        (lv[0], VerticalText.Avail(lv[5], livingPitch, livingFull)),
                        (lv[1], VerticalText.Avail(lv[3], livingPitch, livingFull)),
                        (lv[2], VerticalText.Avail(lv[4], livingPitch, livingFull)),
                        (lv[3], livingFull), (lv[4], livingFull), (lv[5], livingFull));

                    DrawText(layers, 15.2748, 21.87382, 0.91251, livingFull, fl, lv[0], vertical: true);
                    DrawText(layers, 15.2748, 20.96131, 0.91251, livingFull, fl, lv[1], vertical: true);
                    DrawText(layers, 15.2748, 20.0488, 0.91251, livingFull, fl, lv[2], vertical: true);
                    DrawText(layers, 17.25916, 20.96131, 0.91251, livingFull, fl, lv[3], vertical: true);
                    DrawText(layers, 17.25916, 20.0488, 0.91251, livingFull, fl, lv[4], vertical: true);
                    DrawText(layers, 17.25916, 21.87382, 0.91251, livingFull, fl, lv[5], vertical: true); // Six（補：下排右欄，主欄正下方）

                    // DeadName（Rectangle2 群組，絕對座標 = Rect 原點 + 相對；0.9cm）。
                    // 往生／陽上**各自獨立**算安全字級（見 ComputeDeadFontPt 註解）——兩者共用同一個
                    // 0.9cm 起始基準，姓名不多時自然一樣大；只有當某一組自己排不下時才會各自縮小，
                    // 不會因為另一組縮小而連帶被拖小（見 docs/gotchas.md「往生字級被拖累」條）。
                    DrawDeadNames(layers, data, ComputeDeadFontPt(data));

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
                        const double pxToCm = 0.75 / SkiaImageHelpers.AddressColWidthPx; // 等比：27px ↔ 0.75cm
                        var bandW = (SkiaImageHelpers.AddressColumns(data.Address) == 1
                            ? SkiaImageHelpers.AddressColWidthPx
                            : SkiaImageHelpers.AddressColWidthPx * 2 + SkiaImageHelpers.AddressColGapPx) * pxToCm;
                        const double bandRight = 25.4 + 0.75; // 右緣固定（單欄時 Left = 25.4 不變）

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

    /// <summary>
    /// DeadName 群組「自己不重疊」的安全上限字級。與 LivingName 各自獨立計算（同一 0.9cm 基準），
    /// **不跨組對齊**：往生名字多、擠到需要縮小時，只縮往生自己，陽上不會被拖著一起縮小
    /// （曾經加過跨組取最小值對齊、已撤回，見 docs/gotchas.md）。
    /// </summary>
    private static double ComputeDeadFontPt(TextData data)
    {
        var pt08 = NameBaseFontCm * PointsPerCm;
        var d = data.DeadNames;
        if (data.Template == TextTemplate.Two)
        {
            // tmpTextTwo Rectangle2 origin (3.62361, 11.5) — 2 亡者皆高欄
            return VerticalText.GroupFontPt(pt08, (d[0], 10.50374), (d[1], 10.50374));
        }

        // tmpText Rectangle2 origin (3.65889, 11.5) — 5 格矩陣：上排 Two/Three(Top3.65889)
        // 到下排 Four/Five(Top5.72264) 列距 = 2.06375cm。統一字級（整組同大小，最擠的塞得下才不重疊）；
        // 次要格上排只在「正下方有名字」時才以列距為界，否則整欄高（不限）。
        const double pitch = 5.72264 - 3.65889; // 2.06375
        const double full = 10.50374;
        // 第 6 位（d[5]）補在下排正中央 L12.41251（主欄 d[0] 正下方），使矩陣對稱（座標確認見 business-rules-implicit §18）。
        // d[0] 之前下方為空可用整欄高；現 d[5] 在其正下方 → 改用列距為界（無第 6 位時 Avail 回整欄高＝向後相容）。
        return VerticalText.GroupFontPt(pt08,
            (d[0], VerticalText.Avail(d[5], pitch, full)),
            (d[1], VerticalText.Avail(d[3], pitch, full)),
            (d[2], VerticalText.Avail(d[4], pitch, full)),
            (d[3], full), (d[4], full), (d[5], full));
    }

    private static void DrawDeadNames(LayersDescriptor layers, TextData data, double fontPt)
    {
        var d = data.DeadNames;
        if (data.Template == TextTemplate.Two)
        {
            DrawText(layers, 3.65889, 13.01299, 0.91251, 10.50374, fontPt, d[0], vertical: true);
            DrawText(layers, 3.62361, 11.85, 0.91251, 10.50374, fontPt, d[1], vertical: true);
            return;
        }

        const double full = 10.50374;
        DrawText(layers, 3.65889, 12.41251, 0.91251, full, fontPt, d[0], vertical: true); // One（主欄）
        DrawText(layers, 3.65889, 13.32502, 0.91251, full, fontPt, d[1], vertical: true); // Two
        DrawText(layers, 3.65889, 11.5, 0.91251, full, fontPt, d[2], vertical: true);     // Three
        DrawText(layers, 5.72264, 13.32502, 0.91251, full, fontPt, d[3], vertical: true); // Four
        DrawText(layers, 5.72264, 11.5, 0.91251, full, fontPt, d[4], vertical: true);     // Five
        DrawText(layers, 5.72264, 12.41251, 0.91251, full, fontPt, d[5], vertical: true); // Six（補：下排中央，主欄正下方）
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
