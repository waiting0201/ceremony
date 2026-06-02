using SkiaSharp;

namespace Ceremony.Infrastructure.Reporting;

/// <summary>
/// SkiaSharp 逐像素繪圖 helper — 供列印報表需要影像（而非 QuestPDF 流式排版）的場景。
/// </summary>
/// <remarks>
/// 兩個用途：
/// (1) <see cref="VerticalAddress"/> — 1:1 移植舊系統 reference/old/Ceremony/Commons/Library.cs:34-124
///     （原 System.Drawing，非跨平台）；文牒 PhotoAddress 垂直地址 25×605px 透明 PNG。
/// (2) <see cref="DashedLine"/> — DataCard Line2 虛線；QuestPDF 2026 已收回 SkiaSharp 公開 Canvas API，
///     故改以小張虛線 PNG 嵌入。
///
/// 字型：標楷體（BiauKai）。dev macOS 內建；**部署機（Windows / Electron sidecar）必須 bundle
/// TW-Kai / DFKai-SB**，否則直書字會 fallback（見 docs/gotchas.md）。
/// </remarks>
internal static class SkiaImageHelpers
{
    private const string FontFamily = "BiauKai";

    // 標楷體 typeface（快取一次）。注意：SkiaSharp 的 SKTypeface.FromFamilyName 與 QuestPDF 的
    // FontManager 是**兩條獨立**的字型解析路徑——ReportFonts 把字型註冊進 QuestPDF 並不會讓
    // Skia 的 FromFamilyName("BiauKai") 找得到（macOS 家族名其實是「標楷體-繁」）。
    // 若只靠 FromFamilyName，會 fallback 到無中文字符的 Default → 文牒地址整排變成 tofu 方框。
    // 因此這裡直接用 ReportFonts 解析到的字型「檔案路徑」載入，與 QuestPDF 用同一個字型檔。
    private static readonly SKTypeface KaiTypeface = LoadKaiTypeface();

    private static SKTypeface LoadKaiTypeface()
    {
        ReportFonts.EnsureRegistered();
        var path = ReportFonts.ResolvedPath;
        if (!string.IsNullOrEmpty(path))
        {
            var tf = SKTypeface.FromFile(path);
            if (tf is not null) return tf;
        }
        return SKTypeface.FromFamilyName(FontFamily) ?? SKTypeface.Default;
    }

    /// <summary>
    /// 產垂直地址 PNG（25×605px 透明）。中文直排；英數 / dash / 括號旋轉 90°。
    /// 對齊 Library.cs：判定 <c>^[a-zA-Z0-9\-\(\)]$</c> 旋轉、其餘直排；逐字往下堆疊。
    /// </summary>
    public static byte[] VerticalAddress(string? text)
    {
        const int width = 25;
        const int height = 605;
        const float fontSize = 25f;
        text ??= string.Empty;

        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        using var font = new SKFont(KaiTypeface, fontSize);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };

        var metrics = font.Metrics;
        var glyphHeight = metrics.Descent - metrics.Ascent;     // = font 行高（≈ 字級）
        var midOffset = -(metrics.Ascent + metrics.Descent) / 2f; // baseline 置中修正

        // 逐字往下堆疊的「每字佔高」= 行高。舊 Library.cs 用 GDI+ MeasureString.Height − 9，
        // 但 GDI+ MeasureString.Height 比字級膨脹很多（含 line gap/padding，≈1.4–1.5× em），
        // 減 9 後仍 > 字級 → 不重疊。SkiaSharp 的 (Descent−Ascent) 已是緊湊行高（≈25.6px），
        // 若再照搬「−9」會變 16.6px < 字面 23px → 字會疊在一起（黏住）。故直接用行高當步進。
        var step = glyphHeight;

        float y = 0f;
        foreach (var c in text)
        {
            if (y > height) break;
            var s = c.ToString();

            if (IsRotatedChar(c))
            {
                canvas.Save();
                canvas.Translate(width / 2f, y + glyphHeight / 2f);
                canvas.RotateDegrees(90);
                canvas.DrawText(s, 0f, midOffset, SKTextAlign.Center, font, paint);
                canvas.Restore();
                y += step;
            }
            else
            {
                canvas.DrawText(s, width / 2f, y - metrics.Ascent, SKTextAlign.Center, font, paint);
                y += step;
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// 產水平虛線 PNG。寬度給 cm，內部換算為像素（預設 96 dpi）。
    /// </summary>
    public static byte[] DashedLine(double widthCm, double dpi = 96.0)
    {
        var px = Math.Max(1, (int)Math.Round(widthCm / 2.54 * dpi));
        const int h = 3;

        using var bitmap = new SKBitmap(px, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.2f,
            PathEffect = SKPathEffect.CreateDash([4f, 3f], 0f),
        };
        canvas.DrawLine(0f, h / 2f, px, h / 2f, paint);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static bool IsRotatedChar(char c)
        => (c is >= 'a' and <= 'z')
           || (c is >= 'A' and <= 'Z')
           || (c is >= '0' and <= '9')
           || c is '-' or '(' or ')';
}
