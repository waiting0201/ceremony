using System.Reflection;
using Ceremony.Domain.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ceremony.Infrastructure.Reporting;

/// <summary>
/// 普桌 PDF — 對齊 tmpWorship*.rdlc 6 變體（各變體有各自的姓名座標排版）。
/// </summary>
/// <remarks>
/// 頁面 21×29.6cm A4 直；含 worship2 背景紋飾圖（6 變體共用同一張，見 positions §EmbeddedImage）。
/// 變體排版（座標權威：docs/blueprints/printing-reports-positions.md §14–19；客戶樣張 reference/普桌.jpg 佐證）：
/// One 單欄置中、Two 右①左②、Three 上①下右②下左③（三角）、Four 2×2、Five 上 2 下 3、Base 2×3 矩陣。
/// 姓名為直書：顯式每字一行（VerticalText.Stack，免 QuestPDF 窄欄靜默丟字）+ 整組統一字級
/// （VerticalText.GroupFontPt 守住格高，滿足「各容納5個字」需求）+ 同欄上下排間補全形空格
/// （VerticalText.WithBottomGap，2026-07-04 使用者定案）。
/// </remarks>
public sealed class WorshipRenderer
{
    private const string FontFamily = "BiauKai";
    private const double PointsPerCm = 28.3464567;

    // RDLC 姓名格高（cm）。2cm 字變體（Base/Four/Five）與 3cm 字變體（One/Two/Three）各自的格高。
    private const double SlotH = 10.21125;      // 2×3 矩陣 / 2×2 / 上2下3 的每格高
    private const double OneColH = 18.65146;    // One 與 Three 主欄（單欄長格）
    private const double TwoColH = 17.56667;    // Two 雙欄
    private const double ThreeLowerH = 10.95208; // Three 下排兩格

    // worship2.png 背景圖（EmbeddedResource）只載一次；座標精確到小數 5 位（positions §14，不可四捨五入）。
    private static readonly byte[] BackgroundImage = LoadBackground();

