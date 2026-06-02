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

    public byte[] Render(DataCardData data)
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

                    // 25 個 TextBox（座標來自 printing-reports-positions.md §1）
                    DrawText(layers, top: 0.538, left: 1.361, width: 6.204, height: 1.129, fontCm: 1.0, text: data.Number);
                    DrawText(layers, top: 0.776, left: 7.961, width: 3.241, height: 0.891, fontCm: 0.6, text: data.HallName);
                    DrawText(layers, top: 0.776, left: 12.133, width: 7.629, height: 0.891, fontCm: 0.7, text: data.Prepay);

                    DrawText(layers, top: 1.897, left: 1.714, width: 2.438, height: 0.918, fontCm: 0.8, text: "亡者：");
                    DrawText(layers, top: 1.897, left: 4.328, width: 7.236, height: 0.918, fontCm: 0.8, text: data.DeadNames[0]);
                    DrawText(layers, top: 1.897, left: 11.763, width: 7.259, height: 0.918, fontCm: 0.8, text: data.DeadNames[1]);
                    DrawText(layers, top: 2.814, left: 4.328, width: 7.236, height: 0.918, fontCm: 0.8, text: data.DeadNames[2]);
                    DrawText(layers, top: 2.814, left: 11.763, width: 3.596, height: 0.918, fontCm: 0.8, text: data.DeadNames[3]);
                    DrawText(layers, top: 2.814, left: 15.535, width: 3.638, height: 0.918, fontCm: 0.8, text: data.DeadNames[4]);

                    // Line2 — dashed 分隔線 (4.190, 4.328) 長 15.434
                    DrawLine(layers, top: 4.190, left: 4.328, width: 15.434, dashed: true);

                    DrawText(layers, top: 4.707, left: 1.714, width: 2.438, height: 0.918, fontCm: 0.8, text: "陽上：");
                    DrawText(layers, top: 4.707, left: 4.328, width: 7.236, height: 0.918, fontCm: 0.8, text: data.LivingNames[0]);
                    DrawText(layers, top: 4.707, left: 11.763, width: 7.259, height: 0.918, fontCm: 0.8, text: data.LivingNames[1]);
                    DrawText(layers, top: 5.660, left: 4.328, width: 7.236, height: 0.918, fontCm: 0.8, text: data.LivingNames[2]);
                    DrawText(layers, top: 5.730, left: 11.763, width: 3.596, height: 0.918, fontCm: 0.8, text: data.LivingNames[3]);
                    DrawText(layers, top: 5.730, left: 15.535, width: 3.638, height: 0.918, fontCm: 0.8, text: data.LivingNames[4]);

                    DrawText(layers, top: 6.753, left: 1.714, width: 2.438, height: 0.918, fontCm: 0.8, text: "地址：");
                    DrawText(layers, top: 6.753, left: 4.328, width: 15.434, height: 1.870, fontCm: 0.8, text: data.Address);

                    DrawText(layers, top: 8.799, left: 1.714, width: 2.438, height: 0.626, fontCm: 0.6, text: "電話：");
                    DrawText(layers, top: 8.799, left: 4.328, width: 15.434, height: 0.626, fontCm: 0.6, text: data.Phone);

                    DrawText(layers, top: 9.602, left: 1.714, width: 2.438, height: 0.918, fontCm: 0.6, text: "備註：");
                    DrawText(layers, top: 9.602, left: 4.328, width: 15.434, height: 3.421, fontCm: 0.6, text: data.Remark);

                    DrawText(layers, top: 13.182, left: 9.590, width: 6.548, height: 0.749, fontCm: 0.8, text: "確認無誤請簽名：", vAlign: VerticalAlign.Bottom);

                    // Line1 — solid 簽名底線
                    DrawLine(layers, top: 13.931, left: 16.125, width: 3.638, dashed: false);
                });
            });
        }).GeneratePdf();
    }

    private static void DrawText(
        LayersDescriptor layers,
        double top, double left, double width, double height,
        double fontCm,
        string? text,
        VerticalAlign vAlign = VerticalAlign.Top)
    {
        if (string.IsNullOrEmpty(text)) return;
        var fontPt = (float)(fontCm * PointsPerCm);

        // RDLC 的 Height 很貼齊字高；若直接用 .Height() 約束，QuestPDF 預設行高（>字級）會超出而被裁切 → 整段不顯示。
        // 改以 translate 位移模擬 VerticalAlign（不裁切），並把行高壓到 1.0 倍字級對齊單行套印。
        var y = vAlign switch
        {
            VerticalAlign.Middle => top + (height - fontCm) / 2.0,
            VerticalAlign.Bottom => top + (height - fontCm),
            _ => top,
        };

        layers.Layer()
            .TranslateX((float)left, Unit.Centimetre)
            .TranslateY((float)y, Unit.Centimetre)
            .Width((float)width, Unit.Centimetre)
            .Text(text).FontSize(fontPt).FontFamily(FontFamily).LineHeight(1f);
    }

    private static void DrawLine(LayersDescriptor layers, double top, double left, double width, bool dashed)
    {
        if (dashed)
        {
            // QuestPDF 2026 收回 SkiaSharp 公開 Canvas API → 虛線改以 SkiaSharp 產 PNG 嵌入。
            layers.Layer()
                .TranslateX((float)left, Unit.Centimetre)
                .TranslateY((float)top, Unit.Centimetre)
                .Width((float)width, Unit.Centimetre)
                .Image(SkiaImageHelpers.DashedLine(width)).FitWidth();
            return;
        }

        layers.Layer()
            .TranslateX((float)left, Unit.Centimetre)
            .TranslateY((float)top, Unit.Centimetre)
            .Width((float)width, Unit.Centimetre)
            .LineHorizontal(0.5f).LineColor(Colors.Black);
    }

    private enum VerticalAlign { Top, Middle, Bottom }
}
