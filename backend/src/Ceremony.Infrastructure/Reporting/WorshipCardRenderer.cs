using System.Reflection;
using Ceremony.Domain.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ceremony.Infrastructure.Reporting;

/// <summary>
/// 普桌資料卡 PDF — template 全印（左葫蘆輪廓 + 右側「電話／備註／確認無誤請簽名」標題 + 簽名底線），
/// 白紙即可列印，不依賴預印卡紙。
/// </summary>
/// <remarks>
/// 全新報表（舊系統無對應 RDLC）。樣板：reference/template/普桌資料卡.jpg（A5 橫 21×14.8cm，200 DPI）。
/// Spec: docs/blueprints/printing-reports-positions.md §20 普桌資料卡
/// Blueprint: docs/blueprints/printing-reports.md「普桌資料卡」
///
/// 葫蘆內＝普桌牌位（WorshipRenderer）縮小版：Number + 陽上姓名 6 變體排版，座標用「墨跡對墨跡」
/// 仿射映射從 WorshipRenderer 搬過來（本檔保留 WorshipRenderer 的原始座標字面值，統一過 MapTop/MapLeft，
/// 映射錨值推導見 positions §20）。右側電話／備註為橫書。
/// 2026-07-18 客訴改版（比照 DataCardRenderer 同日修正）：template 改由程式全印——葫蘆輪廓重用
/// worship2.png（與卡片樣板同一款線稿，目視比對確認）縮放到樣板量測的墨跡 bbox；右側標題與
/// 簽名底線依樣板墨跡量測座標繪製。樣板 jpg 仍僅 debugOverlay:true（Development）時當對位底圖。
/// </remarks>
public sealed class WorshipCardRenderer
{
    private const string FontFamily = "BiauKai";            // macOS 內建；Windows 為 "DFKai-SB"
    private const double PointsPerCm = 28.3464567;
    private const double PageWidthCm = 21.0;
    private const double PageHeightCm = 14.8;

    // ── 葫蘆墨跡仿射映射（positions §20）──
    // 普桌 A4 葫蘆墨跡（worship2.png ink bbox，fit-by-height 28.88438cm/976px 換算）：
    //   Top 0.64614、高 28.08532、寬 17.96395、中軸 10.039（= WorshipRenderer Number 欄中心 5.5875+8.903/2，
    //   繞過 FitArea X 對齊歧義：以「編號置中於葫蘆」這個視覺事實當 X 錨）。
    // 卡片葫蘆墨跡（worshipcard-template.jpg 200 DPI 暗像素掃描）：
    //   Top 0.7620、Bottom 13.9446（高 13.1826）、Left 2.4003、Right 10.6807（寬 8.2804）、中軸 6.5405。
    private const double CardGourdTop = 0.7620;
    private const double CardAxisX = 6.5405;
    private const double WorshipGourdTop = 0.64614;
    private const double WorshipAxisX = 10.039;
    private const double Sy = 13.1826 / 28.08532;   // ≈ 0.46938
    private const double Sx = 8.2804 / 17.96395;    // ≈ 0.46094（長寬比與 A4 版差 1.8%，形狀吻合）
    private const double Sf = Sx;                   // 字級縮放 = min(Sx, Sy)，保欄距

    // WorshipRenderer 的原始格高（cm）；使用時 × Sy 供 GroupFontPt 守住映射後格高
    private const double SlotH = 10.21125;
    private const double OneColH = 18.65146;
    private const double TwoColH = 17.56667;
    private const double ThreeLowerH = 10.95208;

    // 右側欄位（樣板 200 DPI 量測）：「電話：」label 上緣 4.4704、「備註：」上緣 5.6515、
    // 冒號右緣 13.462 → 內容 Left 留 0.2cm 間隙；寬度收在頁右緣前（21 − 13.662 − 0.54）。
    private const double PhoneTop = 4.4704;
    private const double RemarkTop = 5.6515;
    private const double FieldLeft = 13.662;
    private const double FieldWidth = 6.8;

