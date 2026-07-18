using System.Reflection;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ceremony.Infrastructure.Reporting;

/// <summary>
/// 資料卡 PDF 渲染（QuestPDF 1:1 還原舊 tmpDataCard.rdlc）。
/// </summary>
/// <remarks>
/// Legacy: reference/old/Ceremony/tmpDataCard.rdlc (A5 橫，25 個 TextBox + 2 Lines)
/// Spec: docs/blueprints/printing-reports-positions.md §1 tmpDataCard.rdlc
/// Blueprint: docs/blueprints/printing-reports.md
///
/// 所有座標單位：公分（對應 RDLC TextBox Top/Left/Width/Height）。
/// FontSize 由 cm 轉 pt：1cm = 28.346 pt。
/// 字型固定 BiauKai / 標楷體（per 嚴格執行條款，禁止 fallback）。
/// </remarks>
public sealed class DataCardRenderer
{
    private const string FontFamily = "BiauKai";            // macOS 內建；Windows 為 "DFKai-SB"，需確認部署機字型
    private const double PointsPerCm = 28.3464567;
    private const double PageWidthCm = 21.0;
    private const double PageHeightCm = 14.8;

    // 開發用列印位置檢視工具的樣板照片（EmbeddedResource）；只在 debugOverlay:true 時載入使用，
    // 不進生產列印路徑。詳見 docs/blueprints/printing-reports.md「開發用列印位置檢視工具」。
    private static readonly byte[] TemplateImage = LoadTemplate("datacard-template.jpg");

    public byte[] Render(DataCardData data, bool debugOverlay = false)
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
                    // Primary layer: 整頁背景（white）
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

                    // 2026-07-18 客訴改版：資料卡改成連 template 一起印（欄位標題／簽名底線／
                    // 「故◯◯靈位」窗框），不再假設印在預印樣板紙上，白紙即可列印。
                    // 座標＝reference/template/資料卡.jpg 200 DPI 墨跡量測（與下方內容欄位同一座標系）。
                    DrawTemplate(layers);

                    // 2026-07-03 改版（見 docs/blueprints/printing-reports.md「資料卡改版」）：原 25-TextBox
                    // 版面是舊 tmpDataCard.rdlc 1:1 還原，但跟目前實際使用的樣板紙
                    // （reference/template/資料卡.jpg，200 DPI 量測）對不起來——樣板 Y=0~2.85cm 是空白、
                    // 沒有「亡者」欄也沒有堂號欄，樣板右側印有跟薦牌同款「故◯◯靈位」窗框圖案。改版方向
                    // （使用者確認）：Number 留左、預繳留右、堂號不印、亡者改印進右側樣板窗框裡（比照
                    // TabletRenderer 直書堆疊），陽上／地址／電話／備註／簽名沿用樣板量到的座標。
                    // 2026-07-18 客訴：編號往下 0.5cm（0.538 → 1.038）
                    DrawText(layers, top: 1.038, left: 1.361, width: 6.204, height: 1.129, fontCm: 1.0, text: data.Number);
                    // 預繳欄沿用原寬度：樣板窗框（Top=4.40cm 起）在這一列（Top 0.776~1.667cm）之下，不會重疊
                    DrawText(layers, top: 0.776, left: 12.133, width: 7.629, height: 0.891, fontCm: 0.7, text: data.Prepay);

                    DrawDeadNamesInWindow(layers, data.DeadNames);

                    // 2026-07-03 起欄位標題（陽上／地址／電話／備註／簽名）由 DrawTemplate 統一繪製
                    // （2026-07-18 前假設樣板紙預印、程式不印標題；現改為程式全印），內容欄位仍
                    // 對齊標題右側原本保留的空白處（Left 座標不變）。
                    //
                    // 2026-07-04 再調整（使用者指定版面）：
                    // 陽上改 3 排×2 欄（原本 2 排較擠、寬度太寬）：LivingNames[0] 第一排；[1]/[3] 第二排前/後；
                    // [2]/[4] 第三排前/後。欄寬只留 6 字寬（0.8cm × 6 = 4.8cm，剛好夠不用更寬）；後欄
                    // Left=9.986、寬 4.8 結束於 14.786，跟右側樣板窗框（Left=14.986 起）留 0.2cm 不重疊。
                    DrawText(layers, top: 2.690, left: 4.328, width: 4.8, height: 0.918, fontCm: 0.8, text: data.LivingNames[0]);
                    DrawText(layers, top: 3.643, left: 4.328, width: 4.8, height: 0.918, fontCm: 0.8, text: data.LivingNames[1]);
                    DrawText(layers, top: 4.596, left: 4.328, width: 4.8, height: 0.918, fontCm: 0.8, text: data.LivingNames[2]);
                    DrawText(layers, top: 3.643, left: 9.986, width: 4.8, height: 0.918, fontCm: 0.8, text: data.LivingNames[3]);
                    DrawText(layers, top: 4.596, left: 9.986, width: 4.8, height: 0.918, fontCm: 0.8, text: data.LivingNames[4]);

