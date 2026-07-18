using SkiaSharp;

namespace Ceremony.Infrastructure.Reporting;

/// <summary>
/// SkiaSharp 逐像素繪圖 helper — 供列印報表需要影像（而非 QuestPDF 流式排版）的場景。
/// </summary>
/// <remarks>
/// 兩個用途：
/// (1) <see cref="VerticalAddress"/> — 1:1 移植舊系統 reference/old/Ceremony/Commons/Library.cs:34-124
///     （原 System.Drawing，非跨平台）；文牒 PhotoAddress 垂直地址 27×653px 透明 PNG。
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

    // 文牒垂直地址 canvas 規格。欄寬 = 字級；欄距 9px（0.25cm）在兩欄折行時使用。
    // 與文牒嵌入帶等比：27px ↔ 0.75cm（見 TextRenderer PhotoAddress 段）。
    internal const int AddressColWidthPx = 27;
    internal const int AddressColGapPx = 9;
    internal const int AddressColHeightPx = 653;
    private const float AddressFontSize = 27f;

    /// <summary>單欄可容納字數（步進 = 行高 ≈ 1.02 字級 → 23 字）。TextRenderer 判斷帶寬也用它。</summary>
    internal static int AddressCharsPerColumn
    {
        get
        {
            using var font = new SKFont(KaiTypeface, AddressFontSize);
            var m = font.Metrics;
            return Math.Max(1, (int)(AddressColHeightPx / (m.Descent - m.Ascent)));
        }
    }

    /// <summary>超過單欄容量折兩欄（2026-07-18 使用者指定「太長的到左邊二行」）；再長仍兩欄（尾端裁切，46+ 字才會發生）。</summary>
    internal static int AddressColumns(string? text)
        => string.IsNullOrEmpty(text) || text.Length <= AddressCharsPerColumn ? 1 : 2;

    /// <summary>
    /// 產垂直地址 PNG（透明底）。中文直排；英數 / dash / 括號旋轉 90°。
    /// 對齊 Library.cs：判定 <c>^[a-zA-Z0-9\-\(\)]$</c> 旋轉、其餘直排；逐字往下堆疊。
    /// 2026-07-18 客訴「文牒地址字要加大」：canvas 25×605/字級 25 → 27×653/字級 27（整張等比
    /// ×27/25）——與文牒嵌入帶等比，FitArea 後每字約 0.75cm（原 0.66cm）；
    /// 高度跟著字級放大，可容納字數維持 ~23（曾只放大字級不放大高度 → 24 字長地址尾端被裁）。
    /// 同日追加兩欄折行（使用者指定）：超過單欄容量時平均拆兩欄（多的字給先讀的右欄），直書閱讀
    /// 順序右欄→左欄，canvas 寬變 2 欄＋欄距（63px），由 <see cref="AddressColumns"/> 告知
    /// TextRenderer 同步加寬嵌入帶、往左擴（右欄固定在預印「臺灣」正下方）。
    /// </summary>
    public static byte[] VerticalAddress(string? text)
    {
        text ??= string.Empty;

        var columns = AddressColumns(text);
        var width = columns == 1 ? AddressColWidthPx : AddressColWidthPx * 2 + AddressColGapPx;
        var height = AddressColHeightPx;

        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        // Edging=Alias（不抗鋸齒）：抗鋸齒的邊緣半透明像素在這種窄欄（27px 寬）小字級下佔比高，
        // 疊加印表機網點後視覺上明顯偏灰，客戶反映「地址字要再黑一點」。改無鋸齒後每個像素非黑即透明，
        // 列印出來才是實黑（同 DashedLine 既有的 IsAntialias=false 選擇）。
        using var font = new SKFont(KaiTypeface, AddressFontSize) { Edging = SKFontEdging.Alias };
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };

        var metrics = font.Metrics;
        var glyphHeight = metrics.Descent - metrics.Ascent;     // = font 行高（≈ 字級）
        var midOffset = -(metrics.Ascent + metrics.Descent) / 2f; // baseline 置中修正

        // 逐字往下堆疊的「每字佔高」= 行高。舊 Library.cs 用 GDI+ MeasureString.Height − 9，
        // 但 GDI+ MeasureString.Height 比字級膨脹很多（含 line gap/padding，≈1.4–1.5× em），
        // 減 9 後仍 > 字級 → 不重疊。SkiaSharp 的 (Descent−Ascent) 已是緊湊行高（≈25.6px），
        // 若再照搬「−9」會變 16.6px < 字面 23px → 字會疊在一起（黏住）。故直接用行高當步進。
        var step = glyphHeight;

        // 平均拆欄（單欄時全給右欄）：右欄先讀、多的字給右欄，兩欄頂端對齊。
        var rightCount = columns == 1 ? text.Length : (text.Length + 1) / 2;

        for (var i = 0; i < text.Length; i++)
        {
            var inRight = i < rightCount;
            var colCenterX = inRight ? width - AddressColWidthPx / 2f : AddressColWidthPx / 2f;
            var y = (inRight ? i : i - rightCount) * step;
            if (y > height) continue; // 超出欄高的字略過（46+ 字才會發生），不可 break——右欄溢出時左欄還要畫
            var c = text[i];
            var s = c.ToString();

            if (IsRotatedChar(c))
            {
                canvas.Save();
                canvas.Translate(colCenterX, y + glyphHeight / 2f);
                canvas.RotateDegrees(90);
                canvas.DrawText(s, 0f, midOffset, SKTextAlign.Center, font, paint);
                canvas.Restore();
            }
            else
            {
                canvas.DrawText(s, colCenterX, y - metrics.Ascent, SKTextAlign.Center, font, paint);
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
