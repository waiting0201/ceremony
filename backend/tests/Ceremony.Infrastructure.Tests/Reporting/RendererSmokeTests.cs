using System.Linq;
using System.Text;
using Ceremony.Domain.Services;
using Ceremony.Infrastructure.Reporting;
using FluentAssertions;
using QuestPDF.Infrastructure;

namespace Ceremony.Infrastructure.Tests.Reporting;

/// <summary>
/// Renderer smoke tests：確保各報表 / 各變體都能產出有效 PDF（不丟例外）。
/// 涵蓋新加的 SkiaSharp 路徑（DataCard 虛線、Text 垂直地址）與 worship2 背景嵌入。
/// 需系統有標楷體（dev macOS 內建 BiauKai）。
/// </summary>
public sealed class RendererSmokeTests
{
    static RendererSmokeTests() => QuestPDF.Settings.License = LicenseType.Community;

    private static string?[] N(params string?[] xs)
    {
        var a = new string?[6];
        for (var i = 0; i < Math.Min(xs.Length, 6); i++) a[i] = xs[i];
        return a;
    }

    private static void ShouldBePdf(byte[] bytes)
    {
        bytes.Should().NotBeNullOrEmpty();
        bytes.Length.Should().BeGreaterThan(500);
        Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    // 薦牌直書姓名：每字一行（修正全形標楷體在窄欄被 QuestPDF 靜默丟字的問題）。
    [Theory]
    [InlineData("往生甲", "往\n生\n甲")]
    [InlineData("陳", "陳")]
    [InlineData("", "")]
    [InlineData("陳 明", "陳\n \n明")]   // 中間空格＝刻意排版間隙，渲染成空白列，必須保留
    public void StackVertical_one_char_per_line(string input, string expected)
        => VerticalText.Stack(input).Should().Be(expected);

    // GroupFontPt 刻意把中間空格計入列數（與 Stack 渲染列數一致），否則會低估列數 → 疊字。
    // "陳 明" = 3 列（陳/空格/明）。若誤排空格成 2 列，1.4378/2 > 0.6 會維持 0.6；計入空格才縮到 1.4378/3。
    [Fact]
    public void GroupFontPt_counts_middle_space_as_a_row()
    {
        var f = VerticalText.GroupFontPt(0.6 * PtPerCm, ("陳 明", 1.4378));
        (f / PtPerCm).Should().BeApproximately(1.4378 / 3, 1e-6);
    }

    // 開頭全形空格（U+3000，常用來把名字往下推作排版）必須與 Stack 一致算一列，否則字級沒縮 → 蓋下一格。
    // 真實案例 signup 543EA33D：「　蔡炎城」(4 列) 蓋到下方「蔡貴仁」。回歸鎖。
    [Fact]
    public void GroupFontPt_counts_leading_fullwidth_space_as_a_row()
    {
        var f = VerticalText.GroupFontPt(0.6 * PtPerCm, ("　蔡炎城", 1.8639));
        (f / PtPerCm).Should().BeApproximately(1.8639 / 4, 1e-6, "開頭全形空格算第 4 列，須縮字以免溢出列距");
    }

    private const double PtPerCm = 28.3464567;

    // 整組統一字級：只有當「最擠那格」塞不下舊字級時，整組一起縮到塞得下（全組同大小）。
    [Fact]
    public void GroupFontPt_shrinks_whole_group_to_tightest_cell()
    {
        // 陽上列距 1.4378cm 放 3 字 → 0.6×3=1.8 > 1.4378 → 整組縮到 1.4378/3
        var f = VerticalText.GroupFontPt(0.6 * PtPerCm, ("陳大明", 1.4378));
        (f / PtPerCm).Should().BeApproximately(1.4378 / 3, 1e-6);
    }

    [Fact]
    public void GroupFontPt_keeps_legacy_size_when_all_cells_fit()
    {
        // 往生列距 1.8639 放 3 字（1.8<1.8639）+ 主欄很高 → 都塞得下 → 維持舊 0.6cm
        var f = VerticalText.GroupFontPt(0.6 * PtPerCm, ("陳大明", 1.8639), ("林秀英", 11.0331));
        (f / PtPerCm).Should().BeApproximately(0.6, 1e-6);
    }

    [Fact]
    public void GroupFontPt_one_tight_cell_shrinks_entire_group_uniformly()
    {
        // 一格很擠（3 字 / 1.4378）會把整組（含本來塞得下的）一起縮到同一個較小字級
        var f = VerticalText.GroupFontPt(0.6 * PtPerCm, ("陳大明", 11.0331), ("陳孝二", 1.4378));
        (f / PtPerCm).Should().BeApproximately(1.4378 / 3, 1e-6);
    }

    [Fact]
    public void GroupFontPt_ignores_empty_cells()
    {
        var f = VerticalText.GroupFontPt(0.8 * PtPerCm, ("陳大明", 1.2), (null, 0.1), ("", 0.1));
        (f / PtPerCm).Should().BeApproximately(1.2 / 3, 1e-6);
    }

    [Fact]
    public void DataCard_RendersPdf()
    {
        var pdf = new DataCardRenderer().Render(new DataCardData(
            Number: "信1", Prepay: "預繳 115 梁皇",
            DeadNames: N("陳大明", "陳二", "陳三"), LivingNames: N("陳孝", "陳順"),
            Address: "台北市中山區民族東路161號5樓", Phone: "0912345678", Remark: "無"));
        ShouldBePdf(pdf);
    }

    // 開發用列印位置檢視工具：debugOverlay 疊資料卡樣板照片（reference/template/資料卡.jpg），
    // 不進生產列印路徑（預設 false）。回歸鎖：只驗證疊圖真的畫出來、仍是有效 PDF。
    [Fact]
    public void DataCard_DebugOverlay_DumpsCalibrationPdf()
    {
        var data = new DataCardData(
            Number: "信1", Prepay: "預繳 115 梁皇",
            DeadNames: N("陳大明", "陳二", "陳三"), LivingNames: N("陳孝", "陳順"),
            Address: "台北市中山區民族東路161號5樓", Phone: "0912345678", Remark: "無");

        var plain = new DataCardRenderer().Render(data);
        ShouldBePdf(plain);

        var overlay = new DataCardRenderer().Render(data, debugOverlay: true);
        ShouldBePdf(overlay);
        overlay.Length.Should().BeGreaterThan(plain.Length, "樣板疊圖必須真的畫出來，不是被忽略的參數");
        DumpIfRequested(overlay, "datacard_debug_overlay.pdf");
    }

    // 2026-07-04 使用者指定版面調整：陽上改 3 排×2 欄（6 字寬）、地址上移 1cm、備註下移 0.5cm、
    // 地址/備註縮寬避開右側窗框（可換行）、亡者窗框內文字再靠右 0.3cm。用滿 5 位陽上 + 較長地址/備註
    // （會換行）驗證新版面互不重疊。
    [Fact]
    public void DataCard_FiveLivingNamesAndWrappedText_DumpsCalibrationPdf()
    {
        var data = new DataCardData(
            Number: "信1", Prepay: "預繳 115 梁皇",
            DeadNames: N("陳大明", "陳二", "陳三"),
            LivingNames: N("陳孝一二三", "陳順一二三", "陳仁一二三", "陳義一二三", "陳智一二三"),
            Address: "台北市中山區民族東路一百六十一巷十二弄三號五樓之六（含較長地址測試換行）",
            Phone: "0912345678",
            Remark: "這是一段刻意寫長的備註文字，用來確認備註欄縮寬後仍能正常換行顯示，不會被裁切或蓋到右側樣板窗框。");

        var pdf = new DataCardRenderer().Render(data);
        ShouldBePdf(pdf);

        var overlay = new DataCardRenderer().Render(data, debugOverlay: true);
        ShouldBePdf(overlay);
        DumpIfRequested(overlay, "datacard_five_living_wrapped_overlay.pdf");
    }

    // 2026-07-03 資料卡改版（見 docs/blueprints/printing-reports.md「資料卡改版」）：用
    // reference/template/資料卡.jpg 樣板照片量測發現原 25-TextBox 版面跟實際印刷紙張對不起來
    // （樣板沒有亡者/堂號欄，「陽上：」實際位置在 Top≈2.69cm 不是原本的 4.707cm）。改版後亡者姓名
    // 改印進樣板右側「故◯◯靈位」窗框圖案裡（比照 TabletRenderer 直書堆疊 + GroupFontPt 縮字）。
    // 回歸鎖：多位亡者以「、」串接後，縮字結果的實際高度不能超出窗框量測缺口（含 0.3cm 安全邊界）。
    [Fact]
    public void DataCard_SixDeadNames_MatrixStaysWithinMeasuredWindow()
    {
        // 2026-07-05 改版：亡者改採跟 TabletRenderer.DrawDeadNames 一樣的 2×3 矩陣（1st 中間上、2nd 右邊上、
        // 3rd 左邊上、4th 右邊下、5th 左邊下、6th 中間下），取代舊版單欄「、」串接。用滿 6 位亡者（含長名字
        // 觸發縮字）驗證矩陣不超出窗框缺口。
        var data = new DataCardData(
            Number: "信1", Prepay: "預繳 115 梁皇",
            DeadNames: N("陳大明一二", "陳二一二三", "陳三一二三", "陳四五六七", "陳五一二三", "陳六一二三"),
            LivingNames: N("陳孝"),
            Address: "台北市中山區民族東路161號5樓", Phone: "0912345678", Remark: "無");

        var pdf = new DataCardRenderer().Render(data);
        ShouldBePdf(pdf);
        DumpIfRequested(new DataCardRenderer().Render(data, debugOverlay: true), "datacard_six_dead_matrix_overlay.pdf");

        // 2026-07-21 客訴：字級改採與薦牌一致的 MatrixLayout（起點 ParaFontSize、窗框內動態縮 + 動態下排起點），
        // 取代舊版固定列距 2.6 + GroupFontPt。此處重算須與 DataCardRenderer.DrawDeadNamesInWindow 同一套邏輯。
        const double topRowY = 5.6388 + 0.1;
        const double windowGapBottom = 11.4427; // 「靈」字上緣，硬邊界
        const double safetyMargin = 0.2;
        const double boxHeight = windowGapBottom - topRowY - safetyMargin;

        var d = data.DeadNames;
        var (fontCm, bottomOffset) = VerticalText.MatrixLayout(
            0.6, boxHeight, (d[0], d[5]), (d[1], d[3]), (d[2], d[4]));
        var bottomRowY = topRowY + bottomOffset;

        foreach (var (rowTop, name) in new[] { (topRowY, d[0]), (topRowY, d[1]), (topRowY, d[2]), (bottomRowY, d[3]), (bottomRowY, d[4]), (bottomRowY, d[5]) })
        {
            var bottomCm = rowTop + name!.Length * fontCm;
            bottomCm.Should().BeLessThanOrEqualTo(windowGapBottom - safetyMargin + 1e-6,
                $"「{name}」縮字後不應超出窗框缺口（含安全邊界）");
        }
    }

    // 6 個相異單字姓名（避免同姓氏視覺上混在一起難以判斷欄位），用來目視確認 2×3 矩陣 6 格彼此有間距、
    // 不會因為短名字字級沒縮到極限而互相貼在一起（見 baseFontCm 從 0.8 降到 0.6cm 的說明，
    // DrawDeadNamesInWindow 註解）。
    [Fact]
    public void DataCard_SixDistinctDeadNames_MatrixColumnsDoNotTouch()
    {
        var data = new DataCardData(
            Number: "信1", Prepay: "", DeadNames: N("甲", "乙", "丙", "丁", "戊", "己"), LivingNames: N(),
            Address: "", Phone: "", Remark: "");

        var pdf = new DataCardRenderer().Render(data);
        ShouldBePdf(pdf);
        DumpIfRequested(new DataCardRenderer().Render(data, debugOverlay: true), "datacard_six_distinct_dead_overlay.pdf");
    }

    [Fact]
    public void DataCard_OneDeadName_MatrixCenterTopRenders()
    {
        // 典型情境（只有 1 位亡者）：應印在「中間上」（矩陣第 1 格），其餘 5 格留空。
        var withName = new DataCardData(
            Number: "信1", Prepay: "", DeadNames: N("陳大明"), LivingNames: N(),
            Address: "", Phone: "", Remark: "");
        var empty = new DataCardData(
            Number: "信1", Prepay: "", DeadNames: N(), LivingNames: N(),
            Address: "", Phone: "", Remark: "");

        var pdfWithName = new DataCardRenderer().Render(withName);
        var pdfEmpty = new DataCardRenderer().Render(empty);
        ShouldBePdf(pdfWithName);
        pdfWithName.Length.Should().BeGreaterThan(pdfEmpty.Length, "唯一一位亡者必須真的畫出來");
    }

    // 2026-07-27 客訴：往者 1 位／2 位時姓名要在窗框中置中（框不動；3+ 位矩陣位置不變）。
    // 置中基準＝框中軸 17.278（框外緣 14.973~17.983 幾何中心 + FrameShiftX 0.8），也是「故／靈位」所在線。
    // 直書 CJK 字寬≈字級，故「該組墨跡總寬」的中點必須落在中軸上——先前固定左移 0.3cm 做不到（1 位偏左、
    // 2 位偏右，方向相反）。此處對 0.8cm（1-2 位基準）與 0.5cm（≥8 真字縮字）兩種字級都驗。
    [Fact]
    public void DataCard_OneOrTwoDeadNames_AreCenteredInWindowFrame()
    {
        const double frameCenterX = (14.973 + 17.983) / 2 + 0.8; // 17.278
        const double innerLeft = 14.986 + 0.8;
        const double innerRight = 17.9705 + 0.8;

        foreach (var fontCm in new[] { 0.8, 0.5 })
        {
            // 1 位：單欄置中
            var one = DataCardRenderer.DeadColumnsX(N("陳大明"), fontCm);
            (one.CenterX + fontCm / 2).Should().BeApproximately(frameCenterX, 1e-6, "1 位往者要置中於框中軸");

            // 只填第 2 格（slot tier 2 但實際只有 1 位）：一樣單欄置中
            var onlySecond = DataCardRenderer.DeadColumnsX(N(null, "陳二"), fontCm);
            (onlySecond.RightX + fontCm / 2).Should().BeApproximately(frameCenterX, 1e-6);

            // 2 位：對稱分居中軸左右，整組墨跡（d[1] 左緣 ~ d[0] 右緣）中點落在中軸
            var two = DataCardRenderer.DeadColumnsX(N("陳大明", "陳二"), fontCm);
            ((two.RightX + two.CenterX + fontCm) / 2).Should().BeApproximately(frameCenterX, 1e-6, "2 位往者整組要置中於框中軸");
            // 2026-07-31 客訴：往者一（d[0]，回傳 CenterX）在右、往者二（d[1]，回傳 RightX）在左
            two.CenterX.Should().BeGreaterThan(two.RightX, "往者一要印在往者二右邊（直書右起）");
            (two.CenterX - (two.RightX + fontCm)).Should().BeApproximately(0.1, 1e-6, "兩欄之間留 0.1cm 不可貼在一起");

            // 都不可壓到窗框內緣
            two.RightX.Should().BeGreaterThan(innerLeft);
            (two.CenterX + fontCm).Should().BeLessThan(innerRight);
        }

        // 3+ 位維持原 2×3 矩陣欄位（中欄 16.985、欄距 0.75），不因字級改變
        var threePlus = DataCardRenderer.DeadColumnsX(N("陳大明", "陳二", "陳三"), 0.6);
        threePlus.CenterX.Should().BeApproximately(16.985, 1e-6);
        threePlus.LeftX.Should().BeApproximately(16.985 - 0.75, 1e-6);
        threePlus.RightX.Should().BeApproximately(16.985 + 0.75, 1e-6);

        // slot 有空洞（只填第 3、4 格）→ slot tier 3，走矩陣不走置中
        DataCardRenderer.DeadColumnsX(N(null, null, "陳三", "陳四"), 0.6).CenterX
            .Should().BeApproximately(16.985, 1e-6);

        // 疊圖 PDF 供實體複驗（CEREMONY_PDF_DUMP 才落地）
        foreach (var (names, file) in new[]
                 {
                     (N("陳大明"), "datacard_one_dead_centered_overlay.pdf"),
                     (N("陳大明", "陳二"), "datacard_two_dead_centered_overlay.pdf"),
                 })
        {
            var data = new DataCardData(
                Number: "信1", Prepay: "", DeadNames: names, LivingNames: N("陳孝"),
                Address: "", Phone: "", Remark: "");
            var plain = new DataCardRenderer().Render(data);
            ShouldBePdf(plain);
            DumpIfRequested(plain, file.Replace("_overlay", "_plain"));
            DumpIfRequested(new DataCardRenderer().Render(data, debugOverlay: true), file);
        }
    }

    // 2026-08-14 客訴：「資料卡列印 右邊的列印沒有印到堂號」。根因是 2026-07-03 改版把 HallName 從
    // DataCardModel/DataCardData/ReportModelBuilders 整條移除（當時判定樣板紙沒有堂號欄）。回加後
    // 版面比照薦牌：堂號兩半分列窗框內「故」字左右（右＝First、左＝Second，直書右起）。
    // 回歸鎖：兩欄都要落在「窗框內緣 ↔『故』字邊」的空白帶內，且文字下緣不得越過往者矩陣上緣 5.7388。
    [Fact]
    public void DataCard_HallName_SitsBesideGuGlyphAndStaysAboveDeadNames()
    {
        const double frameCenterX = (14.973 + 17.983) / 2 + 0.8; // 17.278
        const double innerLeft = 14.986 + 0.8;                   // 15.786（樣板量測內緣）
        const double innerRight = 17.9705 + 0.8;                 // 18.7705
        const double glyphLeft = frameCenterX - 1.10 / 2;        // 16.728：「故」左緣
        const double glyphRight = frameCenterX + 1.10 / 2;       // 17.828：「故」右緣
        const double frameTop = 4.394;
        const double deadTopRowY = 5.6388 + 0.1;                 // 5.7388：往者矩陣上緣＝堂號硬下界
        const double bandTop = frameTop + 0.1;
        const double bandHeight = deadTopRowY - bandTop;         // 1.2448

        // SplitHallName：2 字 → 1+1、4 字 → 2+2、3 字（及 5 字以上）→ 整串進 First、Second 空
        foreach (var (first, second) in new[] { ("潁", "川"), ("太原", "王氏"), ("隴西李", "") })
        {
            var (leftX, rightX, fontCm) = DataCardRenderer.HallColumns(first, second);

            fontCm.Should().BeLessThanOrEqualTo(0.6 + 1e-9, "堂號字級起點 0.6cm，只縮不放大");

            // 左半：框內緣 ~「故」左緣之間，且在該空白帶置中
            leftX.Should().BeGreaterThan(innerLeft, $"「{second}」不可壓到窗框左內緣");
            (leftX + fontCm).Should().BeLessThan(glyphLeft, $"「{second}」不可壓到「故」字");
            (leftX + fontCm / 2).Should().BeApproximately((14.973 + 0.8 + glyphLeft) / 2, 1e-6);

            // 右半：「故」右緣 ~ 框內緣之間，且在該空白帶置中
            rightX.Should().BeGreaterThan(glyphRight, $"「{first}」不可壓到「故」字");
            (rightX + fontCm).Should().BeLessThan(innerRight, $"「{first}」不可壓到窗框右內緣");
            (rightX + fontCm / 2).Should().BeApproximately((glyphRight + 17.983 + 0.8) / 2, 1e-6);

            // 垂直：各側依自身字數在可用帶內置中，上不出框、下不碰往者
            foreach (var segment in new[] { first, second })
            {
                var n = VerticalText.ElementCount(segment);
                if (n == 0) continue;
                var top = bandTop + (bandHeight - n * fontCm) / 2;
                top.Should().BeGreaterThan(frameTop, $"「{segment}」不可越過窗框上緣");
                (top + n * fontCm).Should().BeLessThanOrEqualTo(deadTopRowY + 1e-9,
                    $"「{segment}」不可壓到往者矩陣（上緣 {deadTopRowY}）");
            }
        }

        // 3 字堂號整串進 First：0.6cm 塞不下 1.2448cm 可用帶 → 整組等比縮（同往者/陽上的縮字語意）
        DataCardRenderer.HallColumns("隴西李", "").FontCm
            .Should().BeApproximately(bandHeight / 3, 1e-6);
    }

    [Fact]
    public void DataCard_HallName_IsActuallyDrawn()
    {
        var deadNames = N("陳大明", "陳二", "陳三");
        DataCardData Make(string? first, string? second) => new(
            Number: "信1", Prepay: "", DeadNames: deadNames, LivingNames: N("陳孝"),
            Address: "", Phone: "", Remark: "", HallNameFirst: first, HallNameSecond: second);

        var without = new DataCardRenderer().Render(Make(null, null));
        var with = new DataCardRenderer().Render(Make("太原", "王氏"));
        ShouldBePdf(without);
        ShouldBePdf(with);
        with.Length.Should().BeGreaterThan(without.Length, "堂號必須真的畫出來，不是被忽略的欄位");

        // 純列印版 + 疊圖版供實體複驗（CEREMONY_PDF_DUMP 才落地）。
        // ⚠️ 判讀堂號落點要看**純列印版**：疊圖用的樣板照片是 FrameShiftX（右移 0.8cm）之前的原位置，
        // 會出現兩層「故」與兩層框線，堂號左半看起來像壓在（樣板那層的）「故」上，實際沒有。
        foreach (var (first, second, name) in new[]
                 {
                     ("潁", "川", "datacard_hallname_2char"),
                     ("太原", "王氏", "datacard_hallname_4char"),
                     ("隴西李", "", "datacard_hallname_3char"),
                 })
        {
            DumpIfRequested(new DataCardRenderer().Render(Make(first, second)), $"{name}_plain.pdf");
            DumpIfRequested(new DataCardRenderer().Render(Make(first, second), debugOverlay: true), $"{name}_overlay.pdf");
        }
    }

    // 2026-07-18 客訴改版：資料卡改成連 template 一起印（欄位標題／簽名底線／「故◯◯靈位」窗框），
    // 白紙即可列印。回歸鎖：內容全空也必須畫出 template（PDF 遠大於一張空白頁），防止未來誤退回
    // 「假設預印樣板紙、只印內容」的套印模式。
    [Fact]
    public void DataCard_EmptyContent_StillPrintsTemplate()
    {
        var pdf = new DataCardRenderer().Render(new DataCardData(
            Number: "", Prepay: "", DeadNames: N(), LivingNames: N(),
            Address: "", Phone: "", Remark: ""));
        ShouldBePdf(pdf);
        pdf.Length.Should().BeGreaterThan(10_000,
            "template（標題文字/底線/窗框/故靈位）必須在無內容時也被繪製，含内嵌字型的 PDF 不會只有空白頁大小");
    }

    [Fact]
    public void Receipt_RendersPdf()
    {
        var pdf = new ReceiptRenderer().Render(new ReceiptData(
            Name: "陳大明", Zipcode: "110", Address: "台北市信義區市府路 1 號",
            Fee: "1200", Number: "信1", Prepay: "", Year: "115", Month: "5", Day: "29"));
        ShouldBePdf(pdf);
        CountPages(pdf).Should().Be(2, "收據每筆固定兩頁：上下聯 + 郵寄封面（RDLC Tablix 59.4cm）");
        DumpIfRequested(pdf, "receipt_with_cover.pdf");
    }

    // 開發用列印位置檢視工具：debugOverlay 疊收據樣板照片（reference/template/收據.jpg），供對位「郵/大德/號」。
    [Fact]
    public void Receipt_DebugOverlay_DumpsCalibrationPdf()
    {
        var data = new ReceiptData(
            Name: "陳大明", Zipcode: "110", Address: "台北市信義區市府路 1 號",
            Fee: "1200", Number: "信1", Prepay: "", Year: "115", Month: "5", Day: "29");

        var plain = new ReceiptRenderer().Render(data);
        ShouldBePdf(plain);

        var overlay = new ReceiptRenderer().Render(data, debugOverlay: true);
        ShouldBePdf(overlay);
        overlay.Length.Should().BeGreaterThan(plain.Length, "樣板疊圖必須真的畫出來，不是被忽略的參數");
        DumpIfRequested(overlay, "receipt_overlay.pdf");
    }

    // 2026-07-28 客訴（客戶實印信封回掃 reference/template/收據封面.jpg）：郵寄封面收件人姓名要對齊
    // 預印「大德　法啟」那一列（只下移、左緣不動）。回歸鎖：姓名 Top 必須維持在量測錨值，且地址不論
    // 多長都不得長到姓名列（地址換行往下長，姓名錨在預印字上不能被推走）。
    [Fact]
    public void Receipt_CoverName_SitsOnPrePrintedDaDeRow_AndAddressWrapsAboveIt()
    {
        const double addressTopCm = 4.67056;
        const double pointsPerCm = 28.3464567;

        ReceiptRenderer.CoverNameTopCm.Should().BeApproximately(6.13, 1e-9,
            "姓名列錨在預印「大德」墨跡基線（回掃 200DPI：原 5.44111 下移 0.683cm）");

        // 一般長度地址不縮字（維持 RDLC 原始 16pt）
        ReceiptRenderer.CoverAddressFontPt("台中市烏日區三民街97號").Should().Be(16);
        ReceiptRenderer.CoverAddressLineCount("台中市烏日區三民街97號", 16).Should().Be(1);

        // 超過一行 → 印到下一行，字級仍不動（兩行塞得進地址列到姓名列之間）
        const string twoLines = "台中市烏日區三民街97號之3四樓之12號1室";
        ReceiptRenderer.CoverAddressLineCount(twoLines, 16).Should().BeGreaterThan(1, "過長地址要換行，不是截斷");
        ReceiptRenderer.CoverAddressFontPt(twoLines).Should().Be(16);

        // 極長地址 → 降字級塞好，絕不壓到姓名列
        foreach (var address in new[]
                 {
                     "台中市烏日區三民街97號",
                     twoLines,
                     new string('中', 40),
                     new string('中', 80),
                 })
        {
            var pt = ReceiptRenderer.CoverAddressFontPt(address);
            var bottom = addressTopCm + ReceiptRenderer.CoverAddressLineCount(address, pt) * (pt / pointsPerCm);
            bottom.Should().BeLessThanOrEqualTo(ReceiptRenderer.CoverNameTopCm,
                $"地址「{address[..Math.Min(10, address.Length)]}…」換行後不得長到姓名列（實得 {pt}pt）");
        }

        // 疊圖對位樣張：封面頁改成也疊客戶實印信封回掃（reference/template/收據封面.jpg），
        // 供目視確認姓名列落在預印「大德　法啟」上。
        var sample = new ReceiptData(
            Name: "林美鳳", Zipcode: "414", Address: "台中市烏日區三民街97號",
            Fee: "1200", Number: "信1", Prepay: "", Year: "115", Month: "5", Day: "29");
        var samplePlain = new ReceiptRenderer().Render(sample);
        var sampleOverlay = new ReceiptRenderer().Render(sample, debugOverlay: true);
        sampleOverlay.Length.Should().BeGreaterThan(samplePlain.Length, "封面樣板必須真的被疊上去");
        DumpIfRequested(samplePlain, "receipt_cover_name_row.pdf");
        DumpIfRequested(sampleOverlay, "receipt_cover_name_row_overlay.pdf");
        DumpIfRequested(new ReceiptRenderer().Render(new ReceiptData(
            Name: "林美鳳", Zipcode: "414", Address: twoLines,
            Fee: "1200", Number: "信1", Prepay: "", Year: "115", Month: "5", Day: "29")), "receipt_cover_long_address.pdf");
        DumpIfRequested(new ReceiptRenderer().Render(new ReceiptData(
            Name: "林美鳳", Zipcode: "414", Address: new string('中', 80),
            Fee: "1200", Number: "信1", Prepay: "", Year: "115", Month: "5", Day: "29")), "receipt_cover_extreme_address.pdf");
    }

    [Fact]
    public void Receipt_EmptyAddress_StillTwoPages()
    {
        var pdf = new ReceiptRenderer().Render(new ReceiptData(
            Name: "陳大明", Zipcode: "", Address: "",
            Fee: "1200", Number: "信1", Prepay: "", Year: "115", Month: "5", Day: "29"));
        ShouldBePdf(pdf);
        CountPages(pdf).Should().Be(2, "地址空白也要輸出封面頁，維持舊系統送紙順序");
    }

    private static int CountPages(byte[] pdf)
    {
        var text = System.Text.Encoding.Latin1.GetString(pdf);
        return System.Text.RegularExpressions.Regex.Matches(text, @"/Type\s*/Page\b(?!s)").Count;
    }

    [Theory]
    [InlineData(TabletTemplate.OneOne)]
    [InlineData(TabletTemplate.OneTwo)]
    [InlineData(TabletTemplate.One)]
    [InlineData(TabletTemplate.TwoOne)]
    [InlineData(TabletTemplate.TwoTwo)]
    [InlineData(TabletTemplate.Two)]
    [InlineData(TabletTemplate.UnderscoreOne)]
    [InlineData(TabletTemplate.UnderscoreTwo)]
    [InlineData(TabletTemplate.Base)]
    public void Tablet_AllVariants_RenderPdf(TabletTemplate template)
    {
        var pdf = new TabletRenderer().Render(new TabletData(
            Number: "信1", HallNameFirst: "甲", HallNameSecond: "堂",
            DeadNames: N("亡一", "亡二", "亡三", "亡四", "亡五"),
            LivingNames: N("陽一", "陽二", "陽三", "陽四", "陽五"),
            ParaFontSizeCm: 0.6, Template: template));
        ShouldBePdf(pdf);
    }

    [Theory]
    [InlineData(TextTemplate.Base)]
    [InlineData(TextTemplate.Two)]
    public void Text_BothVariants_WithVerticalAddress_RenderPdf(TextTemplate template)
    {
        var pdf = new TextRenderer().Render(new TextData(
            Number: "信1", HallNameFirst: "甲", HallNameSecond: "堂",
            DeadNames: N("亡一", "亡二", "亡三"), LivingNames: N("陽一", "陽二"),
            Address: "台北市中山區民族東路161號5樓", Template: template));
        ShouldBePdf(pdf);
    }

    // 第 6 位往生/陽上必印滿（修正 legacy 缺陷，business-rules-implicit §18）。
    // 回歸鎖：legacy 把第 6 位「靜默丟字」（PDF 仍有效，只是少一欄）→ ShouldBePdf 抓不到。
    // 故用「只填第 6 位 vs 全空」比 PDF 大小：第 6 位若有渲染必含額外字符/字型子集 → 變大；
    // 若被丟掉則兩者相等。直接隔離 d[5]/l[5] 是否真的畫出來。
    [Fact]
    public void Tablet_Base_SixthDeadAndLiving_AreRendered()
    {
        TabletData Data(string?[] dead, string?[] living) => new(
            Number: "信1", HallNameFirst: "甲", HallNameSecond: "堂",
            DeadNames: dead, LivingNames: living, ParaFontSizeCm: 0.6, Template: TabletTemplate.Base);

        var empty = new TabletRenderer().Render(Data(N(), N()));
        var sixthOnly = new TabletRenderer().Render(Data(
            N(null, null, null, null, null, "亡己"),
            N(null, null, null, null, null, "陽巳")));
        ShouldBePdf(sixthOnly);
        sixthOnly.Length.Should().BeGreaterThan(empty.Length,
            "第 6 位往生/陽上必須真的渲染（非如 legacy 靜默丟字）");

        var full = new TabletRenderer().Render(Data(
            N("亡甲", "亡乙", "亡丙", "亡丁", "亡戊", "亡己"),
            N("陽子", "陽丑", "陽寅", "陽卯", "陽辰", "陽巳")));
        ShouldBePdf(full);
        DumpIfRequested(full, "tablet_six.pdf");
    }

    [Fact]
    public void Text_Base_SixthDeadAndLiving_AreRendered()
    {
        TextData Data(string?[] dead, string?[] living) => new(
            Number: "信1", HallNameFirst: "甲", HallNameSecond: "堂",
            DeadNames: dead, LivingNames: living,
            Address: "台北市中山區民族東路161號5樓", Template: TextTemplate.Base);

        var empty = new TextRenderer().Render(Data(N(), N()));
        var sixthOnly = new TextRenderer().Render(Data(
            N(null, null, null, null, null, "亡己"),
            N(null, null, null, null, null, "陽巳")));
        ShouldBePdf(sixthOnly);
        sixthOnly.Length.Should().BeGreaterThan(empty.Length,
            "第 6 位往生/陽上必須真的渲染（非如 legacy 靜默丟字）");

        var full = new TextRenderer().Render(Data(
            N("亡甲", "亡乙", "亡丙", "亡丁", "亡戊", "亡己"),
            N("陽子", "陽丑", "陽寅", "陽卯", "陽辰", "陽巳")));
        ShouldBePdf(full);
        DumpIfRequested(full, "text_six.pdf");
    }

    // 客戶反映（reference/薦牌問題.pdf 手寫註記）薦牌實際列印紙條插入蓮花瓶牌位座後，文字位置
    // 對不準視窗（跑到視窗外/蓋到雕花邊框）。座標已對照 tmpTablet.rdlc XML 逐一核對、與原始 1:1
    // 吻合，PDF 本身文字也不重疊、不超出 11.5×25.4cm 頁面邊界 —— 判斷是「RDLC 校準當年的牌位座
    // 實體尺寸」與「客戶現有牌位座」不一致，屬於實體對位問題，不是排版邏輯錯誤。原本無實測尺寸可
    // 反推修正量，故先提供 debugGrid 疊 1cm 刻度格線版本：印出後插入同一個牌位座，實測視窗上緣/下緣
    // 對到第幾條刻度線，才能算出精確修正量（見 docs/gotchas.md「薦牌實體對位」條）。
    // 2026-07-03 更新：改用 reference/template/薦牌.jpg 實體樣板照片（200 DPI）量測窗框座標，
    // 修正了 Base 變體主欄可用高（見 DrawDeadNames default 分支 deadFull），詳見
    // Tablet_Base_LongDeadName_StaysWithinMeasuredWindow。debugGrid 仍保留供未來實機二次校正用。
    [Fact]
    public void Tablet_DebugGrid_ForRealComplaintScenario_DumpsCalibrationPdf()
    {
        // 近似 reference/薦牌問題.pdf 場景：2 亡（蔡姓歷代祖先、蔡黃氏）+ 3 陽（蔡渭水、蔡慧明、蔡碧英）
        var data = new TabletData(
            Number: "郵1", HallNameFirst: null, HallNameSecond: null,
            DeadNames: N("蔡姓歷代祖先", "蔡黃氏"),
            LivingNames: N("蔡渭水", "蔡慧明", "蔡碧英"),
            ParaFontSizeCm: 0.8, Template: TabletTemplate.Two);

        var plain = new TabletRenderer().Render(data);
        ShouldBePdf(plain);
        DumpIfRequested(plain, "tablet_alignment_complaint.pdf");

        var grid = new TabletRenderer().Render(data, debugGrid: true);
        ShouldBePdf(grid);
        grid.Length.Should().BeGreaterThan(plain.Length, "格線疊層必須真的畫出來，不是被忽略的參數");
        DumpIfRequested(grid, "tablet_alignment_complaint_grid.pdf");

        // 同一場景疊樣板照片版本，供目視比對 Two 變體（正式系統對「恰好 2 位亡者」實際會選的變體，
        // 見 PrintTemplateSelector.ChooseTablet）跟 Base 變體（tablet_debug_overlay_Base.pdf，
        // 3+ 位亡者用的 6 格矩陣，硬塞 2 個名字進去只是壓力測試）排版是否有差異。
        var overlay = new TabletRenderer().Render(data, debugOverlay: true);
        ShouldBePdf(overlay);
        DumpIfRequested(overlay, "tablet_alignment_complaint_overlay.pdf");
    }

    // 2026-07-05 診斷：Base 變體 Three/Five 欄（3+ 位亡者矩陣左欄）Left=4.0，比 Two 變體的 Left=4.2
    // 更靠左，且比樣板量到的窗框內緣（4.191cm）更小——用 3 位亡者實際觸發 Three 欄位，疊圖檢查是否
    // 真的壓到雕花邊框（見 docs/gotchas.md「薦牌實體對位」條的延伸追查）。
    [Fact]
    public void Tablet_Base_ThreeDeadNames_DumpsThreeColumnOverlay()
    {
        var data = new TabletData(
            Number: "郵1", HallNameFirst: null, HallNameSecond: null,
            DeadNames: N("蔡姓歷代祖先", "蔡黃氏", "蔡氏三"),
            LivingNames: N("蔡渭水"),
            ParaFontSizeCm: 0.6, Template: TabletTemplate.Base);

        var overlay = new TabletRenderer().Render(data, debugOverlay: true);
        ShouldBePdf(overlay);
        DumpIfRequested(overlay, "tablet_base_three_dead_overlay.pdf");
    }

    // 2026-07-05：驗證「1 位亡者完全置中在故／靈位中心線上」——比照 Tablet_Base_ThreeDeadNames_
    // DumpsThreeColumnOverlay 的疊圖驗證手法，用 OneOne 變體（單一亡者）疊圖目視確認置中。
    [Fact]
    public void Tablet_OneDeadName_DumpsCenteredOverlay()
    {
        var data = new TabletData(
            Number: "郵1", HallNameFirst: null, HallNameSecond: null,
            DeadNames: N("蔡姓歷代祖先"), LivingNames: N("蔡渭水"),
            ParaFontSizeCm: 0.8, Template: TabletTemplate.OneOne);

        var overlay = new TabletRenderer().Render(data, debugOverlay: true);
        ShouldBePdf(overlay);
        DumpIfRequested(overlay, "tablet_one_dead_centered_overlay.pdf");
    }

    // 2026-07-03：Base 變體長名字曾被印到窗框外（對應「薦牌實體對位」客訴）。
    // 2026-07-17 改版後，3+ 位亡者矩陣排在「故」下 0.2cm 起的 2.8×5.4cm 方框內
    // （見 TabletRenderer.DeadMatrix* 常數），框底 13.1946cm 在「靈」字上緣（13.462cm）之上。
    //
    // ★ 2026-08-14 使用者指定「3、4、5、6 位字體大小都跟目前 4 位的一樣大，不用因為字數再縮小」後，
    //   本測試的語意**反轉**：原本鎖「14 字長名字縮字後不得超出方框」，現在鎖「字級固定 0.6cm 不縮，
    //   超長名字**會**超出方框」——這是使用者在「保留最後防線」與「完全不縮」之間明確選的取捨。
    //   走生產路徑（MatrixLayoutNoShrink）而非直接呼叫 MatrixLayout，否則測試會鎖到一條沒人跑的公式。
    [Fact]
    public void Tablet_Base_LongDeadName_KeepsBaseFontSize_EvenWhenOverflowing()
    {
        var longName = string.Concat(Enumerable.Repeat("蔡", 14)); // 14 字，舊規則會縮到 0.386cm
        var data = new TabletData(
            Number: "郵1", HallNameFirst: null, HallNameSecond: null,
            DeadNames: N(longName), LivingNames: N("蔡渭水"),
            ParaFontSizeCm: 0.6, Template: TabletTemplate.Base);

        var pdf = new TabletRenderer().Render(data);
        ShouldBePdf(pdf);
        DumpIfRequested(pdf, "tablet_base_long_dead_name.pdf");
        DumpIfRequested(new TabletRenderer().Render(data, debugOverlay: true), "tablet_base_long_dead_name_overlay.pdf");

        const double windowInnerTopCm = 6.2294;
        const double textTopCm = 7.7946;      // 故下緣 7.5946 + 0.2（使用者指定）
        const double boxHeightCm = 5.4;       // 使用者指定方框高

        // 生產路徑：3+ 位矩陣自 2026-08-14 起走 MatrixLayoutNoShrink
        var (fontCm, _) = VerticalText.MatrixLayoutNoShrink(0.6, (longName, null));
        var textBottomCm = textTopCm + VerticalText.ElementCount(longName) * fontCm;

        textTopCm.Should().BeGreaterThanOrEqualTo(windowInnerTopCm, "主欄起點不應在窗框內緣之上");
        fontCm.Should().BeApproximately(0.6, 1e-9,
            "14 字長名字不再縮字——字級固定＝ParaFontSize（使用者 2026-08-14 指定）");
        (VerticalText.MatrixLayout(0.6, boxHeightCm, (longName, null)).FontCm)
            .Should().BeLessThan(0.6, "前提：舊規則（MatrixLayout）在同一組輸入下是會縮的，本輪刻意不用它");
        textBottomCm.Should().BeGreaterThan(textTopCm + boxHeightCm,
            "已知且刻意的取捨：14 字在固定 0.6cm 下會超出 5.4cm 方框、壓到預印「靈位」");
    }

    // ★ 2026-08-06 客訴回歸鎖（客戶實印照片：往者的字壓在預印的「靈位」上）。
    //
    // 根因不是字級規則，而是**三種排法各自帶一個可用高常數、來源還不同**：
    //   1 位   `DeadGapHeight−0.1`  → 下緣 13.362
    //   2 位   RDLC 遺留值 `6.31`   → 下緣 13.89   ← 早就越過「靈」上緣，8 個字素起就壓字
    //   3+ 位  手寫量測方框 5.4     → 下緣 13.1946
    // 客戶那筆是 **2 位往生者、每格用全形空格把兩個人名塞成上下兩段**（8 個字素）＝走 2 位分支；
    // 舊的 `>7 真字 → 0.5cm` 門檻用 RealCharCount 只算到 6 個真字，**根本沒觸發**——證明字數門檻
    // 擋不住溢出，可用高才是唯一該管這件事的地方（該門檻已於同日撤除，字級改為自動縮到剛好）。
    // 2026-08-08 補述：字數門檻已依使用者指定回復（字素數 ≥8 → 起點 0.6cm，見 PrintTemplateSelector），
    // 但**這條下界鎖仍是唯一的溢出防線**，語意不變——本測試走生產路徑取起點，門檻只會讓字更小、
    // 不會讓下緣更低，所以斷言不用放寬。
    //
    // 本鎖用生產路徑（PrintTemplateSelector + renderer 同一組公式）算出三種排法在**極端字數**下的
    // 文字下緣。既有 17 支 Tablet smoke 只鎖上界與字級、不鎖下緣，2 位那個洞才會長期沒被抓到。
    //
    // ⚠️ 2026-08-14 起涵蓋範圍縮成 **1 位／2 位**：使用者指定 3~6 位「不用因為字數再縮小」，矩陣
    //    因此刻意退出這條防線（下方改為反向鎖）。客訴的實際路徑（2 位分支）完整保留。
    [Fact]
    public void Tablet_1and2Dead_NeverCrossDeadTextBottom_MatrixDeliberatelyExempt()
    {
        const double bottom = TabletRenderer.DeadTextBottom;   // 13.1946
        const double gapTop = TabletRenderer.DeadGapTop;       // 7.5946（1、2 位起點）
        const double matrixTop = gapTop + 0.2;                 // 7.7946（3+ 位方框頂）
        const double matrixBox = 5.4;
        const double avail = bottom - gapTop;                  // 5.6

        static double BasePt(string?[] dead, string?[] living)
        {
            var (_, para) = PrintTemplateSelector.ChooseTablet(dead, living);
            return double.Parse(para.Replace("cm", "")) * 28.3464567;
        }

        // 客戶實際那筆：2 位往生者，每格用全形空格塞兩個人名（8 / 7 個字素）
        var customer = N("施　棟　施郭秀鑾", "施裕源　施林鳳");
        // 其餘極端：1 位超長、2 位超長、3+ 位上下排都超長
        var oneLong = N(string.Concat(Enumerable.Repeat("蔡", 20)));
        var twoLong = N(string.Concat(Enumerable.Repeat("蔡", 20)), string.Concat(Enumerable.Repeat("陳", 16)));
        var manyLong = N("蔡蔡蔡蔡蔡蔡", "陳陳陳陳陳陳", "林林林林林林", "王王王王王王", "李李李李李李", "吳吳吳吳吳吳");
        var living = N("子甲", "子乙");

        // ── 1 位（One / OneOne / OneTwo）──
        foreach (var dead in new[] { oneLong, N("蔡") })
        {
            var f = VerticalText.GroupFontPt(BasePt(dead, living), (dead[0], avail)) / 28.3464567;
            var b = gapTop + VerticalText.ElementCount(dead[0]) * f;
            b.Should().BeLessThanOrEqualTo(bottom + 1e-6, "1 位往者不得越過共用下界");
        }

        // ── 2 位（Two / TwoOne / TwoTwo）── 客訴就在這一支
        foreach (var dead in new[] { customer, twoLong, N("蔡", "陳") })
        {
            var f = VerticalText.GroupFontPt(BasePt(dead, living), (dead[0], avail), (dead[1], avail)) / 28.3464567;
            foreach (var name in new[] { dead[0], dead[1] })
            {
                var b = gapTop + VerticalText.ElementCount(name) * f;
                b.Should().BeLessThanOrEqualTo(bottom + 1e-6,
                    "2 位往者不得越過共用下界（客戶實印壓到「靈位」的就是這條路徑）");
            }
        }

        // ── 3+ 位矩陣（Base / UnderscoreOne / UnderscoreTwo）──
        // ★ 2026-08-14 起**刻意不受這條下界節制**（使用者指定「3~6 位不用因為字數再縮小」，並在
        //   「保留最後防線」與「完全不縮」之間選了後者）。這裡改成**反向鎖**：確認字級真的沒縮，
        //   而不是把斷言刪掉了事——否則哪天有人「順手」把矩陣接回 MatrixLayout，沒有測試會叫。
        var matrixBase = double.Parse(
            PrintTemplateSelector.ChooseTablet(manyLong, living).ParaFontSize.Replace("cm", ""));
        var (mFont, mOffset) = VerticalText.MatrixLayoutNoShrink(
            matrixBase, (manyLong[0], manyLong[5]), (manyLong[1], manyLong[3]), (manyLong[2], manyLong[4]));
        mFont.Should().BeApproximately(matrixBase, 1e-9,
            "3+ 位矩陣字級固定＝ParaFontSize，任何字數都不縮");
        var matrixBottom = matrixTop + mOffset + VerticalText.ElementCount(manyLong[5]) * mFont;
        matrixBottom.Should().BeGreaterThan(bottom,
            "已知取捨：6 位 × 6 字在固定 0.6cm 下會越過下界壓到「靈位」——使用者明確選的，不是 bug");

        // 樣張：讓使用者能實際看到這個取捨的極端長相（疊樣板照片版最直觀）
        var overflowData = new TabletData(
            Number: "郵1", HallNameFirst: null, HallNameSecond: null,
            DeadNames: manyLong, LivingNames: living,
            ParaFontSizeCm: matrixBase, Template: TabletTemplate.UnderscoreTwo);
        DumpIfRequested(new TabletRenderer().Render(overflowData), "tablet_6dead_longnames_overflow.pdf");
        DumpIfRequested(new TabletRenderer().Render(overflowData, debugOverlay: true),
            "tablet_6dead_longnames_overflow_overlay.pdf");

        // 1 位／2 位仍必須收斂到**同一條**下界（別再散成常數）；矩陣現在是刻意的例外
        avail.Should().BeApproximately(5.6, 1e-9, "1、2 位可用高＝共用下界 − 故下緣");
        (bottom - matrixTop).Should().BeApproximately(matrixBox, 1e-9,
            "矩陣方框幾何未變（只是不再拿它縮字）");
    }

    // 2026-08-08：往者長名字級的兩層規則鎖 + 樣張。
    //  第一層（美觀上限）＝ PrintTemplateSelector 的字素數 ≥8 → 起點 0.6cm（回復舊系統 `.Length > 7`；
    //    2026-07-21 誤調成 0.5cm、08-06 整條撤除，08-08 使用者指定回復成「與三位的一樣」）。
    //  第二層（溢出防線）＝ renderer 的可用高 DeadTextBottom，本輪未動；起點之上仍會再等比縮。
    // 兩層並存的意義：08-06「字數門檻擋不住溢出」的判斷仍成立（所以防線留著），但門檻本身回來
    // 管「長名字不要比 3+ 位往者還大」這件美觀的事——兩者管的不是同一件事。
    [Fact]
    public void Tablet_1and2Dead_LongNames_AutoShrinkToFit_DumpOverlays()
    {
        var name8 = "一二三四五六七八"; // 8 字
        var name7 = "一二三四五六七";   // 7 字

        static TabletData Build(string?[] dead, string?[] living)
        {
            var (template, para) = PrintTemplateSelector.ChooseTablet(dead, living);
            var paraCm = double.Parse(para.Replace("cm", ""));
            return new TabletData("郵1", null, null, dead, living, paraCm, template);
        }

        var oneDead = Build(N(name8), N("子甲", "子乙"));
        oneDead.Template.Should().Be(TabletTemplate.OneTwo);
        oneDead.ParaFontSizeCm.Should().BeApproximately(0.6, 1e-9, "8 字素 → 起點降到 0.6cm");

        var twoDead = Build(N("陳", name8), N("子甲", "子乙"));
        twoDead.Template.Should().Be(TabletTemplate.TwoTwo);
        twoDead.ParaFontSizeCm.Should().BeApproximately(0.6, 1e-9, "任一格 8 字素 → 整組起點降到 0.6cm");

        // 2026-08-14 使用者指定：2 位往者門檻降到 7 字素；1 位維持 8（下面的 control 就是對照組）
        var twoDead7 = Build(N("陳", name7), N("子甲", "子乙"));
        twoDead7.Template.Should().Be(TabletTemplate.TwoTwo);
        twoDead7.ParaFontSizeCm.Should().BeApproximately(0.6, 1e-9, "2 位任一格 7 字素 → 起點 0.6cm");

        var control = Build(N(name7), N("子甲", "子乙"));
        control.ParaFontSizeCm.Should().BeApproximately(0.8, 1e-9,
            "同一個 7 字素名字在 1 位分支不觸發（門檻仍是 8）——兩層門檻刻意不同");

        // renderer 端：起點 0.6cm 在 5.6cm 可用高下塞得下（0.6×8=4.8 ≤ 5.6）→ 維持 0.6cm，不再縮
        const double avail = TabletRenderer.DeadTextBottom - TabletRenderer.DeadGapTop;
        var f8 = VerticalText.GroupFontPt(oneDead.ParaFontSizeCm * 28.3464567, (name8, avail)) / 28.3464567;
        f8.Should().BeApproximately(0.6, 1e-9, "8 字素塞得下 0.6cm 起點 → 就用 0.6cm");
        (8 * f8).Should().BeLessThanOrEqualTo(avail + 1e-9, "仍不得越過可用高");

        // 第二層仍在：字數再多時 0.6cm 起點之上還會自動縮（12 字 → 0.47cm）
        var name12 = string.Concat(Enumerable.Repeat("蔡", 12));
        var f12 = VerticalText.GroupFontPt(0.6 * 28.3464567, (name12, avail)) / 28.3464567;
        f12.Should().BeApproximately(avail / 12, 1e-9, "門檻是起點不是固定值，可用高仍會再縮");
        f12.Should().BeLessThan(0.6);

        // 7 字素控制組：門檻未觸發、可用高也吃得下 → 保住 0.8cm
        var f7 = VerticalText.GroupFontPt(control.ParaFontSizeCm * 28.3464567, (name7, avail)) / 28.3464567;
        f7.Should().BeApproximately(0.8, 1e-9, "7 字素兩層都不觸發，維持 0.8cm");

        foreach (var (data, tag) in new[] { (oneDead, "1dead8"), (twoDead, "2dead8"), (twoDead7, "2dead7"), (control, "1dead7_control") })
        {
            var plain = new TabletRenderer().Render(data);
            ShouldBePdf(plain);
            DumpIfRequested(plain, $"tablet_autoshrink_{tag}.pdf");
            DumpIfRequested(new TabletRenderer().Render(data, debugOverlay: true), $"tablet_autoshrink_{tag}_overlay.pdf");
        }
    }

    // 2026-07-17 客訴回歸鎖（reference/薦牌.jpg 郵27）：5 位亡者 + 5 位陽上時
    // (1) 字級被舊「固定列距 + WithBottomGap」機制縮到 0.37~0.47cm（字太小）；
    // (2) 陽上最左欄 Left=0.1 落在印表機不可列印邊界內，整欄消失（5 位只印出 3 位）；
    // (3) 編號 Left=0.1 的「郵」左半被裁。
    // 改版後：典型 3-4 字姓名必須保住 0.6cm 基準字級；所有陽上欄位 Left ≥ 0.5cm；
    // 下排起點動態＝上排最長字數 +1 個字高間距。
    [Fact]
    public void Tablet_Base_FiveDeadFiveLiving_KeepsBaseFontSize()
    {
        // 對齊客訴照片場景：5 位亡者（含 4 字複姓）+ 5 位陽上（3 字）
        var dead = N("黃毓沛", "歐陽亞麗", "黃放夷", "黃國強", "黃國華");
        var living = N("黃平山", "黃名鳳", "黃志恆", "黃志明", "黃志成");

        // 亡者：最長鏈 = 上排「歐陽亞麗」4 字 + 1 間距 + 下排 3 字 = 8 單位 × 0.6 = 4.8 ≤ 5.4。
        // 2026-08-14 起走 MatrixLayoutNoShrink（字級固定），這組典型姓名在改動前後**數值完全相同**
        // ——本測試同時是「3~6 位不縮」不得誤傷典型案例的迴歸鎖。
        var (deadFont, deadBottomOffset) = VerticalText.MatrixLayoutNoShrink(0.6,
            (dead[0], dead[5]), (dead[1], dead[3]), (dead[2], dead[4]));
        deadFont.Should().BeApproximately(0.6, 1e-9, "典型 3-4 字亡者姓名必須保住 0.6cm 字級");
        VerticalText.MatrixLayout(0.6, 5.4, (dead[0], dead[5]), (dead[1], dead[3]), (dead[2], dead[4]))
            .Should().Be((deadFont, deadBottomOffset), "典型姓名下新舊公式必須逐位元相同（無迴歸）");
        deadBottomOffset.Should().BeApproximately((4 + 1) * 0.6, 1e-9, "下排起點＝最長上排(4字)+1 字高間距");

        // 陽上：最長鏈 = 3 + 1 + 3 = 7 單位 × 0.6 = 4.2 ≤ 4.925 → 不縮
        var (livingFont, livingBottomOffset) = VerticalText.MatrixLayout(0.6, 19.504 - 14.579,
            (living[0], living[5]), (living[1], living[3]), (living[2], living[4]));
        livingFont.Should().BeApproximately(0.6, 1e-9, "典型 3 字陽上姓名必須保住 0.6cm 字級");
        livingBottomOffset.Should().BeApproximately((3 + 1) * 0.6, 1e-9);

        var data = new TabletData(
            Number: "郵27", HallNameFirst: null, HallNameSecond: null,
            DeadNames: dead, LivingNames: living,
            ParaFontSizeCm: 0.6, Template: TabletTemplate.Base);
        var pdf = new TabletRenderer().Render(data);
        ShouldBePdf(pdf);
        DumpIfRequested(pdf, "tablet_5dead_5living_plain.pdf");
        DumpIfRequested(new TabletRenderer().Render(data, debugOverlay: true), "tablet_5dead_5living_overlay.pdf");
    }

    // 2026-08-08 使用者指定「陽上 1 位／2 位左移 0.1cm、下移 0.2cm」（3-6 位矩陣不動）的座標鎖。
    // 鎖的是常數本身 + 三條實體邊界，因為這些值只出現在 DrawText 呼叫參數裡、渲染後無法直接反推。
    [Fact]
    public void Tablet_OneAndTwoLiving_Coordinates_StayWithinPhysicalBounds()
    {
        // 位移量：相對 2026-08-06 的值（Top 14.00389、Left 1.53528 / 1.9825 / 1.00611）
        TabletRenderer.LivingPairTop.Should().BeApproximately(14.00389 + 0.2, 1e-9, "下移 0.2cm");
        TabletRenderer.LivingOneLeft.Should().BeApproximately(1.53528 - 0.1, 1e-9, "1 位左移 0.1cm");
        TabletRenderer.LivingTwoLeftFirst.Should().BeApproximately(1.9825 - 0.1, 1e-9, "2 位 l[0] 左移 0.1cm");
        TabletRenderer.LivingTwoLeftSecond.Should().BeApproximately(1.00611 - 0.1, 1e-9, "2 位 l[1] 左移 0.1cm");

        // 使用者選的是「整塊下移」而非「守住下界、縮可用高」→ 可用高不得跟著變
        TabletRenderer.LivingPairHeight.Should().BeApproximately(5.5, 1e-9, "可用高維持 5.5＝整塊下移");

        const double shift = TabletRenderer.GlobalShiftX;   // -0.25
        const double fontCm = 0.8;

        // (1) 最左緣不得落進印表機不可列印邊界（0.5cm；2026-07-17 客訴「整欄消失」的根因）
        var leftmost = TabletRenderer.LivingTwoLeftSecond + shift;
        leftmost.Should().BeGreaterThan(0.5, "陽上最左欄含全域位移後仍須在可列印區內");

        // (2) 最右緣不得越過標籤帶左側雕花內緣（量測最窄 2.70cm @y14~14.5）
        var rightmost = TabletRenderer.LivingTwoLeftFirst + shift + fontCm;
        rightmost.Should().BeLessThan(2.70, "陽上右緣不得壓到雕花窗框");

        // (3) 文字下緣不得碰到預印「拜薦」上緣 20.49
        var bottom = TabletRenderer.LivingPairTop + TabletRenderer.LivingPairHeight;
        bottom.Should().BeApproximately(19.70389, 1e-9);
        bottom.Should().BeLessThan(20.49, "陽上 1/2 位下緣不得壓到預印「拜薦」");

        // ⚠️ 刻意不與 3-6 位矩陣框底相等：使用者選了整塊下移。這條斷言存在是為了讓未來有人想
        //    「收斂成同一條線」時先看到它是刻意的（對照 DeadTextBottom 那次是真的散落 bug）。
        //    註：08-08 之前兩者也只是「幾乎」重合——1/2 位是 14.00389+5.5=19.50389，矩陣框底是
        //    14.579+4.925=19.504，本來就差 0.00011cm（doc 記的 19.504 是四捨五入後的值）。
        bottom.Should().BeApproximately(19.50389 + 0.2, 1e-9, "＝舊下界整塊下移 0.2cm");
        (bottom - 19.504).Should().BeApproximately(0.2, 1e-3, "1/2 位下界刻意比矩陣框底低約 0.2cm");
    }

    // 2026-08-08 使用者指定「堂號一邊有兩個字時上移 0.2cm」。
    // 堂號是橫排欄位、0.6cm 字塞 0.7cm 欄寬，第 2 字被 QuestPDF 換行往下長，但 vMiddle 只用單字高
    // 算置中 → 視覺重心下沉；以 Top 補回 0.2cm。逐格獨立判斷（SplitHallName：2字→1+1、4字→2+2、
    // 3 字或 5 字以上→整串進 First）。
    [Fact]
    public void Tablet_HallName_MultiCharCell_ShiftsUp()
    {
        TabletRenderer.HallTopOf("堂").Should().BeApproximately(6.3, 1e-9, "單字格維持原位");
        TabletRenderer.HallTopOf("慈光").Should().BeApproximately(6.1, 1e-9, "2 字格上移 0.2cm");
        TabletRenderer.HallTopOf("慈光堂").Should().BeApproximately(6.1, 1e-9, "3 字（整串進右格）同樣上移");
        TabletRenderer.HallTopOf(null).Should().BeApproximately(6.3, 1e-9, "空格子走單字分支（反正不畫）");
        TabletRenderer.HallTopOf("").Should().BeApproximately(6.3, 1e-9);
        // 增補平面造字：.Length=2 但只是一個字 → 不得誤判成 2 字（gotchas「一個字不等於一個 char」）
        TabletRenderer.HallTopOf("\U00020000").Should().BeApproximately(6.3, 1e-9, "門檻以字素計");

        // 渲染鎖：4 字堂號（每格 2 個 0.6cm 全形字塞 0.7cm 欄寬）必須真的印出來。
        // 這是 gotchas 第 1 條「QuestPDF 窄欄靜默丟字」的高風險組合，既有堂號測試只用單字「甲」「堂」，
        // 完全沒覆蓋到；若被丟字，PDF 會跟無堂號版一樣大。
        static TabletData Build(string? first, string? second) => new(
            Number: "郵1", HallNameFirst: first, HallNameSecond: second,
            DeadNames: N("蔡姓歷代祖先"), LivingNames: N("蔡渭水", "蔡慧明"),
            ParaFontSizeCm: 0.8, Template: TabletTemplate.OneTwo);

        var fourChar = new TabletRenderer().Render(Build("慈光", "普照"));
        var empty = new TabletRenderer().Render(Build(null, null));
        ShouldBePdf(fourChar);
        fourChar.Length.Should().BeGreaterThan(empty.Length, "4 字堂號必須真的渲染（非被 QuestPDF 靜默丟字）");
        DumpIfRequested(fourChar, "tablet_hallname_4chars.pdf");
        DumpIfRequested(new TabletRenderer().Render(Build("慈光", "普照"), debugOverlay: true), "tablet_hallname_4chars_overlay.pdf");
        DumpIfRequested(new TabletRenderer().Render(Build("慈", "堂")), "tablet_hallname_2chars.pdf");
    }

    // 2026-08-14 使用者指定「往生 2 位時，名字間距多 0.2cm」的座標鎖。
    // 這個值只出現在 DrawDeadNames 的 rightX/leftX 計算裡、渲染後無法反推，故直接鎖常數 + 實體邊界
    // （比照 Tablet_OneAndTwoLiving_Coordinates_StayWithinPhysicalBounds 的寫法）。
    [Fact]
    public void Tablet_TwoDead_ColumnGap_WidenedAndStaysWithinWindow()
    {
        TabletRenderer.DeadColumnGap.Should().BeApproximately(0.1 + 0.2, 1e-9, "使用者指定 +0.2cm");

        // 最大字級 0.8cm（未觸發長名門檻）時的左右極值，含 GlobalShiftX
        const double fontCm = 0.8;
        const double windowInnerLeft = 4.191;   // 牌位雕花窗框內緣（樣板量測）
        const double windowInnerRight = 7.163;

        var rightEdge = TabletRenderer.DeadCenterX + TabletRenderer.DeadColumnGap / 2
                        + TabletRenderer.GlobalShiftX + fontCm;
        var leftEdge = TabletRenderer.DeadCenterX - TabletRenderer.DeadColumnGap / 2 - fontCm
                       + TabletRenderer.GlobalShiftX;

        rightEdge.Should().BeApproximately(6.435, 1e-9);
        leftEdge.Should().BeApproximately(4.535, 1e-9);
        rightEdge.Should().BeLessThan(windowInnerRight, "右欄不得壓到窗框雕花");
        leftEdge.Should().BeGreaterThan(windowInnerLeft, "左欄不得壓到窗框雕花");

        // 渲染鎖：加寬後兩欄都還要真的印出來（不被 QuestPDF 靜默丟字）
        var data = new TabletData(
            Number: "郵1", HallNameFirst: null, HallNameSecond: null,
            DeadNames: N("蔡渭水", "蔡慧明"), LivingNames: N("子甲", "子乙"),
            ParaFontSizeCm: 0.8, Template: TabletTemplate.TwoTwo);
        var pdf = new TabletRenderer().Render(data);
        ShouldBePdf(pdf);
        DumpIfRequested(pdf, "tablet_2dead_widened_gap.pdf");
        DumpIfRequested(new TabletRenderer().Render(data, debugOverlay: true), "tablet_2dead_widened_gap_overlay.pdf");
    }

    // 2026-08-14 使用者指定「3、4、5、6 位字體大小都跟目前 4 位一樣大」的單元鎖：
    // MatrixLayoutNoShrink 任何字數都不縮，但下排起點仍動態跟著上排最長名字走。
    [Fact]
    public void MatrixLayoutNoShrink_NeverShrinks_ButKeepsDynamicBottomOffset()
    {
        // 與 MatrixLayout_ShrinksUniformly_OnlyWhenChainOverflows 同一組輸入，對照兩者差異
        var (f1, off1) = VerticalText.MatrixLayoutNoShrink(0.6, ("蔡姓歷代祖先", "蔡黃氏"));
        f1.Should().BeApproximately(0.6, 1e-9, "鏈高 10 單位 > 9 也不縮（MatrixLayout 會縮到 0.54）");
        off1.Should().BeApproximately((6 + 1) * 0.6, 1e-9, "下排起點＝上排最長 6 字 +1 間距，字級用未縮的 0.6");
        VerticalText.MatrixLayout(0.6, 5.4, ("蔡姓歷代祖先", "蔡黃氏")).FontCm
            .Should().BeLessThan(f1, "前提：同一組輸入下舊公式確實會縮");

        // 沒有下排 → 無下排位移；超長單欄同樣不縮
        var (f2, off2) = VerticalText.MatrixLayoutNoShrink(0.6, ("蔡渭水", null), ("蔡慧明", ""));
        f2.Should().BeApproximately(0.6, 1e-9);
        off2.Should().Be(0);

        var (f3, off3) = VerticalText.MatrixLayoutNoShrink(0.6, (string.Concat(Enumerable.Repeat("蔡", 20)), null));
        f3.Should().BeApproximately(0.6, 1e-9, "單欄 20 字也不縮");
        off3.Should().Be(0);

        // 空矩陣 → 字級原樣回傳、無位移
        var (f4, off4) = VerticalText.MatrixLayoutNoShrink(0.6, (null, null), ("", ""));
        f4.Should().BeApproximately(0.6, 1e-9);
        off4.Should().Be(0);

        // 增補平面造字仍算一列（gotchas「一個字不等於一個 char」）
        VerticalText.MatrixLayoutNoShrink(0.6, (Rare + "姓歷代祖先", "蔡黃氏"))
            .Should().Be(VerticalText.MatrixLayoutNoShrink(0.6, ("蔡姓歷代祖先", "蔡黃氏")));
    }

    // MatrixLayout 單元行為鎖：塞不下時整組等比縮、單欄超長也不超框、空欄不影響。
    // ⚠️ 薦牌**往者**矩陣自 2026-08-14 起改走 MatrixLayoutNoShrink；本鎖涵蓋的是仍在用 MatrixLayout 的
    //    薦牌陽上／資料卡／文牒。
    [Fact]
    public void MatrixLayout_ShrinksUniformly_OnlyWhenChainOverflows()
    {
        // 上排 6 字 + 間距 + 下排 3 字 = 10 單位 > 5.4/0.6=9 → 縮到 5.4/10
        var (f1, off1) = VerticalText.MatrixLayout(0.6, 5.4, ("蔡姓歷代祖先", "蔡黃氏"));
        f1.Should().BeApproximately(5.4 / 10, 1e-9);
        off1.Should().BeApproximately((6 + 1) * (5.4 / 10), 1e-9);

        // 沒有下排 → 不受鏈限制，單欄 3 字塞得下 → 保持 0.6，且無下排位移
        var (f2, off2) = VerticalText.MatrixLayout(0.6, 5.4, ("蔡渭水", null), ("蔡慧明", ""));
        f2.Should().BeApproximately(0.6, 1e-9);
        off2.Should().Be(0);

        // 「無下排配對的超長單欄」不把下排推低：下排位移只看有配對的欄
        var (f3, off3) = VerticalText.MatrixLayout(0.6, 5.4, ("蔡姓歷代祖先七八", null), ("蔡大", "蔡二"));
        f3.Should().BeApproximately(0.6, 1e-9, "8 字單欄 4.8 ≤ 5.4、配對鏈 2+1+2=5 也塞得下 → 不縮");
        off3.Should().BeApproximately((2 + 1) * 0.6, 1e-9, "下排位移以「有下排配對的上排最長字數」計，不受無配對長欄影響");
    }

    // 開發用列印位置檢視工具：debugOverlay 疊薦牌樣板照片（reference/template/薦牌.jpg），可與
    // debugGrid 同時開（疊圖 + 格線一起看）。涵蓋 OneOne（有 2cm page margin，疊圖要對齊內容區
    // 21.4cm 高而非整張紙 25.4cm）與其餘變體（無 margin，整張紙 25.4cm）兩種尺寸分支。
    [Theory]
    [InlineData(TabletTemplate.OneOne)]
    [InlineData(TabletTemplate.Base)]
    public void Tablet_DebugOverlay_DumpsCalibrationPdf(TabletTemplate template)
    {
        var data = new TabletData(
            Number: "郵1", HallNameFirst: null, HallNameSecond: null,
            DeadNames: N("蔡姓歷代祖先", "蔡黃氏"),
            LivingNames: N("蔡渭水", "蔡慧明", "蔡碧英"),
            ParaFontSizeCm: 0.8, Template: template);

        var plain = new TabletRenderer().Render(data);
        ShouldBePdf(plain);

        var overlay = new TabletRenderer().Render(data, debugOverlay: true);
        ShouldBePdf(overlay);
        overlay.Length.Should().BeGreaterThan(plain.Length, "樣板疊圖必須真的畫出來，不是被忽略的參數");
        DumpIfRequested(overlay, $"tablet_debug_overlay_{template}.pdf");

        var both = new TabletRenderer().Render(data, debugGrid: true, debugOverlay: true);
        ShouldBePdf(both);
        DumpIfRequested(both, $"tablet_debug_overlay_grid_{template}.pdf");
    }

    // 開發用列印位置檢視工具：debugOverlay 疊文牒樣板照片（reference/template/文牒.jpg）。
    [Fact]
    public void Text_DebugOverlay_DumpsCalibrationPdf()
    {
        var data = new TextData(
            Number: "信1", HallNameFirst: "甲", HallNameSecond: "堂",
            DeadNames: N("亡一", "亡二", "亡三"), LivingNames: N("陽一", "陽二"),
            Address: "台北市中山區民族東路161號5樓", Template: TextTemplate.Base);

        var plain = new TextRenderer().Render(data);
        ShouldBePdf(plain);

        var overlay = new TextRenderer().Render(data, debugOverlay: true);
        ShouldBePdf(overlay);
        overlay.Length.Should().BeGreaterThan(plain.Length, "樣板疊圖必須真的畫出來，不是被忽略的參數");
        DumpIfRequested(overlay, "text_debug_overlay.pdf");

        // 2026-07-27 客訴複驗用：滿版 6 位（字級是否仍 0.8cm、下排是否被 MatrixLayout 推到不重疊）
        // 與 2 位變體（最左欄是否同樣離預印字 0.5cm）各疊一份。
        DumpIfRequested(new TextRenderer().Render(data with
        {
            DeadNames = N("亡甲", "亡乙", "亡丙", "亡丁", "亡戊", "亡己"),
            LivingNames = N("陽子", "陽丑", "陽寅", "陽卯", "陽辰", "陽巳"),
        }, debugOverlay: true), "text_debug_overlay_six.pdf");
        DumpIfRequested(new TextRenderer().Render(data with
        {
            DeadNames = N("亡甲", "亡乙"),
            Template = TextTemplate.Two,
        }, debugOverlay: true), "text_debug_overlay_two.pdf");
    }

    // 設 CEREMONY_PDF_DUMP=<dir> 時把 PDF 寫出供 pdftotext 對位驗收；未設則不落地（CI 純記憶體）。
    private static void DumpIfRequested(byte[] pdf, string name)
    {
        var dir = Environment.GetEnvironmentVariable("CEREMONY_PDF_DUMP");
        if (!string.IsNullOrEmpty(dir)) System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name), pdf);
    }

    [Theory]
    [InlineData(WorshipTemplate.One)]
    [InlineData(WorshipTemplate.Two)]
    [InlineData(WorshipTemplate.Three)]
    [InlineData(WorshipTemplate.Four)]
    [InlineData(WorshipTemplate.Five)]
    [InlineData(WorshipTemplate.Base)]
    public void Worship_WithBackground_RendersPdf(WorshipTemplate template)
    {
        var pdf = new WorshipRenderer().Render(new WorshipData(
            Number: "普1", LivingNames: N("陽一", "陽二", "陽三", "陽四", "陽五", "陽六"), Template: template));
        ShouldBePdf(pdf);
    }

    // 2026-07-04 回歸鎖：普桌陽上姓名曾被 QuestPDF 靜默丟字（3cm 字塞 2.2cm 欄寬，One/Two/Three
    // 變體整欄消失，PDF 只剩 Number）。改用 VerticalText.Stack 顯式直書後，「有姓名」的 PDF 必須
    // 真的比「無姓名」多出內容——若又被靜默丟字，兩者位元組數會相同。逐一鎖 6 個變體。
    [Theory]
    [InlineData(WorshipTemplate.One)]
    [InlineData(WorshipTemplate.Two)]
    [InlineData(WorshipTemplate.Three)]
    [InlineData(WorshipTemplate.Four)]
    [InlineData(WorshipTemplate.Five)]
    [InlineData(WorshipTemplate.Base)]
    public void Worship_LivingNames_AreNotSilentlyDropped(WorshipTemplate template)
    {
        var withNames = new WorshipRenderer().Render(new WorshipData(
            Number: "普1", LivingNames: N("陳大明", "林小華", "張三豐", "李四端", "王五福", "趙六順"), Template: template));
        var withoutNames = new WorshipRenderer().Render(new WorshipData(
            Number: "普1", LivingNames: N(), Template: template));

        ShouldBePdf(withNames);
        withNames.Length.Should().BeGreaterThan(withoutNames.Length,
            "陽上姓名必須真的渲染出來（若被 QuestPDF 靜默丟字，PDF 會跟無姓名版一樣大）");
    }

    // 2026-07-04 客戶樣張 reference/普桌.jpg 全情境（普595–600）：1 位 7 字、2 位 7 字、3 位三角、
    // 4 位 2×2、5 位上2下3、6 位矩陣「各容納5個字」（含闔家型態，觸發 5字+上下排空格=6列 的縮字）。
    // 用 CEREMONY_PDF_DUMP 落地供目視比對樣張排版。
    [Fact]
    public void Worship_CustomerSampleScenarios_DumpCalibrationPdfs()
    {
        var r = new WorshipRenderer();
        var cases = new (string Name, WorshipData Data)[]
        {
            ("worship_one_7chars.pdf", new WorshipData("普595", N("一二三四五六七"), WorshipTemplate.One)),
            ("worship_two_7chars.pdf", new WorshipData("普596", N("一二三四五六七", "一二三四五六七"), WorshipTemplate.Two)),
            ("worship_three_triangle.pdf", new WorshipData("普597", N("一二三四五六", "一二三四", "一二三四"), WorshipTemplate.Three)),
            ("worship_four_2x2.pdf", new WorshipData("普598", N("一二三四五", "一二三四五", "一二三四五", "一二三四五"), WorshipTemplate.Four)),
            ("worship_five_2plus3.pdf", new WorshipData("普599", N("一二三四五", "一二三四五", "一二三四五", "一二三四五", "一二三四五"), WorshipTemplate.Five)),
            ("worship_base_5chars_gap.pdf", new WorshipData("普600", N("王大明闔家", "林小華闔家", "張三豐闔家", "李四端闔家", "王五福闔家", "趙六順闔家"), WorshipTemplate.Base)),
        };
        foreach (var (name, data) in cases)
        {
            var pdf = r.Render(data);
            ShouldBePdf(pdf);
            DumpIfRequested(pdf, name);
        }
    }

    [Theory]
    [InlineData(WorshipTemplate.One)]
    [InlineData(WorshipTemplate.Two)]
    [InlineData(WorshipTemplate.Three)]
    [InlineData(WorshipTemplate.Four)]
    [InlineData(WorshipTemplate.Five)]
    [InlineData(WorshipTemplate.Base)]
    public void WorshipCard_AllVariants_RenderPdf(WorshipTemplate template)
    {
        var pdf = new WorshipCardRenderer().Render(new WorshipCardData(
            Number: "普1", LivingNames: N("陽一", "陽二", "陽三", "陽四", "陽五", "陽六"),
            Template: template, Phone: "02-12345678", Remark: "素食一桌"));
        ShouldBePdf(pdf);
    }

    // 普桌資料卡的直書姓名沿用 WorshipRenderer 的 VerticalText.Stack 慣例；比照普桌
    // Worship_LivingNames_AreNotSilentlyDropped 的回歸鎖，防 QuestPDF 靜默丟字（見該測試注解）。
    [Theory]
    [InlineData(WorshipTemplate.One)]
    [InlineData(WorshipTemplate.Two)]
    [InlineData(WorshipTemplate.Three)]
    [InlineData(WorshipTemplate.Four)]
    [InlineData(WorshipTemplate.Five)]
    [InlineData(WorshipTemplate.Base)]
    public void WorshipCard_LivingNames_AreNotSilentlyDropped(WorshipTemplate template)
    {
        var withNames = new WorshipCardRenderer().Render(new WorshipCardData(
            Number: "普1", LivingNames: N("陳大明", "林小華", "張三豐", "李四端", "王五福", "趙六順"),
            Template: template, Phone: null, Remark: null));
        var withoutNames = new WorshipCardRenderer().Render(new WorshipCardData(
            Number: "普1", LivingNames: N(), Template: template, Phone: null, Remark: null));

        ShouldBePdf(withNames);
        withNames.Length.Should().BeGreaterThan(withoutNames.Length,
            "陽上姓名必須真的渲染出來（若被 QuestPDF 靜默丟字，PDF 會跟無姓名版一樣大）");
    }

    [Fact]
    public void WorshipCard_PhoneAndRemark_AreRendered()
    {
        var bare = new WorshipCardRenderer().Render(new WorshipCardData(
            Number: "普1", LivingNames: N("陳大明"), Template: WorshipTemplate.One, Phone: null, Remark: null));
        var full = new WorshipCardRenderer().Render(new WorshipCardData(
            Number: "普1", LivingNames: N("陳大明"), Template: WorshipTemplate.One,
            Phone: "0912-345678", Remark: "備註內容刻意寫得比較長，驗證換行不裁字也不噴例外"));

        ShouldBePdf(full);
        full.Length.Should().BeGreaterThan(bare.Length, "電話/備註必須真的渲染出來");
    }

    // 2026-07-18 客訴回歸鎖（比照 DataCard_EmptyContent_StillPrintsTemplate）：普桌資料卡要連
    // template 一起印（葫蘆輪廓/右側標題/簽名底線），白紙可印，防退回「預印卡紙只套印內容」模式。
    [Fact]
    public void WorshipCard_EmptyContent_StillPrintsTemplate()
    {
        var pdf = new WorshipCardRenderer().Render(new WorshipCardData(
            Number: "", LivingNames: N(), Template: WorshipTemplate.Base, Phone: null, Remark: null));
        ShouldBePdf(pdf);
        pdf.Length.Should().BeGreaterThan(60_000,
            "template（worship2 葫蘆線稿 64KB + 標題文字/簽名底線）必須在無內容時也被繪製");
        DumpIfRequested(pdf, "worshipcard_template_only.pdf");
    }

    // 普桌 6 變體全情境（比照 Worship_CustomerSampleScenarios），含 debugOverlay 樣板疊圖版，
    // 用 CEREMONY_PDF_DUMP 落地到 reference/output/ 供開發者/使用者對位檢視。
    [Fact]
    public void WorshipCard_CustomerScenarios_DumpCalibrationPdfs()
    {
        var r = new WorshipCardRenderer();
        var cases = new (string Name, WorshipCardData Data)[]
        {
            ("worshipcard_one_7chars", new WorshipCardData("普595", N("一二三四五六七"), WorshipTemplate.One, "02-12345678", "素食一桌")),
            ("worshipcard_two_7chars", new WorshipCardData("普596", N("一二三四五六七", "一二三四五六七"), WorshipTemplate.Two, "0912-345678", null)),
            ("worshipcard_three_triangle", new WorshipCardData("普597", N("一二三四五六", "一二三四", "一二三四"), WorshipTemplate.Three, "02-2345678", "備註測試")),
            ("worshipcard_four_2x2", new WorshipCardData("普598", N("一二三四五", "一二三四五", "一二三四五", "一二三四五"), WorshipTemplate.Four, null, null)),
            ("worshipcard_five_2plus3", new WorshipCardData("普599", N("一二三四五", "一二三四五", "一二三四五", "一二三四五", "一二三四五"), WorshipTemplate.Five, "02-12345678", "備註內容刻意寫得比較長，驗證右側欄位換行後不會壓到簽名區")),
            ("worshipcard_base_5chars_gap", new WorshipCardData("普600", N("王大明闔家", "林小華闔家", "張三豐闔家", "李四端闔家", "王五福闔家", "趙六順闔家"), WorshipTemplate.Base, "02-12345678", "全素")),
        };
        foreach (var (name, data) in cases)
        {
            var plain = r.Render(data);
            ShouldBePdf(plain);
            DumpIfRequested(plain, $"{name}.pdf");

            var overlay = r.Render(data, debugOverlay: true);
            ShouldBePdf(overlay);
            overlay.Length.Should().BeGreaterThan(plain.Length, "樣板疊圖必須真的畫出來，不是被忽略的參數");
            DumpIfRequested(overlay, $"{name}_overlay.pdf");
        }
    }

    [Fact]
    public void Skia_VerticalAddress_ProducesPng()
    {
        var png = SkiaImageHelpers.VerticalAddress("台北市中山區ABC-12號");
        png.Should().NotBeNullOrEmpty();
        // PNG magic number
        png[0].Should().Be(0x89);
        Encoding.ASCII.GetString(png, 1, 3).Should().Be("PNG");
    }

    // 2026-07-18 使用者指定：地址超過單欄容量（~23 字）折兩欄，第二欄接左邊（直書右欄先讀）。
    // 鎖 canvas 寬度：短地址單欄（27px）、長地址兩欄（27*2+9px）；並驗證兩欄時左半邊真的有墨
    // （避免「canvas 加寬了但字全擠在右欄」的靜默退化）。
    [Fact]
    public void Skia_VerticalAddress_LongAddress_WrapsToSecondColumnOnLeft()
    {
        using var one = SkiaSharp.SKBitmap.Decode(SkiaImageHelpers.VerticalAddress("台北市中山區民族東路161號5樓"));
        one.Width.Should().Be(SkiaImageHelpers.AddressColWidthPx, "短地址維持單欄");

        var longAddr = "南投縣竹山鎮延平里集山路三段1234巷56號7樓之2第五公寓"; // 29 字 > 單欄容量
        longAddr.Length.Should().BeGreaterThan(SkiaImageHelpers.AddressCharsPerColumn);
        using var two = SkiaSharp.SKBitmap.Decode(SkiaImageHelpers.VerticalAddress(longAddr));
        two.Width.Should().Be(SkiaImageHelpers.AddressColWidthPx * 2 + SkiaImageHelpers.AddressColGapPx);

        var leftInk = false;
        for (var x = 0; x < SkiaImageHelpers.AddressColWidthPx && !leftInk; x++)
            for (var y = 0; y < two.Height; y++)
                if (two.GetPixel(x, y).Alpha != 0) { leftInk = true; break; }
        leftInk.Should().BeTrue("折行的後半段字必須畫在左欄");
    }

    // 客戶反映（reference/文牒問題.pdf 手寫註記）文牒垂直地址列印偏灰，要求「再黑一點」。
    // 根因：抗鋸齒邊緣像素在 25px 窄欄小字級下佔比高，視覺變淡灰。改 Edging=Alias +
    // IsAntialias=false 後，每個有畫到的像素理應為純黑（alpha=255 的像素其 RGB 必為 0,0,0），
    // 不應再有「部分透明的灰邊」像素。用像素掃描鎖住這個不重來。
    [Fact]
    public void Skia_VerticalAddress_NoAntiAliasedGrayEdges()
    {
        var png = SkiaImageHelpers.VerticalAddress("台北市中山區金山南路一段63巷1號1F");
        using var bitmap = SkiaSharp.SKBitmap.Decode(png);

        var hasInk = false;
        for (var x = 0; x < bitmap.Width; x++)
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (p.Alpha == 0) continue; // 透明背景
                hasInk = true;
                // 有畫到的像素必須是全黑（不可是抗鋸齒留下的灰階邊緣）
                p.Red.Should().Be(0);
                p.Green.Should().Be(0);
                p.Blue.Should().Be(0);
            }
        }
        hasInk.Should().BeTrue("地址字要有實際畫到黑色像素，不能整張空白");
    }

    // 客戶反映（reference/文牒問題.pdf 手寫註記）文牒「往生」姓名字級要跟「陽上」一樣大。
    // 往生／陽上共用同一 0.8cm 基準、各自獨立計算安全字級：典型資料（本例即取自該 PDF 上的姓名）
    // 兩組都不需縮字，自然都維持 0.8cm、視覺一致，不需要額外的跨組對齊邏輯。
    [Fact]
    public void Text_DeadAndLivingFontSizes_MatchWhenNeitherNeedsShrinking()
    {
        var dumpDir = Environment.GetEnvironmentVariable("CEREMONY_PDF_DUMP");
        var data = new TextData(
            Number: "郵1", HallNameFirst: null, HallNameSecond: null,
            DeadNames: N("蔡姓歷代祖先", "蔡黃氏"),
            LivingNames: N("蔡渭水", "蔡慧明", "蔡碧英"),
            Address: "台灣台北市大安區金山南路一段63巷1號1F", Template: TextTemplate.Base);

        var pdf = new TextRenderer().Render(data);
        ShouldBePdf(pdf);
        if (!string.IsNullOrEmpty(dumpDir))
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dumpDir, "text_mail1_sample.pdf"), pdf);
    }

    // 反過來：往生那組字數多到需要縮字時，只縮往生自己，**陽上不會被拖著一起縮小**
    // （2026-07-02 second-guess：曾經加過跨組取最小值對齊兩組，會導致陽上被往生拖累而
    // 意外變小，客戶反映後撤回；見 docs/gotchas.md「往生字級被拖累」條）。
    // 主欄可用高固定 10.50374cm（無第 6 位、下方為空 → 整欄高）；14 字時 10.50374/14≈0.750cm
    // < 0.8cm，確實會觸發往生縮字；陽上只有 1 個短名、avail 充足，理應仍是舊字級 0.8cm。
    [Fact]
    public void Text_DeadNameShrinks_WithoutDraggingDownLivingName()
    {
        var crowded = string.Concat(Enumerable.Repeat("蔡", 14)); // 14 字，超過主欄可用高的字級門檻
        var data = new TextData(
            Number: "郵1", HallNameFirst: null, HallNameSecond: null,
            DeadNames: N(crowded), LivingNames: N("蔡"),
            Address: "台北市", Template: TextTemplate.Base);

        var pdf = new TextRenderer().Render(data);
        ShouldBePdf(pdf);

        var (deadFont, _) = VerticalText.MatrixLayout(0.8, 10.50374, (crowded, null));
        deadFont.Should().BeApproximately(10.50374 / 14, 1e-6,
            "14 字超出主欄可用高／0.8cm 門檻，DeadName 自身安全字級應縮小");

        var (livingFont, _) = VerticalText.MatrixLayout(0.8, 6.72806, ("蔡", null));
        livingFont.Should().BeApproximately(0.8, 1e-6,
            "陽上只有 1 個短名、可用高遠超所需，即使往生同一頁被迫縮字，陽上仍應維持舊字級 0.8cm，不受影響");
    }

    // 2026-07-27 客訴（1）：「陽上跟往者不管幾位，字體大小都跟一位一樣」。
    // 回歸鎖：舊做法（固定列距 + WithBottomGap + GroupFontPt 以列距當可用高）在上下排都有名字時，
    // 往者被限在列距 2.06375cm、陽上 1.98436cm 內 → 3 字名 +1 間距 = 4 列，字級掉到 0.50cm 左右。
    // 改用 MatrixLayout 後，字級只受「整欄高」節制（往者 10.50374 / 陽上 6.72806），
    // 3 字名的整欄鏈只要 3+1+3=7 列 × 0.8 = 5.6cm，兩個框都塞得下 → 1~6 位一律維持 0.8cm。
    [Fact]
    public void Text_NameFontSize_StaysAtBase_RegardlessOfNameCount()
    {
        const double deadBox = 10.50374, livingBox = 6.72806;
        const double baseFont = TextRenderer.NameBaseFontCm;

        for (var count = 1; count <= 6; count++)
        {
            var n = N(Enumerable.Range(0, count).Select(i => $"陳大{i}").ToArray<string?>());

            var (deadFont, deadOffset) = VerticalText.MatrixLayout(
                baseFont, deadBox, (n[0], n[5]), (n[1], n[3]), (n[2], n[4]));
            deadFont.Should().BeApproximately(baseFont, 1e-6, $"{count} 位往者字級要跟 1 位一樣大");

            var (livingFont, livingOffset) = VerticalText.MatrixLayout(
                baseFont, livingBox, (n[0], n[5]), (n[1], n[3]), (n[2], n[4]));
            livingFont.Should().BeApproximately(baseFont, 1e-6, $"{count} 位陽上字級要跟 1 位一樣大");

            // 下排（第 4 位起）才需要動態起點；起點 = 上排最長字數 +1 個字高間距
            var expectedOffset = count >= 4 ? 4 * baseFont : 0;
            deadOffset.Should().BeApproximately(expectedOffset, 1e-6);
            livingOffset.Should().BeApproximately(expectedOffset, 1e-6);

            // 整欄鏈（上排 + 間距 + 下排）不可超出方框
            (deadOffset + 3 * deadFont).Should().BeLessThan(deadBox);
            (livingOffset + 3 * livingFont).Should().BeLessThan(livingBox);
        }
    }

    // 2026-07-27 客訴（2）：「往者要跟左邊文牒本來的文字距離 0.5 公分，堂號也是同樣對齊」。
    // 量測（reference/template/文牒.jpg 逐欄墨跡掃描，姓名帶 y 3.6~14cm）：往者左側最近的預印字欄
    // 「鳴呼既追攀之無從…」x 10.558~11.207cm → 最左往者欄左緣必須是 11.207+0.5=11.707。
    // 回歸鎖：防止再度退回 2026-07-21 的「整體右移 0.5cm」（DeadShiftX，實際間距變 0.793cm）。
    [Fact]
    public void Text_DeadNamesAndHallName_KeepHalfCentimeterFromPrePrintedText()
    {
        TextRenderer.DeadLeftX.Should().BeApproximately(11.707, 1e-6);
        (TextRenderer.DeadLeftX - TextRenderer.PrePrintLeftTextRightX)
            .Should().BeApproximately(0.5, 1e-6, "往者最左欄離左側預印字要 0.5cm");

        // 堂號左字（Second）與往者最左欄同一條線；右字（First）維持 RDLC 相對距 2.03753cm
        TextRenderer.HallSecondX.Should().BeApproximately(TextRenderer.DeadLeftX, 1e-6, "堂號要跟往者同樣對齊");
        (TextRenderer.HallFirstX - TextRenderer.HallSecondX).Should().BeApproximately(2.03753, 1e-6);

        // 欄與欄的相對距離維持 RDLC 原值（Base 欄距 0.91251；Two 變體兩高欄相距 1.16299）
        (TextRenderer.DeadMidX - TextRenderer.DeadLeftX).Should().BeApproximately(12.41251 - 11.5, 1e-6);
        (TextRenderer.DeadRightX - TextRenderer.DeadLeftX).Should().BeApproximately(13.32502 - 11.5, 1e-6);
        (TextRenderer.DeadTwoRightX - TextRenderer.DeadLeftX).Should().BeApproximately(13.01299 - 11.85, 1e-6);

        // 最右欄右緣（0.8cm 字寬）不可壓到右側預印字（「等切念」欄 x 13.436~13.971 在 y 22cm 以下，
        // 姓名帶內無預印字；仍以 14.5cm 當硬界限，避免未來調整時無聲越界）
        (TextRenderer.DeadRightX + TextRenderer.NameBaseFontCm).Should().BeLessThan(14.5);
    }

    [Fact]
    public void Skia_DashedLine_ProducesPng()
    {
        var png = SkiaImageHelpers.DashedLine(15.434);
        png.Should().NotBeNullOrEmpty();
        png[0].Should().Be(0x89);
    }

    // ── 造字（Unicode 增補平面罕用字）回歸鎖 ────────────────────────────────────────
    // 2026-08-04 客訴「舊系統的造字印得出來，新系統只有收據、資料卡陽上正常，其他都亂碼」。
    // 現場姓名含 CJK Ext-B（U+20000 以上）罕用字，UTF-16 佔兩個 char；直書路徑原本逐 char 走訪
    // → surrogate pair 被拆成兩個孤兒碼位（豆腐／空白），且列數多算一列連帶縮錯字級。
    // 以下全部以「字素」為單位，任何一條掉了就是同一個 bug 回來了。見 docs/gotchas.md。

    private const string Rare = "\U0002135C";   // 𡍼 U+2135C，dev DB Signups.Name / Believers.DeadName* 實有

    [Fact]
    public void Stack_KeepsSupplementaryPlaneCharIntact()
    {
        var lines = VerticalText.Stack(Rare + "家歷").Split('\n');
        lines.Should().Equal([Rare, "家", "歷"], "增補平面字是一個字＝一列，不可被拆成兩個孤兒 surrogate");
        lines[0].Should().HaveLength(2, "該字素本身仍是完整的 surrogate pair（兩個 char）");
    }

    [Theory]
    [InlineData("\U0002135C家歷", 3)]
    [InlineData("\U0002135C\U000241AC", 2)]
    [InlineData("　\U0002135C家", 3)]           // 開頭全形空格照算一列（既有規則）
    [InlineData("陳明", 2)]
    public void ElementCount_CountsWhatTheEyeSees(string name, int expected)
        => VerticalText.ElementCount(name).Should().Be(expected);

    // 列數是**字級的輸入**：多算一列會讓整組字級被縮小，牌位/文牒的字會無故變小。
    [Fact]
    public void GroupFontPt_CountsSupplementaryCharAsOneRow()
    {
        var rare = VerticalText.GroupFontPt(0.6 * PtPerCm, (Rare + "家歷", 1.5));
        var plain = VerticalText.GroupFontPt(0.6 * PtPerCm, ("陳家歷", 1.5));
        rare.Should().BeApproximately(plain, 1e-9, "𡍼家歷 與 陳家歷 同為 3 列，字級必須一樣");
        (rare / PtPerCm).Should().BeApproximately(1.5 / 3, 1e-6);
    }

    [Fact]
    public void MatrixLayout_CountsSupplementaryCharAsOneRow()
    {
        var rare = VerticalText.MatrixLayout(0.6, 5.4, (Rare + "姓歷代祖先", "蔡黃氏"));
        var plain = VerticalText.MatrixLayout(0.6, 5.4, ("蔡姓歷代祖先", "蔡黃氏"));
        rare.Should().Be(plain, "字素數相同 → 字級與下排起點必須完全一致");
    }

    // 折欄門檻也吃字數：以 .Length 計會讓含造字的地址提早折成兩欄（帶寬跟著變 → 版面位移）。
    [Fact]
    public void AddressColumns_CountsSupplementaryCharAsOneChar()
    {
        var capacity = SkiaImageHelpers.AddressCharsPerColumn;
        var addr = Rare + new string('號', capacity - 1);       // 恰好塞滿單欄的字素數
        VerticalText.ElementCount(addr).Should().Be(capacity);
        SkiaImageHelpers.AddressColumns(addr).Should().Be(1, "字素數沒超過容量就不該折兩欄");
    }

    // 文牒直書地址走 SkiaSharp canvas.DrawText（單一 typeface，**沒有**缺字 fallback）：
    // 修好 surrogate 之後，標楷體若沒有該字仍會整字畫不出來（留空白）。這條鎖住 fallback 有生效。
    [Fact]
    public void Skia_VerticalAddress_DrawsSupplementaryPlaneChar()
    {
        using var bitmap = SkiaSharp.SKBitmap.Decode(SkiaImageHelpers.VerticalAddress(Rare + "北市"));

        // 第一個字素佔的橫帶（頂端到 1/4 高的第一格）必須有墨；沒有＝字被吃掉了
        var rowHeight = bitmap.Height / (double)SkiaImageHelpers.AddressCharsPerColumn;
        var inkInFirstRow = 0;
        for (var y = 0; y < (int)rowHeight; y++)
            for (var x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).Alpha != 0) inkInFirstRow++;

        inkInFirstRow.Should().BeGreaterThan(0, "增補平面字必須畫得出來（標楷體沒有就要 fallback 到有的字型）");
    }

    // 端到端：六種報表都吃得下含造字的姓名/地址（不丟例外、產得出 PDF）。
    [Fact]
    public void AllRenderers_AcceptSupplementaryPlaneNames()
    {
        var name = Rare + "家歷代祖先";
        var addr = "南投縣竹山鎮" + Rare + "路1號";

        ShouldBePdf(new ReceiptRenderer().Render(new ReceiptData(name, "557", addr, "500", "A001", "", "115", "8", "4")));
        ShouldBePdf(new TabletRenderer().Render(new TabletData("A001", null, null, N(name), N(name), 0.6, TabletTemplate.Base)));
        ShouldBePdf(new TextRenderer().Render(new TextData("A001", null, null, N(name), N(name), addr, TextTemplate.Base)));
        ShouldBePdf(new WorshipRenderer().Render(new WorshipData("A001", N(name), WorshipTemplate.Base)));
    }
}
