using Ceremony.Domain.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ceremony.Infrastructure.Reporting;

/// <summary>
/// 薦牌（牌位）PDF — 對齊 tmpTablet*.rdlc 9 變體。
/// </summary>
/// <remarks>
/// 頁面 11.5×25.4cm 窄長牌位；標楷體；Number 0.8cm Bold；座標**直接取自 RDLC XML**（含 Tablix / Rectangle
/// 巢狀換算成絕對值），對齊 docs/blueprints/printing-reports-positions.md §3-11。
/// 變體 + DeadName 字級（ParaFontSize）由 Domain.Services.PrintTemplateSelector.ChooseTablet 決定。
/// tmpTabletOneOne 特例：Page Top/Bottom margin 各 2cm（其餘 0）。
/// </remarks>
public sealed class TabletRenderer
{
    private const string FontFamily = "BiauKai";
    private const double PointsPerCm = 28.3464567;

    public byte[] Render(TabletData data)
    {
        var paraPt = data.ParaFontSizeCm * PointsPerCm;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(11.5f, 25.4f, Unit.Centimetre);
                if (data.Template == TabletTemplate.OneOne)
                {
                    // tmpTabletOneOne：上下各 2cm Page Margin（RDLC Page Margin，非 padding）
                    page.MarginTop(2, Unit.Centimetre);
                    page.MarginBottom(2, Unit.Centimetre);
                    page.MarginLeft(0);
                    page.MarginRight(0);
                }
                else
                {
                    page.Margin(0);
                }
                page.DefaultTextStyle(t => t.FontFamily(FontFamily).FontColor(Colors.Black));

                page.Content().Layers(layers =>
                {
                    layers.PrimaryLayer().Background("#FFFFFF");

                    // 共用：Number（0,0，0.8cm Bold，VAlign=Middle）+ HallName（6.1，0.6cm，VAlign=Middle）
                    DrawText(layers, 0.0, 0.0, 4.29646, 1.13229, 0.8 * PointsPerCm, data.Number, bold: true, vMiddle: true);
                    DrawText(layers, 6.1, 3.9, 0.7, 1.3825, 0.6 * PointsPerCm, data.HallNameSecond, vMiddle: true);
                    DrawText(layers, 6.1, 5.9, 0.7, 1.3825, 0.6 * PointsPerCm, data.HallNameFirst, vMiddle: true);

                    DrawDeadNames(layers, data, paraPt);
                    DrawLivingNames(layers, data);
                });
            });
        }).GeneratePdf();
    }

    private static void DrawDeadNames(LayersDescriptor layers, TabletData data, double paraPt)
    {
        var d = data.DeadNames;
        switch (data.Template)
        {
            case TabletTemplate.OneOne:
            case TabletTemplate.OneTwo:
            case TabletTemplate.One:
            {
                // 1 位亡者（高欄）
                var f = VerticalText.GroupFontPt(paraPt, (d[0], 6.466));
                DrawText(layers, 7.5825, 4.8, 0.8, 6.466, f, d[0], vertical: true);
                break;
            }

            case TabletTemplate.TwoOne:
            case TabletTemplate.TwoTwo:
            case TabletTemplate.Two:
            {
                // 2 位亡者（One 在右 L5.3、Two 在左 L4.2，皆高欄）
                var f = VerticalText.GroupFontPt(paraPt, (d[0], 6.31), (d[1], 6.31));
                DrawText(layers, 7.5825, 5.3, 0.8, 6.31, f, d[0], vertical: true);
                DrawText(layers, 7.5825, 4.2, 0.8, 6.31, f, d[1], vertical: true);
                break;
            }

            default:
            {
                // Base / UnderscoreOne / UnderscoreTwo — 3+ 位亡者，5 格巢狀矩陣。
                // 統一字級：起點 ParaFontSize，只有當某格名字塞不下其可用高才把整組一起縮（全組同大小、不重疊）。
                // 可用高：次要格(Two/Three)取「到下一格的列距」且僅當下方有名字才限制；主欄/末排用全高。
                const double deadRowPitch = 9.4464 - 7.5825; // 1.8639
                const double deadFull = 11.0331;
                var f = VerticalText.GroupFontPt(paraPt,
                    (d[0], deadFull),
                    (d[1], VerticalText.Avail(d[3], deadRowPitch, deadFull)),
                    (d[2], VerticalText.Avail(d[4], deadRowPitch, deadFull)),
                    (d[3], 5.5298),
                    (d[4], 5.5298));
                DrawText(layers, 7.5825, 4.9, 0.6, deadFull, f, d[0], vertical: true);   // One（主，高欄）
                DrawText(layers, 7.5825, 5.8, 0.6, deadFull, f, d[1], vertical: true);   // Two
                DrawText(layers, 7.5825, 4.0, 0.6, deadFull, f, d[2], vertical: true);   // Three
                DrawText(layers, 9.4464, 5.8, 0.6, deadFull, f, d[3], vertical: true);   // Four
                DrawText(layers, 9.4464, 4.0, 0.6, deadFull, f, d[4], vertical: true);   // Five
                break;
            }
        }
    }

    private static void DrawLivingNames(LayersDescriptor layers, TabletData data)
    {
        var l = data.LivingNames;
        var pt06 = 0.6 * PointsPerCm;
        var pt08 = 0.8 * PointsPerCm;
        const double LivingRowPitch = 15.44174 - 14.00389; // 1.43785cm 上下排列距
        const double LivingFull = 5.5;
        switch (data.Template)
        {
            case TabletTemplate.OneOne:
            case TabletTemplate.TwoOne:
            case TabletTemplate.UnderscoreOne:
            {
                // 1 位陽上（0.8cm，高欄）
                var f = VerticalText.GroupFontPt(pt08, (l[0], 5.5));
                DrawText(layers, 14.00389, 0.83528, 0.8, 5.5, f, l[0], vertical: true);
                break;
            }

            case TabletTemplate.OneTwo:
            case TabletTemplate.TwoTwo:
            case TabletTemplate.UnderscoreTwo:
            {
                // 2 位陽上（0.8cm，高欄）
                var f = VerticalText.GroupFontPt(pt08, (l[0], 5.5), (l[1], 5.5));
                DrawText(layers, 14.00389, 1.2825, 0.8, 5.5, f, l[0], vertical: true);
                DrawText(layers, 14.00389, 0.30611, 0.8, 5.5, f, l[1], vertical: true);
                break;
            }

            // 3-6 位陽上（0.6cm）矩陣：上排 l[1]/l[2](Top14.00389) 到下排 l[3]/l[4](Top15.44174)
            // 列距 1.43785cm。統一字級（整組同大小，最擠的塞得下才不重疊）；主欄 l[0] 全高不限。
            case TabletTemplate.Two:
            {
                // Two 變體 L 微調
                var f = VerticalText.GroupFontPt(pt06,
                    (l[0], LivingFull),
                    (l[1], VerticalText.Avail(l[3], LivingRowPitch, LivingFull)),
                    (l[2], VerticalText.Avail(l[4], LivingRowPitch, LivingFull)),
                    (l[3], LivingFull), (l[4], LivingFull));
                DrawText(layers, 14.00389, 1.52639, 0.7, LivingFull, f, l[0], vertical: true);
                DrawText(layers, 14.00389, 0.8, 0.7, LivingFull, f, l[1], vertical: true);
                DrawText(layers, 14.0, 0.1, 0.7, LivingFull, f, l[2], vertical: true);
                DrawText(layers, 15.44174, 0.8, 0.7, LivingFull, f, l[3], vertical: true);
                DrawText(layers, 15.44174, 0.1, 0.7, LivingFull, f, l[4], vertical: true);
                break;
            }

            case TabletTemplate.One:
            {
                // One 變體
                var f = VerticalText.GroupFontPt(pt06,
                    (l[0], LivingFull),
                    (l[1], VerticalText.Avail(l[3], LivingRowPitch, LivingFull)),
                    (l[2], VerticalText.Avail(l[4], LivingRowPitch, LivingFull)),
                    (l[3], LivingFull), (l[4], LivingFull));
                DrawText(layers, 14.00389, 1.56167, 0.7, LivingFull, f, l[0], vertical: true);
                DrawText(layers, 14.00389, 0.83528, 0.7, LivingFull, f, l[1], vertical: true);
                DrawText(layers, 14.0, 0.1, 0.7, LivingFull, f, l[2], vertical: true);
                DrawText(layers, 15.44174, 0.83528, 0.7, LivingFull, f, l[3], vertical: true);
                DrawText(layers, 15.44174, 0.1, 0.7, LivingFull, f, l[4], vertical: true);
                break;
            }

            default:
            {
                // Base
                var f = VerticalText.GroupFontPt(pt06,
                    (l[0], LivingFull),
                    (l[1], VerticalText.Avail(l[3], LivingRowPitch, LivingFull)),
                    (l[2], VerticalText.Avail(l[4], LivingRowPitch, LivingFull)),
                    (l[3], LivingFull), (l[4], LivingFull));
                DrawText(layers, 14.00389, 1.56167, 0.7, LivingFull, f, l[0], vertical: true);
                DrawText(layers, 14.00389, 0.83528, 0.7, LivingFull, f, l[1], vertical: true);
                DrawText(layers, 14.0, 0.1, 0.7, LivingFull, f, l[2], vertical: true);
                DrawText(layers, 15.44174, 0.83528, 0.7, LivingFull, f, l[3], vertical: true);
                DrawText(layers, 15.44174, 0.13528, 0.7, LivingFull, f, l[4], vertical: true);
                break;
            }
        }
    }

    private static void DrawText(LayersDescriptor layers, double top, double left, double width, double height, double fontPt, string? text, bool bold = false, bool vMiddle = false, bool vertical = false)
    {
        if (string.IsNullOrEmpty(text)) return;

        // 直書姓名欄：RDLC 用窄欄 + 自動換行達成「一字一列」。改用顯式換行（每字一行）並
        // 不約束寬度——否則標楷體全形字寬 ≈ 欄寬，QuestPDF 因放不下不可斷的單字而**靜默丟字**
        // （PingFang fallback 時字較窄剛好塞進去，換成真標楷體就消失）。
        // 直書姓名欄：顯式每字一行（免 QuestPDF 窄欄丟字）+ 不約束寬度。
        // 字級由呼叫端用 VerticalText.GroupFontPt 算好「整組統一字級」後傳入，這裡不再逐格縮。
        var content = vertical ? VerticalText.Stack(text) : text;

        var fontCm = fontPt / PointsPerCm;
        // vMiddle：以 translate 位移模擬 VerticalAlign=Middle（橫向單字，如 Number / 堂號）。
        var y = vMiddle ? top + (height - fontCm) / 2.0 : top;

        var layer = layers.Layer()
            .TranslateX((float)left, Unit.Centimetre)
            .TranslateY((float)y, Unit.Centimetre);
        if (!vertical) layer = layer.Width((float)width, Unit.Centimetre);

        var span = layer.Text(content).FontSize((float)fontPt).FontFamily(FontFamily).LineHeight(1f);
        if (bold) span.Bold();
    }
}

public sealed record TabletData(
    string Number,
    string? HallNameFirst,
    string? HallNameSecond,
    string?[] DeadNames,      // 6 元素
    string?[] LivingNames,    // 6 元素
    double ParaFontSizeCm,    // 由 PrintTemplateSelector 決定 (0.6 or 0.8)
    TabletTemplate Template);
