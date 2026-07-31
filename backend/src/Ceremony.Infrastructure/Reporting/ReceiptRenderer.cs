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
/// 字級：14pt 主資訊；16pt 郵寄封面（地址過長時降階，見 <see cref="CoverAddressFontPt"/>）；0.6cm Name。
/// 對齊 docs/blueprints/printing-reports-positions.md §2；
/// 2026-07-18 依客戶樣張（reference/收據.jpg）校正第 1 頁座標，偏離 RDLC 原始值，見 §2 改版覆蓋註記；
/// 2026-07-28 依客戶實印信封回掃（reference/template/收據封面.jpg）校正第 2 頁封面姓名列與地址換行。
/// </remarks>
public sealed class ReceiptRenderer
{
    private const string FontFamily = "BiauKai";
    private const double PointsPerCm = 28.3464567;
    internal const double PageWidthCm = 21.0;
    internal const double PageHeightCm = 29.7;

    // 郵寄封面（第 2 頁）欄位錨值 — 見 docs/blueprints/printing-reports-positions.md §2「郵寄標籤區」。
    // 2026-07-28 客訴（客戶實印信封回掃 reference/template/收據封面.jpg，200DPI 量測）：
    // (1) 收件人姓名要「對齊右邊大德」＝下移到信封預印「大德　法啟」那一列（客戶確認只下移、Left 不動，
    //     仍與郵遞區號／地址同左緣）。回掃：預印「大德」墨跡 y 432–464px、原姓名墨跡 384–417px，
    //     以基線（CJK 無下伸部→墨跡下緣）對齊需下移 47px；同圖以本 renderer 三個已知欄位 Top
    //     （3.90 / 4.67056 / 5.44111）線性回歸得 68.79px/cm → 47px ≒ 0.683cm，Top 5.44111 → 6.13。
    // (2) 地址過長要印到下一行：QuestPDF 於 CoverAddressWidthCm 內自動斷行，但行高會往下長，
    //     以往第 2 行就會壓到 Top 5.44111 的姓名列。姓名改錨在預印「大德」列後不可再跟著地址移動
    //     （否則就對不到預印字），故改由地址端負責：字級依 CoverAddressFontPt 動態降階，
    //     確保換行後的整段都塞在姓名列之上。
    private const double CoverLeftCm = 4.75646;
    private const double CoverZipcodeTopCm = 3.90;
    private const double CoverAddressTopCm = 4.67056;
    internal const double CoverNameTopCm = 6.13;
    private const double CoverAddressWidthCm = 10.67562;
    private const double CoverNameWidthCm = 9.24354;
    private const double CoverFontPt = 16;

    // 地址字級降階梯（第一階＝RDLC 原始 16pt；只有塞不下才往下降，正常長度地址完全等於原字級）。
    private static readonly double[] CoverAddressFontTiersPt = [16, 14, 13, 12, 11, 10];

    // 開發用列印位置檢視工具的樣板照片（EmbeddedResource，來源 reference/template/收據.jpg）；
    // 只在 debugOverlay:true 時載入使用，不進生產列印路徑。
    // 詳見 docs/blueprints/printing-reports.md「開發用列印位置檢視工具」。
    private static readonly byte[] TemplateImage = LoadTemplate("receipt-template.jpg");

    // 郵寄封面樣板（來源 reference/template/收據封面.jpg，客戶實印信封回掃 200DPI）。
    // 與其他報表樣板不同：**不是整頁掃描**，只有信封本體那一塊，故不可拉滿整頁，必須依量測擺放。
    private static readonly byte[] CoverTemplateImage = LoadTemplate("receipt-cover-template.png");

    // 封面樣板擺放錨值：以回掃圖上「本 renderer 印出的」郵遞區號／地址／姓名墨跡，對照同一筆資料
    // 新版 PDF 的實際墨跡位置解出 scale 與原點——
    // scale：取純 CJK 欄位（姓名 113px / 1.638cm ≒ 68.99、列距 47px / 0.683cm ≒ 68.79）→ 68.85 px/cm
    //        （地址反推 67.31 偏低，因半形「97」在客戶 Windows 標楷體與本機字型寬度不同，不採計）；
    // 原點：三欄各自解出 x 0.022~0.072（取 0.05）、y −0.062~−0.119（取 −0.10）。
    // ⚠️ 兩個 QuestPDF 圖片路徑的雷（文字不受影響，只有 Image 會中）——都已在**資產端**處理掉，
    //    故資產是裁過的 PNG（1441×665，非原始 1458×672 JPG）：
    //    (1) 位移為負 → 整張圖直接不畫（不是只裁掉超出的部分）→ 圖上緣先裁 7px（＝0.10cm），改用 Top 0；
    //    (2) 寬度超出頁面（原圖 1458px ＝ 21.18cm > A4 21cm）→ 同樣整張不畫 → 右緣裁到 1441px（20.93cm）。
    private const double CoverTemplateLeftCm = 0.05;
    private const double CoverTemplateTopCm = 0.0;
    private const double CoverTemplateWidthCm = 1441 / 68.85;
    private const double CoverTemplateHeightCm = 665 / 68.85;

