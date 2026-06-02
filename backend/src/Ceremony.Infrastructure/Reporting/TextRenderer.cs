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
/// PhotoAddress 為 25×605px 直書地址 PNG（SkiaSharp 移植自 Library.DrawText），嵌入 0.66×16.8cm 窄帶。
/// 字型固定 BiauKai；DeadName / LivingName 0.8cm、HallName 0.6cm VAlign=Middle、Number 1cm Bold。
/// </remarks>
public sealed class TextRenderer
{
    private const string FontFamily = "BiauKai";
    private const double PointsPerCm = 28.3464567;

    public byte[] Render(TextData data)
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

                    // Number (Top 3.8, Left 31.49729, 1cm Bold)
                    DrawText(layers, 3.8, 31.49729, 4.74896, 1.10272, 1.0 * PointsPerCm, data.Number, bold: true);

                    // HallName (Top 2.1, VAlign=Middle, 0.6cm)
                    DrawText(layers, 2.1, 11.5, 0.7, 1.3825, 0.6 * PointsPerCm, data.HallNameSecond, vMiddle: true);
                    DrawText(layers, 2.1, 13.53753, 0.7, 1.3825, 0.6 * PointsPerCm, data.HallNameFirst, vMiddle: true);

                    // LivingNames 5 位（0.8cm）— tmpText.rdlc；矩陣上排(Top15.2748)→下排(Top17.25916)
                    // 列距 1.98436cm。統一字級（整組同大小、最擠的塞得下才不重疊）；上排僅當下方有名字才以列距為界。
                    var pt08l = 0.8 * PointsPerCm;
                    const double livingPitch = 17.25916 - 15.2748; // 1.98436
                    const double livingFull = 6.72806;
                    var lv = data.LivingNames;
                    var fl = VerticalText.GroupFontPt(pt08l,
                        (lv[0], livingFull),
                        (lv[1], VerticalText.Avail(lv[3], livingPitch, livingFull)),
                        (lv[2], VerticalText.Avail(lv[4], livingPitch, livingFull)),
                        (lv[3], livingFull), (lv[4], livingFull));
                    DrawText(layers, 15.2748, 21.87382, 0.91251, livingFull, fl, lv[0], vertical: true);
                    DrawText(layers, 15.2748, 20.96131, 0.91251, livingFull, fl, lv[1], vertical: true);
                    DrawText(layers, 15.2748, 20.0488, 0.91251, livingFull, fl, lv[2], vertical: true);
                    DrawText(layers, 17.25916, 20.96131, 0.91251, livingFull, fl, lv[3], vertical: true);
                    DrawText(layers, 17.25916, 20.0488, 0.91251, livingFull, fl, lv[4], vertical: true);

                    // DeadName（Rectangle2 群組，絕對座標 = Rect 原點 + 相對；0.8cm）
                    DrawDeadNames(layers, data);

                    // PhotoAddress（垂直地址 PNG，Top 4.1 Left 25.4 W 0.66 H 16.8 FitProportional）
                    if (!string.IsNullOrEmpty(data.Address))
                    {
                        layers.Layer()
                            .TranslateX(25.4f, Unit.Centimetre)
                            .TranslateY(4.1f, Unit.Centimetre)
                            .Width(0.66f, Unit.Centimetre)
                            .Height(16.8f, Unit.Centimetre)
                            .Image(SkiaImageHelpers.VerticalAddress(data.Address)).FitArea();
                    }
                });
            });
        }).GeneratePdf();
    }

    private static void DrawDeadNames(LayersDescriptor layers, TextData data)
    {
        var pt08 = 0.8 * PointsPerCm;
        var d = data.DeadNames;
        if (data.Template == TextTemplate.Two)
        {
            // tmpTextTwo Rectangle2 origin (3.62361, 11.5) — 2 亡者皆高欄
            var f2 = VerticalText.GroupFontPt(pt08, (d[0], 10.50374), (d[1], 10.50374));
            DrawText(layers, 3.65889, 13.01299, 0.91251, 10.50374, f2, d[0], vertical: true);
            DrawText(layers, 3.62361, 11.85, 0.91251, 10.50374, f2, d[1], vertical: true);
            return;
        }

        // tmpText Rectangle2 origin (3.65889, 11.5) — 5 格矩陣：上排 Two/Three(Top3.65889)
        // 到下排 Four/Five(Top5.72264) 列距 = 2.06375cm。統一字級（整組同大小，最擠的塞得下才不重疊）；
        // 次要格上排只在「正下方有名字」時才以列距為界，否則整欄高（不限）。
        const double pitch = 5.72264 - 3.65889; // 2.06375
        const double full = 10.50374;
        var f = VerticalText.GroupFontPt(pt08,
            (d[0], full),
            (d[1], VerticalText.Avail(d[3], pitch, full)),
            (d[2], VerticalText.Avail(d[4], pitch, full)),
            (d[3], full), (d[4], full));
        DrawText(layers, 3.65889, 12.41251, 0.91251, full, f, d[0], vertical: true); // One（主欄）
        DrawText(layers, 3.65889, 13.32502, 0.91251, full, f, d[1], vertical: true); // Two
        DrawText(layers, 3.65889, 11.5, 0.91251, full, f, d[2], vertical: true);     // Three
        DrawText(layers, 5.72264, 13.32502, 0.91251, full, f, d[3], vertical: true); // Four
        DrawText(layers, 5.72264, 11.5, 0.91251, full, f, d[4], vertical: true);     // Five
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
}

public sealed record TextData(
    string Number,
    string? HallNameFirst,
    string? HallNameSecond,
    string?[] DeadNames,
    string?[] LivingNames,
    string? Address,
    TextTemplate Template);
