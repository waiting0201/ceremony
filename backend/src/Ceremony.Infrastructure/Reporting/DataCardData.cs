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
    string? Remark);