    public byte[] Render(ReceiptData data, bool debugOverlay = false)
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
                page.Size((float)PageWidthCm, (float)PageHeightCm, Unit.Centimetre);
                page.Margin(0);
                page.DefaultTextStyle(t => t.FontFamily(FontFamily).FontColor(Colors.Black));

                page.Content().Layers(layers =>
                {
                    layers.PrimaryLayer().Background("#FFFFFF");

                    if (debugOverlay)
                    {
                        // 疊客戶實印信封回掃供對位（預印「大德　法啟」列／寺方地址列）。
                        layers.Layer()
                            .TranslateX((float)CoverTemplateLeftCm, Unit.Centimetre)
                            .TranslateY((float)CoverTemplateTopCm, Unit.Centimetre)
                            .Width((float)CoverTemplateWidthCm, Unit.Centimetre)
                            .Height((float)CoverTemplateHeightCm, Unit.Centimetre)
                            .Image(CoverTemplateImage).FitUnproportionally();
                    }

                    DrawText(layers, CoverZipcodeTopCm, CoverLeftCm, 2.50, 0.70, CoverFontPt, data.Zipcode);
                    DrawText(layers, CoverAddressTopCm, CoverLeftCm, CoverAddressWidthCm, 0.70,
                        CoverAddressFontPt(data.Address), data.Address);
                    DrawText(layers, CoverNameTopCm, CoverLeftCm, CoverNameWidthCm, 0.70, CoverFontPt, data.Name);
                });
            });
        }).GeneratePdf();
    }

    /// <summary>
    /// 郵寄封面地址字級：取「換行後整段還塞得進地址列到姓名列之間」的**最大**字級。
    /// 一般長度（16pt 下 ≤ 2 行 ≈ 37 個全形字）完全等於 RDLC 原始 16pt，不會無故縮字。
    /// </summary>
    internal static double CoverAddressFontPt(string? address)
    {
        if (string.IsNullOrEmpty(address)) return CoverFontPt;

        // 可用高＝地址列頂到姓名列頂（姓名錨在預印「大德」列，不能被地址推走）。
        var budgetCm = CoverNameTopCm - CoverAddressTopCm;
        foreach (var pt in CoverAddressFontTiersPt)
        {
            var fontCm = pt / PointsPerCm;                       // LineHeight(1) → 每行進位 = 字級
            var maxLines = (int)Math.Floor(budgetCm / fontCm);
            if (maxLines >= 1 && CoverAddressLineCount(address, pt) <= maxLines) return pt;
        }
        return CoverAddressFontTiersPt[^1];
    }

    /// <summary>
    /// 估算地址在封面地址欄寬下的行數（QuestPDF 對 CJK 可任意處斷行，逐字貪婪換行即可近似）。
    /// 全形字寬 ≈ 字級、半形（數字 / 英文 / 空白）≈ 半個字級；欄寬再打 0.97 安全係數，
    /// 寧可早一階縮字，也不要估太寬→實際多長一行→壓到姓名列。
    /// </summary>
    internal static int CoverAddressLineCount(string address, double fontPt)
    {
        var capacityEm = CoverAddressWidthCm / (fontPt / PointsPerCm) * 0.97;
        var lines = 1;
        var used = 0.0;
        foreach (var ch in address)
        {
            if (ch is '\n')
            {
                lines++;
                used = 0;
                continue;
            }

            var em = ch < 0x1100 ? 0.5 : 1.0;                    // 0x1100 以下＝ASCII/拉丁/標點，視為半形
            if (used + em > capacityEm + 1e-9)
            {
                lines++;
                used = em;
            }
            else
            {
                used += em;
            }
        }
        return lines;
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
