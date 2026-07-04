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
/// 一筆待載入的候選（已完成欄位映射與 PrepayYear 結轉判斷，但 <b>尚未配 Number</b>）。
/// Number 由 <c>InsertPrepayBatchAsync</c> 在 transaction 內、上鎖讀取 MAX 後才分配，
/// 以杜絕「讀 MAX 與 insert 之間」的並發配號碰撞。
/// </summary>
public sealed record PrepayCandidate(
    Guid BelieverId,
    bool IsFixedNumber,
    int? PreservedNumber,
    bool CarriedForward,
    Signups.SignupWriteModel Signup,
    Signups.SignupLogWriteModel Log);

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
