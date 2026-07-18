namespace Ceremony.Domain.Services;

/// <summary>
/// 列印模板選擇邏輯（refactored from SignupForm.cs:1148-1696）。
/// </summary>
/// <remarks>
/// **這不是推測 — 是 code ground truth**。對齊 docs/blueprints/printing-reports-positions.md §模板選擇邏輯。
/// 「N 位」判定：name != null && name.Trim() != ""。
///
/// **判定是 slot-based 不是 count-based（2026-07-18 客訴修正，勿回退）**：舊系統的「只有 2 位」
/// 實際條件是「slot 2 有名字且 slot 3-6 全空」（SignupForm.cs:1164/1190/1350 逐槽檢查，
/// 不數總數）。曾誤實作成 Count(IsPresent)==2 —— 當名字填在後面的欄位（如只填第 3、4 格）時
/// 會誤選 Two 系列變體，而 Two 變體只畫 slot 1/2 → 往生者整組沒印出來（文牒客訴根因）。
/// </remarks>
public static class PrintTemplateSelector
{
    /// <summary>
    /// 薦牌 9 變體選擇 + 動態 ParaFontSize。
    /// </summary>
    public static (TabletTemplate Template, string ParaFontSize) ChooseTablet(string?[] deadNames, string?[] livingNames)
    {
        var deadTier = SlotTier(deadNames);
        var livingTier = SlotTier(livingNames);

        var dead1Long = IsPresent(deadNames[0]) && RealCharCount(deadNames[0]) > 7;
        var dead2Long = IsPresent(deadNames[1]) && RealCharCount(deadNames[1]) > 7;

        return deadTier switch
        {
            1 => (livingTier switch
            {
                1 => TabletTemplate.OneOne,
                2 => TabletTemplate.OneTwo,
                _ => TabletTemplate.One,
            }, dead1Long ? "0.6cm" : "0.8cm"),

            2 => (livingTier switch
            {
                1 => TabletTemplate.TwoOne,
                2 => TabletTemplate.TwoTwo,
                _ => TabletTemplate.Two,
            }, (dead1Long || dead2Long) ? "0.6cm" : "0.8cm"),

            _ => (livingTier switch
            {
                1 => TabletTemplate.UnderscoreOne,
                2 => TabletTemplate.UnderscoreTwo,
                _ => TabletTemplate.Base,    // 3+ 亡 3+ 陽 fallback
            }, "0.6cm"),                     // 3+ 亡時固定 0.6cm
        };
    }

    /// <summary>文牒 2 變體：slot 2 有名字且 3-6 全空（舊 SignupForm.cs:1350）選 Two，否則 Base。</summary>
    public static TextTemplate ChooseText(string?[] deadNames)
        => SlotTier(deadNames) == 2 ? TextTemplate.Two : TextTemplate.Base;

    /// <summary>
    /// 舊系統的三段 if/else 逐槽判定：1＝「slot 1 有、2-6 全空」；2＝「slot 2 有、3-6 全空」
    /// （不看 slot 1，與舊 code 一致）；其餘＝3（fallback）。名字填在後面欄位（有空洞）一律落 fallback，
    /// 由 Base 系列變體逐槽全畫，才不會丟名字。
    /// </summary>
    private static int SlotTier(string?[] names)
    {
        if (IsPresent(names[0]) && AllEmptyFrom(names, 1)) return 1;
        if (IsPresent(names[1]) && AllEmptyFrom(names, 2)) return 2;
        return 3;
    }

    private static bool AllEmptyFrom(string?[] names, int start)
    {
        for (var i = start; i < names.Length; i++)
            if (IsPresent(names[i])) return false;
        return true;
    }

    /// <summary>
    /// 普桌 6 變體：從 Six 往 One 找第一個有值的（**不考慮中間有空格**）。
    /// </summary>
    public static WorshipTemplate ChooseWorship(string?[] livingNames)
    {
        if (IsPresent(livingNames[5])) return WorshipTemplate.Base;      // 6 位
        if (IsPresent(livingNames[4])) return WorshipTemplate.Five;
        if (IsPresent(livingNames[3])) return WorshipTemplate.Four;
        if (IsPresent(livingNames[2])) return WorshipTemplate.Three;
        if (IsPresent(livingNames[1])) return WorshipTemplate.Two;
        return WorshipTemplate.One;
    }

    private static bool IsPresent(string? name)
        => !string.IsNullOrWhiteSpace(name);

    /// <summary>
    /// 真實字數（排除所有空白字元）。使用者會在姓名中間刻意輸入空格作排版間隙，
    /// 直書渲染時保留（見 VerticalText.Stack），但這些間隙**不應計入** ">7 字 → 0.6cm" 字級門檻。
    /// char.IsWhiteSpace 已涵蓋半形 (U+0020) 與全形空格 (U+3000)。
    /// **刻意偏離 legacy**：舊 SignupForm.cs:1179/1203 用 Trim().Length，會把中間空格計入。
    /// 注意：與 VerticalText.GroupFontPt（刻意計入空格以對齊渲染列數）語意相反，勿統一。
    /// </summary>
    private static int RealCharCount(string? name)
        => name is null ? 0 : name.Count(c => !char.IsWhiteSpace(c));
}

public enum TabletTemplate
{
    Base,          // tmpTablet.rdlc — 3+ 亡 3+ 陽 (fallback)
    One,           // tmpTabletOne.rdlc — 1 亡 3-6 陽
    OneOne,        // tmpTabletOneOne.rdlc — 1 亡 1 陽
    OneTwo,        // tmpTabletOneTwo.rdlc — 1 亡 2 陽
    Two,           // tmpTabletTwo.rdlc — 2 亡 3-6 陽
    TwoOne,        // tmpTabletTwoOne.rdlc — 2 亡 1 陽
    TwoTwo,        // tmpTabletTwoTwo.rdlc — 2 亡 2 陽
    UnderscoreOne, // tmpTablet_One.rdlc — 3+ 亡 1 陽
    UnderscoreTwo, // tmpTablet_Two.rdlc — 3+ 亡 2 陽
}

public enum TextTemplate
{
    Base,  // tmpText.rdlc — 1 OR 3+ 亡 OR 6 亡 (fallback)
    Two,   // tmpTextTwo.rdlc — 恰好 2 亡
}

public enum WorshipTemplate
{
    Base,   // tmpWorship.rdlc — 6 位陽上
    Five,   // tmpWorshipFive.rdlc
    Four,
    Three,
    Two,
    One,
}
