using System.Reflection;
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
    private const double PageWidthCm = 21.0;
    private const double PageHeightCm = 29.7;

    // 開發用列印位置檢視工具的樣板照片（EmbeddedResource，來源 reference/template/收據.jpg）；
    // 只在 debugOverlay:true 時載入使用，不進生產列印路徑。樣板僅涵蓋收據本體（上下聯），
    // 郵寄封面頁無樣板故不疊。詳見 docs/blueprints/printing-reports.md「開發用列印位置檢視工具」。
    private static readonly byte[] TemplateImage = LoadTemplate("receipt-template.jpg");

    public byte[] Render(ReceiptData data, bool debugOverlay = false)
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

                    if (debugOverlay)
                    {
                        // 疊樣板照片供對位（拉伸填滿＝座標系統比對用途，同 TextRenderer/TabletRenderer）。
                        layers.Layer()
                            .TranslateX(0, Unit.Centimetre)
                            .TranslateY(0, Unit.Centimetre)
                            .Width((float)PageWidthCm, Unit.Centimetre)
                            .Height((float)PageHeightCm, Unit.Centimetre)
                            .Image(TemplateImage).FitUnproportionally();
                    }

                    // 2026-07-18 客戶樣張校正（reference/收據.jpg 手寫註記）：偏離 RDLC 原始座標——
                    // Name +0.2、Number +0.8/右+1.0、Prepay +0.3、年月日 +0.5（cm，上下聯同步套用）。
                    // 2026-07-21 客訴續調（overlay 對位後定案，見 reference/output/receipt_overlay.pdf）：
                    // (1) Number 左移 0.5（Left 16.00→15.50，距預印「郵」字 0.5cm，上下聯同步）。
                    // (2) 本體 Name 下移 0.2（上聯 Top 2.50→2.70、下聯 12.30→12.50），原名字浮在預印「大德贊助法會」上方，
                    //     下移對齊「大德」列。客戶原稱「封面」實指收據聯本體；第 2 頁郵寄封面無「大德」故 Name 維持 5.44111。
                    // (3) 本體 Fee 對齊左右預印「新台幣…元整」列，與 Number 同列。金額與編號本在同列；07-18 只把 Number
                    //     下移到該列、Fee 落單，此次補齊；再依客戶回饋整列上移 0.2 → Fee/Number 上聯 Top 4.10、下聯 14.20。

                    // 上聯（收據聯）
                    DrawText(layers, 2.70, 6.73, 8.257, 0.726, 0.6 * PointsPerCm, data.Name);
                    DrawText(layers, 4.10, 5.00, 2.50, 0.653, 14, data.Fee);
                    DrawText(layers, 4.10, 15.50, 2.50, 0.653, 14, data.Number, bold: true);
                    DrawText(layers, 5.00, 11.50, 6.00, 0.653, 14, data.Prepay);
                    DrawText(layers, 8.10, 8.00, 2.50, 0.653, 14, data.Year);
                    DrawText(layers, 8.10, 11.50, 2.50, 0.653, 14, data.Month);
                    DrawText(layers, 8.10, 15.00, 2.50, 0.653, 14, data.Day);

                    // 下聯（存根聯）
                    DrawText(layers, 12.50, 6.73, 8.257, 0.753, 0.6 * PointsPerCm, data.Name);
                    DrawText(layers, 14.20, 5.00, 2.50, 0.653, 14, data.Fee);
                    DrawText(layers, 14.20, 15.50, 2.50, 0.653, 14, data.Number, bold: true);
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
