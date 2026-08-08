using System.Globalization;

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

        // 2026-08-08 使用者指定「往者 8 字以上時，字體大小與三位的一樣」＝**回復舊系統原始規則**
        // （`DeadNameOne.Length > 7 ? "0.6cm" : "0.8cm"`，見 printing-reports-positions.md §模板選擇邏輯），
        // 但語意是 **起點** 不是固定值：renderer 仍會在 0.6cm 之上依可用高再自動縮。
        //
        // 這條門檻曾於 2026-07-21 被誤調成 0.5cm，再於 2026-08-06「縮字改成自動縮到剛好」時整條撤除。
        // 撤除當時的理由是「字數門檻擋不住溢出」——**那個判斷仍然成立且未被推翻**：客戶實印壓到
        // 「靈位」的根因是 TabletRenderer 2 位分支可用高用了 RDLC 遺留值 6.31，已由 `DeadTextBottom`
        // 這條共用下界修掉，本輪**完全不動它**。所以現在是兩層並存、各司其職：
        //   - 可用高（DeadTextBottom / MatrixLayout）＝**溢出防線**，保證任何字數都不壓到預印字；
        //   - 本門檻＝**美觀上限**，長名字不要比 3+ 位往者還大（使用者這次要的就是這個）。
        // 字級對照（可用高 5.6cm）：7 字 0.80（不觸發）／8 字 0.70→**0.60**／9 字 0.62→**0.60**／
        // 12 字 0.47（門檻與自動縮取小者，無變化）。
        //
        // 字數以**字素數、含空格**計（使用者定案）：客戶常用全形空格把兩個人名塞進同一格
        // （「施　棟　施郭秀鑾」＝8 字素、6 真字），那筆會觸發——與直書實際列數一致，也與
        // VerticalText.Stack/GroupFontPt 的計算基準同源。這是與舊 `RealCharCount`（不算空格、且用
        // `.Count(char)` 對增補平面造字會多算一倍）刻意的差異，不是回退。
        // ⚠️ 影響範圍不只薦牌：資料卡（ReportModelBuilders.DataCard）取的是同一個 ParaFontSize，
        //    長名往者會一併從 0.69 縮到 0.60cm——使用者已確認要兩份報表一起回復（本就是 2026-07-21
        //    「資料卡往者字級改與薦牌一致」客訴的原意）。
        return deadTier switch
        {
            1 => (livingTier switch
            {
                1 => TabletTemplate.OneOne,
                2 => TabletTemplate.OneTwo,
                _ => TabletTemplate.One,
            }, IsLongDeadName(deadNames[0]) ? "0.6cm" : "0.8cm"),

            2 => (livingTier switch
            {
                1 => TabletTemplate.TwoOne,
                2 => TabletTemplate.TwoTwo,
                _ => TabletTemplate.Two,
            }, IsLongDeadName(deadNames[0]) || IsLongDeadName(deadNames[1]) ? "0.6cm" : "0.8cm"),

            _ => (livingTier switch
            {
                1 => TabletTemplate.UnderscoreOne,
                2 => TabletTemplate.UnderscoreTwo,
                _ => TabletTemplate.Base,    // 3+ 亡 3+ 陽 fallback
            }, "0.6cm"),                     // 3+ 亡時固定 0.6cm
        };
    }

    /// <summary>往者「長名字」門檻：字素數 ≥ 8（對齊舊系統 <c>.Length &gt; 7</c>）。</summary>
    private const int LongDeadNameThreshold = 8;

    /// <summary>
    /// 往者姓名是否達長名字門檻 → <c>ParaFontSize</c> 起點降到 0.6cm（同 3+ 位往者）。
    /// </summary>
    /// <remarks>
    /// 以 <see cref="StringInfo.LengthInTextElements"/>（字素）計、**不 trim 也不排除空格**：
    /// 與 <c>VerticalText.Stack</c> 實際渲染的直書列數同一個基準（Infrastructure 的
    /// <c>VerticalText.ElementCount</c> 內部就是同一個 API；Domain 不能反向引用 Infrastructure，
    /// 故此處直接用 <see cref="StringInfo"/>）。用字素而非 <c>.Length</c> 是因為增補平面造字
    /// （`𡍼`/`𤆬`…，UTF-16 佔兩個 code unit）在 <c>.Length</c> 會多算一倍——見 gotchas
    /// 「一個字不等於一個 char」。
    /// </remarks>
    private static bool IsLongDeadName(string? name)
        => !string.IsNullOrEmpty(name) && new StringInfo(name).LengthInTextElements >= LongDeadNameThreshold;

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