                    // 2026-07-05：地址／電話／備註的上下對齊方式改參照陽上——直接對齊樣板量到的標題文字
                    // 「上緣」（同 陽上「陽上：」對齊 Top=2.6924 的做法），取代前一版用位移量（±1cm/±0.5cm）
                    // 推算的座標。樣板量測標題上緣：地址 Top=6.4135、電話 Top=8.8392、備註 Top=9.8679。
                    // 地址／備註寬度縮到 10.4（4.328 起算，結束於 14.728，避開右側窗框 Left=14.986），
                    // 不設 .Height() 故文字過長會自動換到下一行，不會被裁掉。
                    DrawText(layers, top: 6.4135, left: 4.328, width: 10.4, height: 1.870, fontCm: 0.8, text: data.Address);

                    DrawText(layers, top: 8.8392, left: 4.328, width: 15.434, height: 0.626, fontCm: 0.6, text: data.Phone);

                    DrawText(layers, top: 9.8679, left: 4.328, width: 10.4, height: 3.421, fontCm: 0.6, text: data.Remark);

                    // 「確認無誤請簽名：」標題與簽名底線由 DrawTemplate 繪製
                });
            });
        }).GeneratePdf();
    }

    /// <summary>
    /// 把樣板本身印出來（2026-07-18 客訴：資料卡要連 template 都印，白紙即可列印）：
    /// 欄位標題（陽上／地址／電話／備註／確認無誤請簽名）＋簽名底線＋右側「故◯◯靈位」窗框。
    /// 所有座標＝reference/template/資料卡.jpg（200 DPI 掃描）墨跡量測值，
    /// 與既有內容欄位共用同一座標系，故內容欄位座標完全不動。
    /// </summary>
    private static void DrawTemplate(LayersDescriptor layers)
    {
        // 欄位標題：Top 沿用內容欄位既有的「標題上緣」對齊值，Left/字級為樣板墨跡量測
        DrawText(layers, top: 2.690, left: 2.121, width: 3.0, height: 0.7, fontCm: 0.7, text: "陽上：");
        DrawText(layers, top: 6.4135, left: 2.096, width: 3.0, height: 0.7, fontCm: 0.7, text: "地址：");
        DrawText(layers, top: 8.8392, left: 2.134, width: 3.0, height: 0.6, fontCm: 0.6, text: "電話：");
        DrawText(layers, top: 9.8679, left: 2.083, width: 3.0, height: 0.6, fontCm: 0.6, text: "備註：");
        DrawText(layers, top: 13.208, left: 2.057, width: 7.0, height: 0.75, fontCm: 0.75, text: "確認無誤請簽名：");

        // 簽名底線（樣板量測 x 8.23~12.29、y≈13.87、線寬≈0.026cm）
        layers.Layer()
            .TranslateX(8.23f, Unit.Centimetre)
            .TranslateY(13.855f, Unit.Centimetre)
            .Width(4.06f, Unit.Centimetre)
            .LineHorizontal(0.8f);

        // 窗框矩形（樣板外緣量測 x 14.973~17.983、y 4.394~14.046）
        layers.Layer()
            .TranslateX(14.973f, Unit.Centimetre)
            .TranslateY(4.394f, Unit.Centimetre)
            .Width(3.010f, Unit.Centimetre)
            .Height(9.652f, Unit.Centimetre)
            .Border(0.8f);

        // 「故」與「靈位」：置中於窗框中軸 16.478；墨跡對齊樣板量測（「故」下緣 5.6388、
        // 「靈」上緣 11.4427——兩者是 DrawDeadNamesInWindow 亡者矩陣的硬邊界，不可越過）
        const double frameCenterX = (14.973 + 17.983) / 2; // 16.478
        const double glyphFontCm = 1.10;                   // 樣板墨跡量測字級（位 字寬 ≈1.12cm）
        var glyphFontPt = glyphFontCm * PointsPerCm;
        DrawVerticalName(layers, top: 4.585, left: frameCenterX - glyphFontCm / 2, fontPt: glyphFontPt, text: "故");
        // LineHeight 1.13：讓「靈」上緣落在 11.4427、「位」下緣落在 13.7414（皆為樣板量測值）
        layers.Layer()
            .TranslateX((float)(frameCenterX - glyphFontCm / 2), Unit.Centimetre)
            .TranslateY(11.33f, Unit.Centimetre)
            .Text(VerticalText.Stack("靈位")).FontSize((float)glyphFontPt).FontFamily(FontFamily).LineHeight(1.13f);
    }

    // 亡者姓名印進樣板右側「故◯◯靈位」窗框圖案的空白處（窗框由 DrawTemplate 繪製）。
    // 窗框座標（reference/template/資料卡.jpg 200 DPI 像素量測）：內緣 Left=14.986~17.9705cm；
    // 「故」字下緣 Y=5.6388cm、「靈位」上緣 Y=11.4427cm。
    // 2026-07-05 改版（使用者指定）：改成跟 TabletRenderer.DrawDeadNames default 分支同一套 2×3
    // 矩陣排法（不再是單欄「、」串接）：1st 中間上、2nd 右邊上、3rd 左邊上、4th 右邊下、5th 左邊下、
    // 6th 中間下；同時整體再往下 0.1cm、往左 0.1cm。
    private static void DrawDeadNamesInWindow(LayersDescriptor layers, string?[] deadNames)
    {
        const double topRowY = 5.6388 + 0.1;        // 5.7388
        const double windowGapBottom = 11.4427;     // 「靈」字上緣，硬邊界
        const double rowPitch = 2.6;
        const double bottomRowY = topRowY + rowPitch; // 8.3388
        const double safetyMargin = 0.2;
        // 下排／單獨欄可用高（到「靈」字上緣扣安全邊界）；上排跟下排共用同一個保守值（比照 TabletRenderer
        // 的 deadFull 對上下排一視同仁的簡化做法）
        const double fullHeight = windowGapBottom - bottomRowY - safetyMargin; // 2.9039

        const double centerX = 16.285 - 0.1; // 16.185（前一版單欄置中值再往左 0.1cm）
        const double columnGap = 0.75;
        const double leftX = centerX - columnGap;
        const double rightX = centerX + columnGap;

        // 窗框內緣只有 2.9845cm 寬，塞 3 欄字級不能沿用 0.8cm（1 字時會撐到 columnGap 都不夠、3 欄互相
        // 貼在一起看起來像連在一起）；改用 0.6cm 上限，GroupFontPt 只會縮不會放大，欄距才留得出空隙。
        const double baseFontCm = 0.6;
        var d = deadNames;

        var fontPt = VerticalText.GroupFontPt(baseFontCm * PointsPerCm,
            (d[0], VerticalText.Avail(d[5], rowPitch, fullHeight)),
            (d[1], VerticalText.Avail(d[3], rowPitch, fullHeight)),
            (d[2], VerticalText.Avail(d[4], rowPitch, fullHeight)),
            (d[3], fullHeight),
            (d[4], fullHeight),
            (d[5], fullHeight));

        DrawVerticalName(layers, topRowY, centerX, fontPt, d[0]);    // 1st 中間上
        DrawVerticalName(layers, topRowY, rightX, fontPt, d[1]);     // 2nd 右邊上
        DrawVerticalName(layers, topRowY, leftX, fontPt, d[2]);      // 3rd 左邊上
        DrawVerticalName(layers, bottomRowY, rightX, fontPt, d[3]);  // 4th 右邊下
        DrawVerticalName(layers, bottomRowY, leftX, fontPt, d[4]);   // 5th 左邊下
        DrawVerticalName(layers, bottomRowY, centerX, fontPt, d[5]); // 6th 中間下
    }

    private static void DrawVerticalName(LayersDescriptor layers, double top, double left, double fontPt, string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        // 直書刻意不設 .Width()（見 VerticalText 頂部注解：窄欄自動換行會被 QuestPDF 靜默丟字）
        layers.Layer()
            .TranslateX((float)left, Unit.Centimetre)
            .TranslateY((float)top, Unit.Centimetre)
            .Text(VerticalText.Stack(text)).FontSize((float)fontPt).FontFamily(FontFamily).LineHeight(1f);
    }

    private static void DrawText(
        LayersDescriptor layers,
        double top, double left, double width, double height,
        double fontCm,
        string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        _ = height; // 保留參數只為了讓每個呼叫點對照 printing-reports-positions.md 的 RDLC Height 值，排版不需要
        var fontPt = (float)(fontCm * PointsPerCm);

        // RDLC 的 Height 很貼齊字高；若直接用 .Height() 約束，QuestPDF 預設行高（>字級）會超出而被裁切 → 整段不顯示。
        // 改以 translate 位移（不裁切），並把行高壓到 1.0 倍字級對齊單行套印。
        layers.Layer()
            .TranslateX((float)left, Unit.Centimetre)
            .TranslateY((float)top, Unit.Centimetre)
            .Width((float)width, Unit.Centimetre)
            .Text(text).FontSize(fontPt).FontFamily(FontFamily).LineHeight(1f);
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
