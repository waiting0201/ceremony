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
        // 2026-08-08 使用者指定「往者 8 字以上字級與三位的一樣」＝回復舊系統 `.Length > 7 → 0.6cm`。
        // 是**起點**不是固定值：renderer 仍會在 0.6cm 之上依可用高再縮（12 字 → 0.47cm）。
        p.Should().Be("0.6cm", "8 字素 → 字級起點降到 0.6cm（同 3+ 位往者）");
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
        // 2 位往者：任一格達門檻就整組降起點（renderer 的 GroupFontPt 本來就整組同字級）。
        p.Should().Be("0.6cm", "dead2 達 8 字素 → 整組字級起點降到 0.6cm");
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

    // === Tablet 字長門檻（2026-08-08 回復舊系統規則）：字素數 ≥8 → 0.6cm 起點，**空格計入** ===
    //
    // 與 2026-07-21 那版（RealCharCount，空格不計）刻意不同：門檻要跟「直書實際排幾列」對齊，
    // 而列數就是 VerticalText.Stack 的字素數（含空格）。客戶常用全形空格把兩個人名塞進同一格，
    // 那種格子看起來就是長長一條，該縮。

    [Fact]
    public void Tablet_dead_7elements_para08()
    {
        // 7 字素（無空格）＝門檻邊界下緣，不觸發
        var (_, p) = PrintTemplateSelector.ChooseTablet(N("一二三四五六七"), N("子甲"));
        p.Should().Be("0.8cm", "7 字素 < 8 → 維持 0.8cm 起點");
    }

    [Fact]
    public void Tablet_deadHalfWidthSpace_8elements_para06()
    {
        // "一二三 四五六七" = 7 真字 + 1 半形空格 = 8 字素 → 觸發（舊 RealCharCount 版不會觸發）
        var (_, p) = PrintTemplateSelector.ChooseTablet(N("一二三 四五六七"), N("子甲"));
        p.Should().Be("0.6cm", "空格計入字長門檻：8 字素 → 0.6cm");
    }

    [Fact]
    public void Tablet_deadFullWidthSpace_8elements_para06()
    {
        // 全形空格 U+3000 同樣計入（Stack 會為它渲染一列）
        var (_, p) = PrintTemplateSelector.ChooseTablet(N("一二三　四五六七"), N("子甲"));
        p.Should().Be("0.6cm", "全形空格 (U+3000) 亦計入字長門檻");
    }

    [Fact]
    public void Tablet_deadName_customerCaseWithFullWidthSpaces_para06()
    {
        // 2026-08-06 客訴那筆的往者格：用全形空格把兩個人名塞成上下兩段。
        // 舊 RealCharCount 只算 6 真字、門檻沒觸發；改以字素計後會觸發 → 起點 0.6cm。
        // ⚠️ 這不是溢出防線——不壓到「靈位」靠的仍是 TabletRenderer.DeadTextBottom（本輪未動）。
        foreach (var name in new[] { "一二三四 五六七八", "施　棟　施郭秀鑾" })
        {
            var (_, p) = PrintTemplateSelector.ChooseTablet(N(name), N("子甲"));
            p.Should().Be("0.6cm", "字素數（含空格）≥8：{0}", name);
        }
    }

    [Fact]
    public void Tablet_deadSupplementaryPlaneChars_countedAsOneElementEach()
    {
        // 增補平面造字（U+20000 以上，UTF-16 各佔 2 個 code unit）：7 個字 → .Length=14 會誤觸發，
        // 以字素計是 7 → 不觸發。gotchas「一個字不等於一個 char」的回歸鎖。
        var name = string.Concat(Enumerable.Repeat("\U00020000", 7));
        name.Length.Should().Be(14, "前提：這些字在 .Length 各算兩格");
        var (_, p) = PrintTemplateSelector.ChooseTablet(N(name), N("子甲"));
        p.Should().Be("0.8cm", "門檻以字素計，7 個增補平面字不得誤判成 14 字");
    }

    [Fact]
    public void Tablet_dead2_withSpace_8elements_para06()
    {
        // dead2 含空格，驗證 2 位分支同樣以字素計
        var (_, p) = PrintTemplateSelector.ChooseTablet(N("陳", "一二三 四五六七"), N("子甲", "子乙"));
        p.Should().Be("0.6cm", "dead2 空格計入字長門檻：8 字素 → 0.6cm");
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

    // === Slot-based（非 count-based）判定：名字填在後面欄位（有空洞）必落 fallback ===
    // 2026-07-18 客訴根因回歸鎖：舊系統逐槽判定（slot 2 有且 3-6 空 → Two），曾誤實作成
    // Count(IsPresent)==2 —— 只填第 3、4 格時誤選 Two 變體，而 Two 只畫 slot 1/2 → 往生者整組沒印。

    [Fact]
    public void Text_2dead_inSlots3And4_picks_Base_notTwo()
    {
        var t = PrintTemplateSelector.ChooseText(N(null, null, "亡丙", "亡丁"));
        t.Should().Be(TextTemplate.Base, "Two 變體只畫 slot 1/2，空洞資料必須落 Base 逐槽全畫");
    }

    [Fact]
    public void Text_slot2Filled_slot1Empty_picks_Two_likeLegacy()
    {
        // 舊 SignupForm.cs:1350 不看 slot 1：slot 2 有、3-6 空 → tmpTextTwo
        PrintTemplateSelector.ChooseText(N(null, "亡乙")).Should().Be(TextTemplate.Two);
    }

    [Fact]
    public void Tablet_2dead_inSlots3And4_picks_BaseFamily_notTwoFamily()
    {
        var (t, p) = PrintTemplateSelector.ChooseTablet(N(null, null, "亡丙", "亡丁"), N("子甲"));
        t.Should().Be(TabletTemplate.UnderscoreOne, "空洞資料落 fallback 系列（逐槽全畫）");
        p.Should().Be("0.6cm");
    }

    [Fact]
    public void Tablet_1dead_inSlot2_picks_TwoFamily_likeLegacy()
    {
        // 舊系統第二段 else if 不看 slot 1：slot 2 有、3-6 空 → Two 系列
        var (t, _) = PrintTemplateSelector.ChooseTablet(N(null, "亡乙"), N("子甲"));
        t.Should().Be(TabletTemplate.TwoOne);
    }

    [Fact]
    public void Tablet_livingInSlots2And3_fallsBackToUnsuffixed()
    {
        // 陽上同樣 slot-based：只填第 2、3 格 → 不是「2 位」→ 無後綴變體
        var (t, _) = PrintTemplateSelector.ChooseTablet(N("陳大明"), N(null, "子乙", "子丙"));
        t.Should().Be(TabletTemplate.One);
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
