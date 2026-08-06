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

        // 2026-08-06 使用者定案「縮字改成自動縮到剛好」：撤掉 2026-07-21 的「≥8 真字 → 固定 0.5cm」
        // 分支，1、2 位往者一律回 0.8cm **起點**，實際字級交給 renderer 依可用高等比縮
        // （薦牌 VerticalText.GroupFontPt、資料卡 MatrixLayout）。
        //
        // 為什麼撤掉：固定值同時是「不夠」也是「太小」——它擋不住真正的溢出（客戶實印壓到「靈位」的
        // 那筆是 2 位往者、每格用全形空格塞兩個人名共 8 個字素，`RealCharCount` 只算 6 真字，門檻根本
        // 沒觸發；真正的根因是 TabletRenderer 2 位分支的可用高用了 RDLC 遺留值 6.31），卻又讓沒有溢出
        // 風險的長名字被無條件縮到 0.5cm。可用高才是唯一該管溢出的地方，字級交給它算就好：
        // 8 字素 → 0.7cm、10 字 → 0.56cm、12 字 → 0.47cm，都保證塞得下且盡量大。
        // 連帶：`RealCharCount` 隨之移除（唯一呼叫端就是這裡；它用 `.Count(char)`，對增補平面造字會
        // 多算一倍，是 gotchas「一個字不等於一個 char」那條的殘留破口）。
        // ⚠️ 影響範圍不只薦牌：資料卡（ReportModelBuilders.DataCard）取的是同一個 ParaFontSize。
        return deadTier switch
        {
            1 => (livingTier switch
            {
                1 => TabletTemplate.OneOne,
                2 => TabletTemplate.OneTwo,
                _ => TabletTemplate.One,
            }, "0.8cm"),

            2 => (livingTier switch
            {
                1 => TabletTemplate.TwoOne,
                2 => TabletTemplate.TwoTwo,
                _ => TabletTemplate.Two,
            }, "0.8cm"),

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
    /// <remarks>
    /// public 是為了讓 <c>DataCardRenderer</c> 判「往者 1 位／2 位」時共用同一套 slot-based 語意
    /// （2026-07-27 客訴：1、2 位往者名字左移 0.3cm）；不可在呼叫端另外用 Count 數總數（見上方 remarks 的客訴根因）。
    /// </remarks>
    public static int SlotTier(string?[] names)
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