    public byte[] Render(WorshipData data)
    {
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

                    // Number 2cm Bold Center (Top 4.474, Left 5.5875) — 6 變體共用
                    DrawNumber(layers, 4.474, 5.5875, 8.903, 2.0 * PointsPerCm, data.Number);

                    DrawLivingNames(layers, data);
                });
            });
        }).GeneratePdf();
    }

    private static void DrawLivingNames(LayersDescriptor layers, WorshipData data)
    {
        var l = data.LivingNames;
        var pt3 = 3.0 * PointsPerCm;
        var pt2 = 2.0 * PointsPerCm;

        switch (data.Template)
        {
            case WorshipTemplate.One:
            {
                var f = VerticalText.GroupFontPt(pt3, (l[0], OneColH));
                DrawName(layers, 7.31167, 8.55021, f, l[0]);
                break;
            }
            case WorshipTemplate.Two:
            {
                var f = VerticalText.GroupFontPt(pt3, (l[0], TwoColH), (l[1], TwoColH));
                DrawName(layers, 7.31167, 10.34938, f, l[0]);
                DrawName(layers, 7.31167, 6.62188, f, l[1]);
                break;
            }
            case WorshipTemplate.Three:
            {
                // 三角排列：①主欄（8.55021）通過下排兩欄（12.10938 / 5.00792）之間，X 不重疊 → 不需上下排空格
                var f = VerticalText.GroupFontPt(pt3, (l[0], OneColH), (l[1], ThreeLowerH), (l[2], ThreeLowerH));
                DrawName(layers, 7.31167, 8.55021, f, l[0]);
                DrawName(layers, 14.47139, 12.10938, f, l[1]);
                DrawName(layers, 14.47139, 5.00792, f, l[2]);
                break;
            }
            case WorshipTemplate.Four:
            {
                // 2×2：上排右①左②、下排右③左④（同欄配對 0↔2、1↔3）
                var n0 = VerticalText.WithBottomGap(l[0], l[2]);
                var n1 = VerticalText.WithBottomGap(l[1], l[3]);
                var f = VerticalText.GroupFontPt(pt2, (n0, SlotH), (n1, SlotH), (l[2], SlotH), (l[3], SlotH));
                DrawName(layers, 7.31167, 10.4575, f, n0);
                DrawName(layers, 7.31167, 7.26334, f, n1);
                DrawName(layers, 17.69931, 10.4575, f, l[2]);
                DrawName(layers, 17.69931, 7.26334, f, l[3]);
                break;
            }
            case WorshipTemplate.Five:
            {
                // 上 2 下 3：上下欄位 X 錯開（非正對），上排姓名的「正下方」取 X 範圍有重疊者
                // （① 10.14–12.34 疊 ③ 11.08–13.28 與 ④ 8.86–11.06；② 7.58–9.78 疊 ⑤ 6.63–8.83 與 ④）
                var n0 = VerticalText.WithBottomGap(l[0], FirstPresent(l[2], l[3]));
                var n1 = VerticalText.WithBottomGap(l[1], FirstPresent(l[4], l[3]));
                var f = VerticalText.GroupFontPt(pt2,
                    (n0, SlotH), (n1, SlotH), (l[2], SlotH), (l[3], SlotH), (l[4], SlotH));
                DrawName(layers, 7.31167, 10.14, f, n0);
                DrawName(layers, 7.31167, 7.5848, f, n1);
                DrawName(layers, 17.69931, 11.07715, f, l[2]);
                DrawName(layers, 17.69931, 8.86362, f, l[3]);
                DrawName(layers, 17.69931, 6.62834, f, l[4]);
                break;
            }
            default: // Base 2×3 矩陣：上排右→左①②③、下排右→左④⑤⑥（同欄配對 0↔3、1↔4、2↔5）
            {
                // 2026-07-18 客訴「六位的要靠右一點，比較置中」：RDLC 原值整組墨跡中心 9.8604
                // 偏離葫蘆中軸 10.039（positions §20 錨值），6 欄 Left 統一 +0.1786 對齊中軸（positions §14）
                var n0 = VerticalText.WithBottomGap(l[0], l[3]);
                var n1 = VerticalText.WithBottomGap(l[1], l[4]);
                var n2 = VerticalText.WithBottomGap(l[2], l[5]);
                var f = VerticalText.GroupFontPt(pt2,
                    (n0, SlotH), (n1, SlotH), (n2, SlotH),
                    (l[3], SlotH), (l[4], SlotH), (l[5], SlotH));
                DrawName(layers, 7.31167, 11.2711, f, n0);
                DrawName(layers, 7.31167, 9.00694, f, n1);
                DrawName(layers, 7.31167, 6.80694, f, n2);
                DrawName(layers, 17.69931, 11.25575, f, l[3]);
                DrawName(layers, 17.69931, 9.04222, f, l[4]);
                DrawName(layers, 17.69931, 6.80694, f, l[5]);
                break;
            }
        }
    }

    private static string? FirstPresent(string? a, string? b)
        => !string.IsNullOrWhiteSpace(a) ? a : b;

    private static byte[] LoadBackground()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames().Single(n => n.EndsWith("worship2.png", StringComparison.Ordinal));
        using var stream = asm.GetManifestResourceStream(name)!;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>橫書 Number（RDLC TextAlign=Center）。不用 .Height() 約束（避免行高超出被裁）；行高壓 1.0 倍。</summary>
    private static void DrawNumber(LayersDescriptor layers, double top, double left, double width, double fontPt, string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        layers.Layer()
            .TranslateX((float)left, Unit.Centimetre)
            .TranslateY((float)top, Unit.Centimetre)
            .Width((float)width, Unit.Centimetre)
            .AlignCenter()
            .Text(text).FontSize((float)fontPt).FontFamily(FontFamily).LineHeight(1f).Bold();
    }

    /// <summary>直書姓名：顯式每字一行 + 不約束寬度（RDLC LivingName 無 TextAlign = 靠左，X 直接用 RDLC Left）。</summary>
    private static void DrawName(LayersDescriptor layers, double top, double left, double fontPt, string? name)
    {
        if (string.IsNullOrEmpty(name)) return;
        layers.Layer()
            .TranslateX((float)left, Unit.Centimetre)
            .TranslateY((float)top, Unit.Centimetre)
            .Text(VerticalText.Stack(name)).FontSize((float)fontPt).FontFamily(FontFamily).LineHeight(1f);
    }
}

public sealed record WorshipData(
    string Number,
    string?[] LivingNames,    // 6 元素
    WorshipTemplate Template);
