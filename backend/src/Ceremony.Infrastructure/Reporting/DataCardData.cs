namespace Ceremony.Infrastructure.Reporting;

/// <summary>
/// 資料卡 PDF 所需資料（從 SignupListItem + JOIN 整理）。
/// </summary>
/// <remarks>
/// 2026-07-03：拿掉 HallName——樣板量測確認資料卡實際印刷紙張沒有堂號欄，見
/// docs/blueprints/printing-reports.md「資料卡改版」。
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
    double ParaFontSizeCm = 0.6);    // 往者字級起點（cm），與薦牌一致：由 PrintTemplateSelector.ChooseTablet 決定
