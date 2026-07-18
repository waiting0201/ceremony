using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ceremony.Infrastructure.Reporting;

/// <summary>
/// 雙聯收據 PDF — 對齊 tmpReceipt.rdlc。
/// </summary>
/// <remarks>
/// 頁面 A4 直 21×29.7cm；每筆固定兩頁（RDLC Tablix 高 59.4cm）：
/// 第 1 頁上聯 + 下聯（欄位 Top 差 9.8~10cm）、第 2 頁郵寄封面（Zipcode / Address / Name）。
/// 封面就算地址空白也照樣輸出，維持與舊系統相同的頁數與送紙順序。
/// 字級：14pt 主資訊；16pt 郵寄封面；0.6cm Name。
/// 對齊 docs/blueprints/printing-reports-positions.md §2；
/// 2026-07-18 依客戶樣張（reference/收據.jpg）校正第 1 頁座標，偏離 RDLC 原始值，見 §2 改版覆蓋註記。
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

                    // 2026-07-18 客戶樣張校正（reference/收據.jpg 手寫註記）：偏離 RDLC 原始座標——
                    // Name +0.2、Number +0.8/右+1.0、Prepay +0.3、年月日 +0.5（cm，上下聯同步套用）。

                    // 上聯（收據聯）
                    DrawText(layers, 2.50, 6.73, 8.257, 0.726, 0.6 * PointsPerCm, data.Name);
                    DrawText(layers, 3.50, 5.00, 2.50, 0.653, 14, data.Fee);
                    DrawText(layers, 4.30, 16.00, 2.50, 0.653, 14, data.Number, bold: true);
                    DrawText(layers, 5.00, 11.50, 6.00, 0.653, 14, data.Prepay);
                    DrawText(layers, 8.10, 8.00, 2.50, 0.653, 14, data.Year);
                    DrawText(layers, 8.10, 11.50, 2.50, 0.653, 14, data.Month);
                    DrawText(layers, 8.10, 15.00, 2.50, 0.653, 14, data.Day);

                    // 下聯（存根聯）
                    DrawText(layers, 12.30, 6.73, 8.257, 0.753, 0.6 * PointsPerCm, data.Name);
                    DrawText(layers, 13.60, 5.00, 2.50, 0.653, 14, data.Fee);
                    DrawText(layers, 14.40, 16.00, 2.50, 0.653, 14, data.Number, bold: true);
                    DrawText(layers, 14.80, 11.50, 6.00, 0.653, 14, data.Prepay);
                    DrawText(layers, 18.00, 8.00, 2.50, 0.653, 14, data.Year);
                    DrawText(layers, 18.00, 11.50, 2.50, 0.653, 14, data.Month);
                    DrawText(layers, 18.00, 15.00, 2.50, 0.653, 14, data.Day);
                });
            });

            // 第 2 頁：郵寄封面（RDLC Textbox22-24，Top 為原始值 − 29.7cm）
            container.Page(page =>
            {
                page.Size(21f, 29.7f, Unit.Centimetre);
                page.Margin(0);
                page.DefaultTextStyle(t => t.FontFamily(FontFamily).FontColor(Colors.Black));

                page.Content().Layers(layers =>
                {
                    layers.PrimaryLayer().Background("#FFFFFF");

                    DrawText(layers, 3.90, 4.75646, 2.50, 0.70, 16, data.Zipcode);
                    DrawText(layers, 4.67056, 4.75646, 10.67562, 0.70, 16, data.Address);
                    DrawText(layers, 5.44111, 4.75646, 9.24354, 0.70, 16, data.Name);
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
    string Zipcode,
    string Address,
    string Fee,
    string Number,
    string Prepay,
    string Year,
    string Month,
    string Day);
