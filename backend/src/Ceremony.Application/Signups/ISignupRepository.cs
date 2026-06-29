using Ceremony.Application.Prepay;

namespace Ceremony.Application.Signups;

public interface ISignupRepository
{
    Task<IReadOnlyList<SignupListItem>> SearchAsync(SignupSearchQuery query, CancellationToken ct = default);

    Task<SignupListItem?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>確認 keepNumber 路徑下，customNumber 沒被佔用。</summary>
    Task<bool> NumberExistsAsync(int year, Guid ceremonyCategoryId, int signupType, int number, CancellationToken ct = default);

    /// <summary>編輯時的 number 重複檢查（排除自己的 SignupID）。</summary>
    Task<bool> NumberExistsExcludingAsync(int year, Guid ceremonyCategoryId, int signupType, int number, Guid excludeSignupId, CancellationToken ct = default);

    /// <summary>
    /// 在交易內 lock 編號 + 插入 Signup + 插入 SignupLog。
    /// 若 nextNumber 為 null 則由 repository 內部用 UPDLOCK + MAX+1 算；否則用呼叫端提供值（keepNumber 路徑）。
    /// </summary>
    Task InsertWithLogAsync(SignupWriteModel signup, SignupLogWriteModel log, int? explicitNumber, CancellationToken ct = default);

    /// <summary>
    /// 編輯 Signup（全欄位覆寫，使用 signup.SignupId 為主鍵）+ 同交易插入 SignupLog。
    /// </summary>
    /// <remarks>
    /// 刻意不回寫 Believer 任何欄位：堂號/員工類型/固定編號皆為「信眾層級」屬性，只在信眾維護頁修改。
    /// （故意偏離 legacy EditSignupForm 的 believer 回寫，修正「改一筆報名堂號連動同信眾全部報名」缺陷；
    ///  見 docs/blueprints/signup-hallname-isolation.md）
    /// </remarks>
    /// <returns>true if updated; false if signupId not found.</returns>
    Task<bool> UpdateWithLogAsync(
        SignupWriteModel signup,
        SignupLogWriteModel log,
        int number,
        CancellationToken ct = default);

    /// <summary>硬刪除（無 IsEnabled）。</summary>
    /// <returns>true if deleted; false if not found.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    Task<string?> GetCeremonyCategoryTitleAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 批次列印用：按編號範圍 + 選填條件查 signups，ORDER BY Number。
    /// 對齊 <c>SignupForm.cs:462-475</c> btnPrint_Click 的 LINQ 串接。
    /// </summary>
    Task<IReadOnlyList<SignupListItem>> SearchByNumberRangeAsync(SignupRangeQuery query, CancellationToken ct = default);

    /// <summary>
    /// 取某信眾「Year ≤ yearLte 的最新一筆報名」的預繳資訊（ORDER BY Year DESC, CeremonySort DESC）。
    /// 對應舊 NewSignupForm.BelieverSelected:1102-1115。查無報名回 null。
    /// </summary>
    Task<BelieverLatestPrepayResult?> GetLatestPrepayByBelieverAsync(Guid believerId, int yearLte, CancellationToken ct = default);
}

/// <summary>批次列印查詢條件。對齊舊 nudStart / nudEnd / txtSearchYear / cbIsScope / dlSearchCeremony / dlSearchSignupType。</summary>
public sealed record SignupRangeQuery(
    int NumberStart,
    int NumberEnd,
    int? Year,
    bool YearGte,
    Guid? CeremonyCategoryId,
    int? SignupType);

/// <summary>Repository 內部 Signup 寫入 model。Number 在 InsertWithLogAsync 內賦值。</summary>
public sealed record SignupWriteModel(
    Guid SignupId,
    int Year,
    Guid CeremonyCategoryId,
    int SignupType,
    Guid? BelieverId,
    string NumberTitle,
    int? Fee,
    string Name,
    string? Phone,
    string?[] LivingNames,
    string?[] DeadNames,
    int? MailZipcodeId,
    string? MailAddress,
    int? TextZipcodeId,
    string? TextAddress,
    string? Remark,
    int? PrepayYear,
    Guid? PrepayCeremonyCategoryId,
    int AdminId,
    DateTime CreateDate);

/// <summary>Repository 內部 SignupLog 寫入 model（含已 join 的 title 快照）。Number 在 InsertWithLogAsync 內賦值。</summary>
public sealed record SignupLogWriteModel(
    Guid SignupLogId,
    Guid SignupId,
    int Year,
    string? CeremonyCategoryTitle,
    int SignupType,
    string? HallName,
    string Name,
    string? Phone,
    string NumberTitle,
    int? Fee,
    string?[] LivingNames,
    string?[] DeadNames,
    string? MailCity,
    string? MailZone,
    string? MailAddress,
    string? TextCity,
    string? TextZone,
    string? TextAddress,
    string? Remark,
    int? PrepayYear,
    string? PrepayCeremonyCategoryTitle,
    string Admin,
    DateTime CreateDate);
