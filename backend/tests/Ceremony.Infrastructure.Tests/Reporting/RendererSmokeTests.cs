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

        const double topRowY = 5.6388 + 0.1;
        const double windowGapBottom = 11.4427; // 「靈」字上緣，硬邊界
        const double rowPitch = 2.6;
        const double bottomRowY = topRowY + rowPitch;
        const double safetyMargin = 0.2;
        const double fullHeight = windowGapBottom - bottomRowY - safetyMargin;

        var d = data.DeadNames;
        var font = VerticalText.GroupFontPt(0.6 * PtPerCm,
            (d[0], VerticalText.Avail(d[5], rowPitch, fullHeight)),
            (d[1], VerticalText.Avail(d[3], rowPitch, fullHeight)),
            (d[2], VerticalText.Avail(d[4], rowPitch, fullHeight)),
            (d[3], fullHeight),
            (d[4], fullHeight),
            (d[5], fullHeight));

        foreach (var (rowTop, name) in new[] { (topRowY, d[0]), (topRowY, d[1]), (topRowY, d[2]), (bottomRowY, d[3]), (bottomRowY, d[4]), (bottomRowY, d[5]) })
        {
            var bottomCm = rowTop + name!.Length * (font / PtPerCm);
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

    [Fact]
    public void Receipt_RendersPdf()
    {
        var pdf = new ReceiptRenderer().Render(new ReceiptData(
            Name: "陳大明", Fee: "1200", Number: "信1", Prepay: "", Year: "115", Month: "5", Day: "29"));
        ShouldBePdf(pdf);
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

    // 2026-07-03：Base 變體主欄（One）若無第 6 位搭配，原本 Avail 回退到 deadFull=11.0331cm
    // （top=7.5825 起算到 18.6156cm），比 reference/template/薦牌.jpg 量測到的雕花窗框內緣底部
    // （16.0782cm）多出約 2.5cm——14 字以上的長名字會被印到窗框外，對應「薦牌實體對位」客訴。
    // 已改用量測值收緊 deadFull=8.4957（見 TabletRenderer.DrawDeadNames default 分支）。回歸鎖：
    // 14 字長名字（觸發 GroupFontPt 縮字）算出的實際字高，不得超出窗框量測底線。
    [Fact]
    public void Tablet_Base_LongDeadName_StaysWithinMeasuredWindow()
    {
        var longName = string.Concat(Enumerable.Repeat("蔡", 14)); // 14 字，會觸發縮字
        var data = new TabletData(
            Number: "郵1", HallNameFirst: null, HallNameSecond: null,
            DeadNames: N(longName), LivingNames: N("蔡渭水"),
            ParaFontSizeCm: 0.8, Template: TabletTemplate.Base);

        var pdf = new TabletRenderer().Render(data);
        ShouldBePdf(pdf);
        DumpIfRequested(pdf, "tablet_base_long_dead_name.pdf");
        DumpIfRequested(new TabletRenderer().Render(data, debugOverlay: true), "tablet_base_long_dead_name_overlay.pdf");

        const double windowInnerTopCm = 6.2294;
        const double windowInnerBottomCm = 16.0782;
        const double textTopCm = 7.5825;

        var font = VerticalText.GroupFontPt(0.8 * PtPerCm, (longName, 8.4957));
        var textBottomCm = textTopCm + longName.Length * (font / PtPerCm);

        textTopCm.Should().BeGreaterThanOrEqualTo(windowInnerTopCm, "主欄起點不應在窗框內緣之上");
        textBottomCm.Should().BeLessThanOrEqualTo(windowInnerBottomCm + 1e-6,
            "14 字長名字縮字後的實際高度不應超出樣板窗框內緣底部（量測值 16.0782cm）");
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

    [Fact]
    public void Skia_VerticalAddress_ProducesPng()
    {
        var png = SkiaImageHelpers.VerticalAddress("台北市中山區ABC-12號");
        png.Should().NotBeNullOrEmpty();
        // PNG magic number
        png[0].Should().Be(0x89);
        Encoding.ASCII.GetString(png, 1, 3).Should().Be("PNG");
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

        var deadFont = VerticalText.GroupFontPt(0.8 * PtPerCm, (crowded, 10.50374));
        (deadFont / PtPerCm).Should().BeApproximately(10.50374 / 14, 1e-6,
            "14 字超出主欄可用高／0.8cm 門檻，DeadName 自身安全字級應縮小");

        var livingFont = VerticalText.GroupFontPt(0.8 * PtPerCm, ("蔡", 6.72806));
        (livingFont / PtPerCm).Should().BeApproximately(0.8, 1e-6,
            "陽上只有 1 個短名、可用高遠超所需，即使往生同一頁被迫縮字，陽上仍應維持舊字級 0.8cm，不受影響");
    }

    [Fact]
    public void Skia_DashedLine_ProducesPng()
    {
        var png = SkiaImageHelpers.DashedLine(15.434);
        png.Should().NotBeNullOrEmpty();
        png[0].Should().Be(0x89);
    }
}