    // ── template 元素（2026-07-18 客訴：連 template 一起印）──
    // 葫蘆輪廓：worship2.png（647×976px，墨跡 bbox x 20..626、y 13..961）縮放到卡片葫蘆墨跡
    // bbox（Top 0.7620、Left 2.4003、W 8.2804、H 13.1826，§20 量測錨值）。整張圖框座標由
    // 「墨跡落在量測 bbox」反推：cm/px = 8.2804/607（X）、13.1826/949（Y）。
    private const double GourdFrameLeft = 2.4003 - 20 * (8.2804 / 607);    // ≈ 2.12747
    private const double GourdFrameTop = 0.7620 - 13 * (13.1826 / 949);    // ≈ 0.58142
    private const double GourdFrameWidth = 647 * (8.2804 / 607);           // ≈ 8.82606
    private const double GourdFrameHeight = 976 * (13.1826 / 949);         // ≈ 13.55766
    // 右側標題／簽名底線（worshipcard-template.jpg 200 DPI 墨跡量測，px×0.0127）：
    // 「電話：」墨跡 Top 4.4704、Left 11.9380；「備註：」Top 5.6388、Left 11.8872；
    // 「確認無誤請簽名」y 896..946、x 938..1314 → Top 11.3792、Left 11.9126（墨跡寬 4.788cm ⇒ 字級 0.7cm，渲染回掃寬度吻合）；
    // 簽名底線 y 1086..1088、x 1075..1462 → Top 13.792、Left 13.6525、寬 4.9276、線厚 3px≈1pt。
    // 標題繪製座標＝墨跡目標 − 渲染回掃差值（BiauKai 墨跡相對 em-box 偏右下 0.025~0.05cm）。
    // 校正用「生產字型」渲染（ReportFonts 註冊真 BiauKai，非測試 fallback）回掃，逐項誤差 ≤0.013cm（1px）。
    private const double PhoneLabelTop = 4.4323;      // 墨跡落 4.4704
    private const double PhoneLabelLeft = 11.8618;    // 墨跡落 11.9380
    private const double RemarkLabelTop = 5.6007;     // 墨跡落 5.6388
    private const double RemarkLabelLeft = 11.8618;   // 墨跡落 11.8872
    private const double SignLabelTop = 11.3411;      // 墨跡落 11.3792
    private const double SignLabelLeft = 11.8872;     // 墨跡落 11.9126
    private const double SignLineTop = 13.792;
    private const double SignLineLeft = 13.6525;
    private const double SignLineWidth = 4.9276;

    // 葫蘆線稿（EmbeddedResource；與 WorshipRenderer 共用同一張 worship2.png）— 生產列印使用
    private static readonly byte[] GourdImage = LoadTemplate("worship2.png");

    // 開發用列印位置檢視工具的樣板照片（EmbeddedResource）；只在 debugOverlay:true 時載入使用，
    // 不進生產列印路徑。詳見 docs/blueprints/printing-reports.md「開發用列印位置檢視工具」。
    private static readonly byte[] TemplateImage = LoadTemplate("worshipcard-template.jpg");

    public byte[] Render(WorshipCardData data, bool debugOverlay = false)
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
                    layers.PrimaryLayer().Background("#FFFFFF");

                    if (debugOverlay)
                    {
                        // FitUnproportionally：樣板照片＝座標系統，直接拉伸填滿頁面（見 DataCardRenderer 同段說明）
                        layers.Layer()
                            .TranslateX(0, Unit.Centimetre)
                            .TranslateY(0, Unit.Centimetre)
                            .Width((float)PageWidthCm, Unit.Centimetre)
                            .Height((float)PageHeightCm, Unit.Centimetre)
                            .Image(TemplateImage).FitUnproportionally();
                    }

                    // 2026-07-18 客訴改版：template（葫蘆輪廓／右側標題／簽名底線）由程式全印，
                    // 白紙即可列印，不再假設印在預印卡紙上。
                    DrawTemplate(layers);

                    // Number：WorshipRenderer 原座標 (Top 4.474, Left 5.5875, W 8.903)、2cm Bold Center
                    DrawNumber(layers,
                        top: MapTop(4.474), left: MapLeft(5.5875), width: 8.903 * Sx,
                        fontPt: 2.0 * Sf * PointsPerCm, text: data.Number);

                    DrawLivingNames(layers, data);

