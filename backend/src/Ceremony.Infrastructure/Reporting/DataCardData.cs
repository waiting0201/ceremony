namespace Ceremony.Infrastructure.Reporting;

/// <summary>
/// 資料卡 PDF 所需資料（從 SignupListItem + JOIN 整理）。
/// </summary>
public sealed record DataCardData(
    string Number,
    string? HallName,
    string? Prepay,
    string?[] DeadNames,     // 6 elements
    string?[] LivingNames,   // 6 elements
    string? Address,
    string? Phone,
    string? Remark);
