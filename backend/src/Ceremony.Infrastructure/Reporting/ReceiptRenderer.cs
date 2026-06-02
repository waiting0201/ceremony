using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ceremony.Infrastructure.Reporting;

/// <summary>
/// 雙聯收據 PDF — 對齊 tmpReceipt.rdlc。
/// </summary>
/// <remarks>
/// 頁面 A4 直 21×29.7cm；上聯 + 下聯各佔半，欄位 Top 差 9.8~10cm。
/// 字級：14pt 主資訊；16pt 郵寄標籤；0.6cm Name。
/// 對齊 docs/blueprints/printing-reports-positions.md §2。
/// </remarks>
public sealed class ReceiptRenderer
{
    private const string FontFamily = "BiauKai";
    private const double PointsPerCm = 28.3464567;

    public byte[] Render(ReceiptData data)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(21f, 29.7f, Unit.Centimetre);
                page.Margin(0);
                page.DefaultTextStyle(t => t.FontFamily(FontFamily).FontColor(Colors.Black));

                page.Content().Layers(layers =>
                {
                    layers.PrimaryLayer().Background("#FFFFFF");

                    // 上聯（收據聯）
                    DrawText(layers, 2.30, 6.73, 8.257, 0.726, 0.6 * PointsPerCm, data.Name);
                    DrawText(layers, 3.50, 5.00, 2.50, 0.653, 14, data.Fee);
                    DrawText(layers, 3.50, 15.00, 2.50, 0.653, 14, data.Number, bold: true);
                    DrawText(layers, 4.70, 11.50, 6.00, 0.653, 14, data.Prepay);
                    DrawText(layers, 7.60, 8.00, 2.50, 0.653, 14, data.Year);
                    DrawText(layers, 7.60, 11.50, 2.50, 0.653, 14, data.Month);
                    DrawText(layers, 7.60, 15.00, 2.50, 0.653, 14, data.Day);

                    // 下聯（存根聯）
                    DrawText(layers, 12.10, 6.73, 8.257, 0.753, 0.6 * PointsPerCm, data.Name);
                    DrawText(layers, 13.60, 5.00, 2.50, 0.653, 14, data.Fee);
                    DrawText(layers, 13.60, 15.00, 2.50, 0.653, 14, data.Number, bold: true);
                    DrawText(layers, 14.50, 11.50, 6.00, 0.653, 14, data.Prepay);
                    DrawText(layers, 17.50, 8.00, 2.50, 0.653, 14, data.Year);
                    DrawText(layers, 17.50, 11.50, 2.50, 0.653, 14, data.Month);
                    DrawText(layers, 17.50, 15.00, 2.50, 0.653, 14, data.Day);
                });
            });
        }).GeneratePdf();
    }

    private static void DrawText(LayersDescriptor layers, double top, double left, double width, double height, double fontPt, string? text, bool bold = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        // 不用 .Height() 約束（RDLC 高度貼齊字高，QuestPDF 預設行高會超出被裁切）；行高壓 1.0 倍。
        _ = height;
        var span = layers.Layer()
            .TranslateX((float)left, Unit.Centimetre)
            .TranslateY((float)top, Unit.Centimetre)
            .Width((float)width, Unit.Centimetre)
            .Text(text)
            .FontSize((float)fontPt)
            .FontFamily(FontFamily)
            .LineHeight(1f);
        if (bold) span.Bold();
    }
}

public sealed record ReceiptData(
    string Name,
    string Fee,
    string Number,
    string Prepay,
    string Year,
    string Month,
    string Day);
