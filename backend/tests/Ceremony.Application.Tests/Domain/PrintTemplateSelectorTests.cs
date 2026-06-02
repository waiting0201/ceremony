using Ceremony.Domain.Services;
using FluentAssertions;

namespace Ceremony.Application.Tests.Domain;

public sealed class PrintTemplateSelectorTests
{
    private static string?[] N(params string?[] xs)
    {
        var arr = new string?[6];
        for (var i = 0; i < Math.Min(xs.Length, 6); i++) arr[i] = xs[i];
        return arr;
    }

    // === Tablet 9 variants ===

    [Fact]
    public void Tablet_1dead_1living_OneOne_para08()
    {
        var (t, p) = PrintTemplateSelector.ChooseTablet(N("陳大明"), N("子甲"));
        t.Should().Be(TabletTemplate.OneOne);
        p.Should().Be("0.8cm");
    }

    [Fact]
    public void Tablet_1deadLong_2living_OneTwo_para06()
    {
        var (t, p) = PrintTemplateSelector.ChooseTablet(N("一二三四五六七八"), N("子甲", "子乙"));
        t.Should().Be(TabletTemplate.OneTwo);
        p.Should().Be("0.6cm", "dead.Length > 7 → 0.6cm");
    }

    [Fact]
    public void Tablet_1dead_5living_One()
    {
        var (t, _) = PrintTemplateSelector.ChooseTablet(N("陳大明"), N("a", "b", "c", "d", "e"));
        t.Should().Be(TabletTemplate.One);
    }

    [Fact]
    public void Tablet_2dead_1living_TwoOne()
    {
        var (t, p) = PrintTemplateSelector.ChooseTablet(N("陳大明", "李三"), N("子甲"));
        t.Should().Be(TabletTemplate.TwoOne);
        p.Should().Be("0.8cm");
    }

    [Fact]
    public void Tablet_2dead_2living_dead2Long_para06()
    {
        var (t, p) = PrintTemplateSelector.ChooseTablet(N("陳", "一二三四五六七八"), N("子甲", "子乙"));
        t.Should().Be(TabletTemplate.TwoTwo);
        p.Should().Be("0.6cm");
    }

    [Fact]
    public void Tablet_2dead_6living_Two()
    {
        var (t, _) = PrintTemplateSelector.ChooseTablet(N("陳", "李"), N("a", "b", "c", "d", "e", "f"));
        t.Should().Be(TabletTemplate.Two);
    }

    [Fact]
    public void Tablet_3dead_1living_UnderscoreOne()
    {
        var (t, p) = PrintTemplateSelector.ChooseTablet(N("a", "b", "c"), N("子甲"));
        t.Should().Be(TabletTemplate.UnderscoreOne);
        p.Should().Be("0.6cm", "3+ 亡固定 0.6cm");
    }

    [Fact]
    public void Tablet_3dead_2living_UnderscoreTwo()
    {
        var (t, _) = PrintTemplateSelector.ChooseTablet(N("a", "b", "c"), N("x", "y"));
        t.Should().Be(TabletTemplate.UnderscoreTwo);
    }

    [Fact]
    public void Tablet_6dead_6living_Base()
    {
        var (t, _) = PrintTemplateSelector.ChooseTablet(N("a", "b", "c", "d", "e", "f"), N("1", "2", "3", "4", "5", "6"));
        t.Should().Be(TabletTemplate.Base);
    }

    // === Tablet 字長門檻：中間空格不計入（刻意排版間隙，渲染時保留但不污染字級） ===

    [Fact]
    public void Tablet_deadHalfWidthSpace_7realChars_para08()
    {
        // "一二三 四五六七" = 7 真字 + 1 半形空格；真實字數 7 ≤ 7 → 不縮
        var (_, p) = PrintTemplateSelector.ChooseTablet(N("一二三 四五六七"), N("子甲"));
        p.Should().Be("0.8cm", "中間空格不計入字長門檻，7 真字 → 0.8cm");
    }

    [Fact]
    public void Tablet_deadFullWidthSpace_7realChars_para08()
    {
        // 全形空格 U+3000；真實字數 7 → 不縮
        var (_, p) = PrintTemplateSelector.ChooseTablet(N("一二三　四五六七"), N("子甲"));
        p.Should().Be("0.8cm", "全形空格 (U+3000) 亦不計入字長門檻");
    }

    [Fact]
    public void Tablet_dead8realChars_withSpace_still_para06()
    {
        // "一二三四 五六七八" = 8 真字 + 空格；真實字數 8 > 7 → 仍縮（沒把真字誤刪）
        var (_, p) = PrintTemplateSelector.ChooseTablet(N("一二三四 五六七八"), N("子甲"));
        p.Should().Be("0.6cm", "8 真字 > 7 門檻仍觸發");
    }

    [Fact]
    public void Tablet_dead2_withSpace_7realChars_para08()
    {
        // dead2 含空格，驗證 dead2Long 也走 RealCharCount
        var (_, p) = PrintTemplateSelector.ChooseTablet(N("陳", "一二三 四五六七"), N("子甲", "子乙"));
        p.Should().Be("0.8cm", "dead2 中間空格不計入字長門檻");
    }

    // === Text 2 variants ===

    [Theory]
    [InlineData(0, "Base")]
    [InlineData(1, "Base")]
    [InlineData(2, "Two")]
    [InlineData(3, "Base")]
    [InlineData(6, "Base")]
    public void Text_only2dead_picks_Two(int deadCount, string expected)
    {
        var names = new string?[6];
        for (var i = 0; i < deadCount; i++) names[i] = $"d{i}";
        var t = PrintTemplateSelector.ChooseText(names);
        t.ToString().Should().Be(expected);
    }

    // === Worship 6 variants ===

    [Fact]
    public void Worship_6living_Base()
        => PrintTemplateSelector.ChooseWorship(N("a", "b", "c", "d", "e", "f")).Should().Be(WorshipTemplate.Base);

    [Fact]
    public void Worship_5living_Five()
        => PrintTemplateSelector.ChooseWorship(N("a", "b", "c", "d", "e", null)).Should().Be(WorshipTemplate.Five);

    [Fact]
    public void Worship_4living_Four()
        => PrintTemplateSelector.ChooseWorship(N("a", "b", "c", "d")).Should().Be(WorshipTemplate.Four);

    [Fact]
    public void Worship_1living_One()
        => PrintTemplateSelector.ChooseWorship(N("a")).Should().Be(WorshipTemplate.One);

    [Fact]
    public void Worship_sparseHighEnd_picksHighestPresent()
    {
        // 第 6 位有值 → Base，即使 2~5 是空
        PrintTemplateSelector.ChooseWorship(N("a", null, null, null, null, "f")).Should().Be(WorshipTemplate.Base);
    }
}
