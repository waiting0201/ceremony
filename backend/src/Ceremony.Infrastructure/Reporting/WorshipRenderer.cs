using System.Reflection;
using Ceremony.Domain.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ceremony.Infrastructure.Reporting;

/// <summary>
/// 普桌 PDF — 對齊 tmpWorship*.rdlc 6 變體。
/// </summary>
/// <remarks>
/// 頁面 21×29.6cm A4 直；含 worship2 背景紋飾圖（6 變體共用同一張，見 positions §EmbeddedImage）。
/// 6 變體依 LivingName 最高位數選擇；Number 2cm Bold；LivingName 字級 2cm（base）或 3cm（One/Two/Three）。
/// 對齊 docs/blueprints/printing-reports-positions.md §14-19。
/// </remarks>
public sealed class WorshipRenderer
{
    private const string FontFamily = "BiauKai";
    private const double PointsPerCm = 28.3464567;

    // worship2.png 背景圖（EmbeddedResource）只載一次；座標精確到小數 5 位（positions §14，不可四捨五入）。
    private static readonly byte[] BackgroundImage = LoadBackground();

    public byte[] Render(WorshipData data)
    {
        var livingFontPt = data.Template switch
        {
            WorshipTemplate.One or WorshipTemplate.Two or WorshipTemplate.Three => 3.0 * PointsPerCm,
            _ => 2.0 * PointsPerCm,
        };

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(21f, 29.6f, Unit.Centimetre);
                page.Margin(0);
                page.DefaultTextStyle(t => t.FontFamily(FontFamily).FontColor(Colors.Black));

                page.Content().Layers(layers =>
                {
                    layers.PrimaryLayer().Background("#FFFFFF");

                    // worship2 背景紋飾（文字層之下）：Top 0.26141, Left 0.42, W 20.04729, H 28.88438, FitProportional
                    layers.Layer()
                        .TranslateX(0.42f, Unit.Centimetre)
                        .TranslateY(0.26141f, Unit.Centimetre)
                        .Width(20.04729f, Unit.Centimetre)
                        .Height(28.88438f, Unit.Centimetre)
                        .Image(BackgroundImage).FitArea();

                    // Number 2cm Bold Center (Top 4.474, Left 5.5875)
                    DrawText(layers, 4.474, 5.5875, 8.903, 2.206, 2.0 * PointsPerCm, data.Number, bold: true, hCenter: true);

                    // LivingNames base 6-position 2×3 matrix
                    // Row 1 (Top 7.31167): col3 col2 col1 — Three Two One (right to left)
                    DrawText(layers, 7.31167, 11.0925, 2.2, 10.21125, livingFontPt, data.LivingNames[0]);   // One
                    DrawText(layers, 7.31167, 8.82834, 2.2, 10.21125, livingFontPt, data.LivingNames[1]);   // Two
                    DrawText(layers, 7.31167, 6.62834, 2.2, 10.21125, livingFontPt, data.LivingNames[2]);   // Three
                    // Row 2 (Top 17.69931): col3 col2 col1 — Six Five Four (right to left)
                    DrawText(layers, 17.69931, 11.07715, 2.2, 10.21125, livingFontPt, data.LivingNames[3]); // Four
                    DrawText(layers, 17.69931, 8.86362, 2.2, 10.21125, livingFontPt, data.LivingNames[4]);  // Five
                    DrawText(layers, 17.69931, 6.62834, 2.2, 10.21125, livingFontPt, data.LivingNames[5]);  // Six
                });
            });
        }).GeneratePdf();
    }

    private static byte[] LoadBackground()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames().Single(n => n.EndsWith("worship2.png", StringComparison.Ordinal));
        using var stream = asm.GetManifestResourceStream(name)!;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static void DrawText(LayersDescriptor layers, double top, double left, double width, double height, double fontPt, string? text, bool bold = false, bool hCenter = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        // 不用 .Height() 約束（避免 QuestPDF 行高超出被裁切，例如 Number 2cm 在 2.206cm 框）；行高壓 1.0 倍。
        _ = height;
        var box = layers.Layer()
            .TranslateX((float)left, Unit.Centimetre)
            .TranslateY((float)top, Unit.Centimetre)
            .Width((float)width, Unit.Centimetre);
        var aligned = hCenter ? box.AlignCenter() : box;
        var span = aligned.Text(text).FontSize((float)fontPt).FontFamily(FontFamily).LineHeight(1f);
        if (bold) span.Bold();
    }
}

public sealed record WorshipData(
    string Number,
    string?[] LivingNames,    // 6 元素
    WorshipTemplate Template);
