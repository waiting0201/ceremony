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
    bool ScopeRemark = false,
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

/// <summary>
/// 重複報名警示項：某信眾在同一 (Year, CeremonyCategoryID) 既有的報名摘要（忽略 SignupType）。
/// </summary>
/// <remarks>新版增強，legacy 無此檢查。詳見 docs/blueprints/api-endpoints/get-signup-duplicates.md。</remarks>
public sealed record SignupDuplicateItem(
    Guid SignupId,
    int SignupType,
    string? NumberTitle,
    int? Number,
    string? Name);

public sealed record SignupDuplicateListResponse(IReadOnlyList<SignupDuplicateItem> Items, int Total);
