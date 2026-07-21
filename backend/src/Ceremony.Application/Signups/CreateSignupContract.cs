namespace Ceremony.Application.Signups;

/// <summary>
/// 新增報名請求 — 對應舊 NewSignupForm.btnConfirm_Click。
/// </summary>
/// <remarks>
/// 故意捨棄「inline 新建 Believer」路徑：API 要求前端先 POST /believers 取 BelieverID。
/// NumberTitle 由 SignupType 推導，不收。
/// </remarks>
public sealed record CreateSignupRequest(
    int Year,
    Guid CeremonyCategoryId,
    int SignupType,
    Guid BelieverId,
    string Name,
    string MailAddress,
    bool KeepNumber = false,
    int? CustomNumber = null,
    int? Fee = null,
    string? Phone = null,
    string? HallName = null,
    // per-signup 覆寫欄（2026-07-21）：報名自持堂號/員工類型/固定編號，不回寫 Believer。
    // 空/超範圍 → 存 null → SignupView COALESCE 回退信眾值。見 signup-hallname-isolation.md（方案 A）。
    int? EmployeeType = null,
    bool? IsFixedNumber = null,
    int? MailZipcodeId = null,
    int? TextZipcodeId = null,
    string? TextAddress = null,
    IReadOnlyList<string?>? LivingNames = null,
    IReadOnlyList<string?>? DeadNames = null,
    string? Remark = null,
    int? PrepayYear = null,
    Guid? PrepayCeremonyCategoryId = null);

/// <summary>當前 caller 的身分（從 JWT claim 解出）。</summary>
public sealed record CallerContext(int AdminId, string AdminName);
