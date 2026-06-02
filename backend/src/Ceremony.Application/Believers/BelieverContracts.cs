namespace Ceremony.Application.Believers;

/// <summary>
/// 搜尋條件。對應 BelieverForm.cs:35-44 (btnSearch_Click)。
/// </summary>
public sealed record BelieverSearchQuery(
    string? Name = null,
    string? Phone = null,
    string? HallName = null,
    string? LivingName = null,
    string? DeadName = null);

/// <summary>
/// 信眾列表 read-model。對應舊 BelieverViewModel (BelieverForm.cs:368-396)。
/// EmployeeTypeTitle 為計算欄位（1=非員工, 2=大殿, 3=地藏殿）。
/// </summary>
public sealed record BelieverListItem(
    Guid Id,
    int EmployeeType,
    string EmployeeTypeTitle,
    string? HallName,
    string Name,
    string? Phone,
    bool IsFixedNumber,
    int? MailZipcodeId,
    string? MailCity,
    string? MailArea,
    string? MailAddress,
    int? TextZipcodeId,
    string? TextCity,
    string? TextArea,
    string? TextAddress,
    string?[] LivingNames,
    string?[] DeadNames);

public sealed record BelieverListResponse(IReadOnlyList<BelieverListItem> Items, int Total);

/// <summary>
/// 新增 / 更新請求。POST 與 PUT 共用結構。
/// 對應 BelieverForm.cs:101-185 (btnConfirm_Click 雙路徑)。
/// </summary>
public sealed record BelieverUpsertRequest(
    int EmployeeType,
    string Name,
    string MailAddress,
    string? HallName = null,
    string? Phone = null,
    bool IsFixedNumber = false,
    int? MailZipcodeId = null,
    int? TextZipcodeId = null,
    string? TextAddress = null,
    IReadOnlyList<string?>? LivingNames = null,
    IReadOnlyList<string?>? DeadNames = null);
