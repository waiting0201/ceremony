namespace Ceremony.Domain.Services;

/// <summary>
/// 列印模板選擇邏輯（refactored from SignupForm.cs:1148-1696）。
/// </summary>
/// <remarks>
/// **這不是推測 — 是 code ground truth**。對齊 docs/blueprints/printing-reports-positions.md §模板選擇邏輯。
/// 「N 位」判定：name != null && name.Trim() != ""。
/// </remarks>
public static class PrintTemplateSelector
{
    /// <summary>
    /// 薦牌 9 變體選擇 + 動態 ParaFontSize。
    /// </summary>
    public static (TabletTemplate Template, string ParaFontSize) ChooseTablet(string?[] deadNames, string?[] livingNames)
    {
        var deadCount = deadNames.Count(IsPresent);
        var livingCount = livingNames.Count(IsPresent);

        var dead1Long = IsPresent(deadNames[0]) && RealCharCount(deadNames[0]) > 7;
        var dead2Long = deadCount >= 2 && IsPresent(deadNames[1]) && RealCharCount(deadNames[1]) > 7;

        return deadCount switch
        {
            1 => (livingCount switch
            {
                1 => TabletTemplate.OneOne,
                2 => TabletTemplate.OneTwo,
                _ => TabletTemplate.One,
            }, dead1Long ? "0.6cm" : "0.8cm"),

            2 => (livingCount switch
            {
                1 => TabletTemplate.TwoOne,
                2 => TabletTemplate.TwoTwo,
                _ => TabletTemplate.Two,
            }, (dead1Long || dead2Long) ? "0.6cm" : "0.8cm"),

            _ => (livingCount switch
            {
                1 => TabletTemplate.UnderscoreOne,
                2 => TabletTemplate.UnderscoreTwo,
                _ => TabletTemplate.Base,    // 3+ 亡 3+ 陽 fallback
            }, "0.6cm"),                     // 3+ 亡時固定 0.6cm
        };
    }

    /// <summary>文牒 2 變體：只有恰好 2 亡者選 Two，否則 Base。</summary>
    public static TextTemplate ChooseText(string?[] deadNames)
        => deadNames.Count(IsPresent) == 2 ? TextTemplate.Two : TextTemplate.Base;

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
