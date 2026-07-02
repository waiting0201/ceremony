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
    public void DataCard_WithDashedLine_RendersPdf()
    {
        var pdf = new DataCardRenderer().Render(new DataCardData(
            Number: "信1", HallName: "甲堂", Prepay: "預繳 115 梁皇",
            DeadNames: N("陳大明", "陳二", "陳三"), LivingNames: N("陳孝", "陳順"),
            Address: "台北市中山區民族東路161號5樓", Phone: "0912345678", Remark: "無"));
        ShouldBePdf(pdf);
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
    // 實體尺寸」與「客戶現有牌位座」不一致，屬於實體對位問題，不是排版邏輯錯誤。無實測尺寸可反推
    // 修正量，故先提供 debugGrid 疊 1cm 刻度格線版本：印出後插入同一個牌位座，實測視窗上緣/下緣
    // 對到第幾條刻度線，才能算出精確修正量。回歸鎖：只驗證 debugGrid 不會撞壞既有排版、仍是有效
    // PDF；真正的座標修正待實機量測後再做（見 docs/gotchas.md「薦牌實體對位」條）。
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