                    // 右側橫書欄位內容（標題「電話：」「備註：」由 DrawTemplate 繪製）
                    DrawText(layers, top: PhoneTop, left: FieldLeft, width: FieldWidth, fontCm: 0.6, text: data.Phone);
                    DrawText(layers, top: RemarkTop, left: FieldLeft, width: FieldWidth, fontCm: 0.6, text: data.Remark);
                });
            });
        }).GeneratePdf();
    }

    /// <summary>
    /// 把樣板本身印出來（2026-07-18 客訴：普桌資料卡要連 template 都印，白紙即可列印）：
    /// 葫蘆輪廓（worship2.png 縮放至樣板墨跡 bbox）＋右側標題（電話／備註／確認無誤請簽名）＋簽名底線。
    /// 座標＝樣板 jpg 200 DPI 墨跡量測值（常數區塊注解），與既有內容欄位共用同一座標系，內容欄位座標不動。
    /// </summary>
    private static void DrawTemplate(LayersDescriptor layers)
    {
        // 葫蘆：FitUnproportionally 拉伸到反推的圖框，墨跡即落在量測 bbox（X/Y 縮放差 1.8%，同 Sx/Sy 注解）
        layers.Layer()
            .TranslateX((float)GourdFrameLeft, Unit.Centimetre)
            .TranslateY((float)GourdFrameTop, Unit.Centimetre)
            .Width((float)GourdFrameWidth, Unit.Centimetre)
            .Height((float)GourdFrameHeight, Unit.Centimetre)
            .Image(GourdImage).FitUnproportionally();

        // 標題（座標含回掃校正，見常數注解；電話/備註內容仍以 PhoneTop/RemarkTop 對齊標題墨跡上緣）
        DrawText(layers, top: PhoneLabelTop, left: PhoneLabelLeft, width: 3.0, fontCm: 0.6, text: "電話：");
        DrawText(layers, top: RemarkLabelTop, left: RemarkLabelLeft, width: 3.0, fontCm: 0.6, text: "備註：");
        DrawText(layers, top: SignLabelTop, left: SignLabelLeft, width: 6.0, fontCm: 0.7, text: "確認無誤請簽名");

        // 簽名底線
        layers.Layer()
            .TranslateX((float)SignLineLeft, Unit.Centimetre)
            .TranslateY((float)SignLineTop, Unit.Centimetre)
            .Width((float)SignLineWidth, Unit.Centimetre)
            .LineHorizontal(1.0f);
    }

    // 6 變體排版逐行對照 WorshipRenderer.DrawLivingNames：座標字面值相同，統一過 MapTop/MapLeft，
    // 字級 × Sf、格高 × Sy。WithBottomGap／FirstPresent 配對邏輯不變。
    private static void DrawLivingNames(LayersDescriptor layers, WorshipCardData data)
    {
        var l = data.LivingNames;
        var pt3 = 3.0 * Sf * PointsPerCm;
        var pt2 = 2.0 * Sf * PointsPerCm;

        switch (data.Template)
        {
            case WorshipTemplate.One:
            {
                var f = VerticalText.GroupFontPt(pt3, (l[0], OneColH * Sy));
                DrawName(layers, MapTop(7.31167), MapLeft(8.55021), f, l[0]);
                break;
            }
            case WorshipTemplate.Two:
            {
                var f = VerticalText.GroupFontPt(pt3, (l[0], TwoColH * Sy), (l[1], TwoColH * Sy));
                DrawName(layers, MapTop(7.31167), MapLeft(10.34938), f, l[0]);
                DrawName(layers, MapTop(7.31167), MapLeft(6.62188), f, l[1]);
                break;
            }
            case WorshipTemplate.Three:
            {
                // 三角排列：①主欄通過下排兩欄之間，X 不重疊 → 不需上下排空格
                var f = VerticalText.GroupFontPt(pt3, (l[0], OneColH * Sy), (l[1], ThreeLowerH * Sy), (l[2], ThreeLowerH * Sy));
                DrawName(layers, MapTop(7.31167), MapLeft(8.55021), f, l[0]);
                DrawName(layers, MapTop(14.47139), MapLeft(12.10938), f, l[1]);
                DrawName(layers, MapTop(14.47139), MapLeft(5.00792), f, l[2]);
                break;
            }
            case WorshipTemplate.Four:
            {
                // 2×2：上排右①左②、下排右③左④（同欄配對 0↔2、1↔3）
                var n0 = VerticalText.WithBottomGap(l[0], l[2]);
                var n1 = VerticalText.WithBottomGap(l[1], l[3]);
                var f = VerticalText.GroupFontPt(pt2, (n0, SlotH * Sy), (n1, SlotH * Sy), (l[2], SlotH * Sy), (l[3], SlotH * Sy));
                DrawName(layers, MapTop(7.31167), MapLeft(10.4575), f, n0);
                DrawName(layers, MapTop(7.31167), MapLeft(7.26334), f, n1);
                DrawName(layers, MapTop(17.69931), MapLeft(10.4575), f, l[2]);
                DrawName(layers, MapTop(17.69931), MapLeft(7.26334), f, l[3]);
                break;
            }
            case WorshipTemplate.Five:
            {
                // 上 2 下 3：上下欄位 X 錯開，配對取 X 範圍有重疊者（同 WorshipRenderer 注解）
                var n0 = VerticalText.WithBottomGap(l[0], FirstPresent(l[2], l[3]));
                var n1 = VerticalText.WithBottomGap(l[1], FirstPresent(l[4], l[3]));
                var f = VerticalText.GroupFontPt(pt2,
                    (n0, SlotH * Sy), (n1, SlotH * Sy), (l[2], SlotH * Sy), (l[3], SlotH * Sy), (l[4], SlotH * Sy));
                DrawName(layers, MapTop(7.31167), MapLeft(10.14), f, n0);
                DrawName(layers, MapTop(7.31167), MapLeft(7.5848), f, n1);
                DrawName(layers, MapTop(17.69931), MapLeft(11.07715), f, l[2]);
                DrawName(layers, MapTop(17.69931), MapLeft(8.86362), f, l[3]);
                DrawName(layers, MapTop(17.69931), MapLeft(6.62834), f, l[4]);
                break;
            }
            default: // Base 2×3 矩陣：上排右→左①②③、下排右→左④⑤⑥（同欄配對 0↔3、1↔4、2↔5）
            {
                // 2026-07-18 客訴置中修正：同 WorshipRenderer，6 欄 Left +0.1786 對齊葫蘆中軸（positions §14）
                var n0 = VerticalText.WithBottomGap(l[0], l[3]);
                var n1 = VerticalText.WithBottomGap(l[1], l[4]);
                var n2 = VerticalText.WithBottomGap(l[2], l[5]);
                var f = VerticalText.GroupFontPt(pt2,
                    (n0, SlotH * Sy), (n1, SlotH * Sy), (n2, SlotH * Sy),
                    (l[3], SlotH * Sy), (l[4], SlotH * Sy), (l[5], SlotH * Sy));
                DrawName(layers, MapTop(7.31167), MapLeft(11.2711), f, n0);
                DrawName(layers, MapTop(7.31167), MapLeft(9.00694), f, n1);
                DrawName(layers, MapTop(7.31167), MapLeft(6.80694), f, n2);
                DrawName(layers, MapTop(17.69931), MapLeft(11.25575), f, l[3]);
                DrawName(layers, MapTop(17.69931), MapLeft(9.04222), f, l[4]);
                DrawName(layers, MapTop(17.69931), MapLeft(6.80694), f, l[5]);
                break;
            }
        }
    }

    private static double MapTop(double worshipTop) => CardGourdTop + (worshipTop - WorshipGourdTop) * Sy;
    private static double MapLeft(double worshipLeft) => CardAxisX + (worshipLeft - WorshipAxisX) * Sx;

    private static string? FirstPresent(string? a, string? b)
        => !string.IsNullOrWhiteSpace(a) ? a : b;

    /// <summary>橫書 Number（Center）。不用 .Height() 約束（避免行高超出被裁）；行高壓 1.0 倍。</summary>
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

    /// <summary>直書姓名：顯式每字一行 + 不約束寬度（窄欄自動換行會被 QuestPDF 靜默丟字）。</summary>
    private static void DrawName(LayersDescriptor layers, double top, double left, double fontPt, string? name)
    {
        if (string.IsNullOrEmpty(name)) return;
        layers.Layer()
            .TranslateX((float)left, Unit.Centimetre)
            .TranslateY((float)top, Unit.Centimetre)
            .Text(VerticalText.Stack(name)).FontSize((float)fontPt).FontFamily(FontFamily).LineHeight(1f);
    }

    /// <summary>橫書欄位內容。不設 .Height()（行高超出會整段被裁），過長自動換行不裁字。</summary>
    private static void DrawText(LayersDescriptor layers, double top, double left, double width, double fontCm, string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var fontPt = (float)(fontCm * PointsPerCm);
        layers.Layer()
            .TranslateX((float)left, Unit.Centimetre)
            .TranslateY((float)top, Unit.Centimetre)
            .Width((float)width, Unit.Centimetre)
            .Text(text).FontSize(fontPt).FontFamily(FontFamily).LineHeight(1f);
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

public sealed record WorshipCardData(
    string Number,
    string?[] LivingNames,    // 6 元素
    WorshipTemplate Template,
    string? Phone,
    string? Remark);
