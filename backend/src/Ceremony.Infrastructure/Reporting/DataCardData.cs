namespace Ceremony.Infrastructure.Reporting;

/// <summary>
/// 資料卡 PDF 所需資料（從 SignupListItem + JOIN 整理）。
/// </summary>
/// <remarks>
/// 堂號沿革（**現況＝要印**，勿再依 2026-07-03 的舊敘述判斷）：2026-07-03 依樣板量測判定
/// 「資料卡印刷紙張沒有堂號欄」而把 HallName 整條移除；2026-08-14 客訴「右邊的列印沒有印到堂號」，
/// 使用者指定比照薦牌——堂號拆兩半、分列右側「故◯◯靈位」窗框內「故」字左右（右＝First、左＝Second，
/// 直書右起）。見 docs/blueprints/printing-reports.md「資料卡改版」。
/// </remarks>
public sealed record DataCardData(
    string Number,
    string? Prepay,
    string?[] DeadNames,     // 6 elements
    string?[] LivingNames,   // 6 elements
    string? Address,
    string? Phone,
    string? Remark,
    string? NumberTitle = null,      // 編號抬頭；與 Number 分開繪製，中間留 0.3cm 空隙（2026-07-21 客訴）
    double ParaFontSizeCm = 0.6,     // 往者字級起點（cm），與薦牌一致：由 PrintTemplateSelector.ChooseTablet 決定
    string? HallNameFirst = null,    // 堂號右半（SplitHallName 的 First）；直書右起故右邊先讀
    string? HallNameSecond = null);  // 堂號左半（SplitHallName 的 Second）；3 字／5 字以上會整串進 First、此欄為空
