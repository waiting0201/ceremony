namespace Ceremony.Application.Signups;

/// <summary>
/// 報名查詢條件 — 對應 <c>SignupForm.LoadSearchSignups</c> 的 UI 控件。
/// </summary>
/// <remarks>
/// AND 群組：Year/Ceremony/SignupType/Number；OR 群組：scope* + IsFixedNumber。
/// 詳見 docs/blueprints/api-endpoints/get-signups.md OR 群組規則。
/// </remarks>
public sealed record SignupSearchQuery(
    int? Year = null,
    bool IsScope = false,
    Guid? CeremonyCategoryId = null,
    int? SignupType = null,
    int? Number = null,
    string? SearchKey = null,
    bool ScopeName = false,
    bool ScopeLivingName = false,
    bool ScopeDeadName = false,
    bool ScopePhone = false,
    bool IsFixedNumber = false);

/// <summary>
/// 報名列表項，欄位對應既有 <c>dbo.SignupView</c>。
/// </summary>
public sealed record SignupListItem(
    Guid Id,
    int Year,
    Guid CeremonyCategoryId,
    string? CeremonyTitle,
    int SignupType,
    string? NumberTitle,
    int? Number,
    int? Fee,
    string? Employee,
    Guid? BelieverId,
    string? Name,
    string? HallName,
    string? Phone,
    bool IsFixedNumber,
    string?[] LivingNames,
    string?[] DeadNames,
    string? MailCity,
    string? MailZone,
    string? MailZipcode,
    string? MailAddress,
    string? TextCity,
    string? TextZone,
    string? TextZipcode,
    string? TextAddress,
    int? PrepayYear,
    Guid? PrepayCeremonyCategoryId,
    string? PrepayCeremonyTitle,
    string? Remark,
    string? AdminName,
    DateTime? CreateDate);

public sealed record SignupListResponse(IReadOnlyList<SignupListItem> Items, int Total);
