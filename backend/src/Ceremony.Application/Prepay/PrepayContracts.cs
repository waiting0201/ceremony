namespace Ceremony.Application.Prepay;

/// <summary>預繳載入請求 — 對應 <c>LoadPrepayForm</c> 的 5 個 dropdown。</summary>
public sealed record PrepayLoadRequest(
    int SourceYear,
    Guid SourceCeremonyId,
    int TargetYear,
    Guid TargetCeremonyId,
    int BelieverGroup);

/// <summary>載入結果摘要。</summary>
public sealed record PrepayLoadResponse(
    int Loaded,
    int Skipped,
    PrepayLoadDetails Details);

/// <summary>
/// 某信眾「今年(含)以前最新一筆報名」的預繳資訊 — 對應舊 NewSignupForm.BelieverSelected:1102-1115
/// （選信眾時自動帶入預繳年/法會）。全為 null 代表查無報名或最新報名無預繳。
/// </summary>
public sealed record BelieverLatestPrepayResult(
    int? PrepayYear,
    Guid? PrepayCeremonyCategoryId,
    string? PrepayCeremonyCategoryTitle);

public sealed record PrepayLoadDetails(
    int FixedLoaded,
    int NonFixedLoaded,
    int CarriedForwardPrepay,
    IReadOnlyList<int> FilledGaps);

/// <summary>
/// Repository 內部 — 一筆預繳源資料（已 join Believer + PrepayCeremonyCategory）。
/// </summary>
public sealed record PrepaySourceRow(
    Guid SignupId,
    Guid BelieverId,
    int SignupType,
    string? NumberTitle,
    int? Number,
    int? Fee,
    string? Name,
    string? Phone,
    string?[] LivingNames,
    string?[] DeadNames,
    int? MailZipcodeId,
    string? MailZipcode,
    string? MailAddress,
    int? TextZipcodeId,
    string? TextZipcode,
    string? TextAddress,
    string? Remark,
    int? PrepayYear,
    Guid? PrepayCeremonyCategoryId,
    int? PrepayCeremonySort,
    string? PrepayCeremonyTitle,
    bool IsFixedNumber,
    int? EmployeeType);
