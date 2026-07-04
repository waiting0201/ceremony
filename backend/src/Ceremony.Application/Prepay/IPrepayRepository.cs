using Ceremony.Application.Signups;

namespace Ceremony.Application.Prepay;

public interface IPrepayRepository
{
    Task<(string Title, int Sort)?> GetCeremonyCategoryAsync(Guid id, CancellationToken ct = default);

    /// <summary>查源資料（含 Believer 與 PrepayCeremonyCategorys 已 join 的欄位）。</summary>
    Task<IReadOnlyList<PrepaySourceRow>> GetPrepaySourcesAsync(
        int sourceYear,
        Guid sourceCeremonyId,
        int signupType,
        int? employeeType,
        int targetYear,
        int targetSort,
        CancellationToken ct = default);

    /// <summary>
    /// 在<b>單一 transaction</b> 內完成整個載入：取得群組互斥鎖（<c>sp_getapplock</c>）→
    /// 讀已存在信眾（idempotency）→ <c>SELECT MAX(Number) WITH (UPDLOCK, HOLDLOCK)</c> →
    /// 用 <c>PrepayNumberAllocator</c> 配號 → 逐筆 insert Signup + SignupLog → commit。
    /// </summary>
    /// <remarks>
    /// 配號（讀 MAX）與 insert 必須同交易、且 MAX 讀取加 UPDLOCK/HOLDLOCK 範圍鎖，才能同時擋住
    /// 另一個預繳載入與一般報名（<c>SignupRepository.InsertWithLogAsync</c>）的並發插入，杜絕重號。
    /// 已存在的信眾（同 Year×Ceremony×SignupType×Believer）計入 <c>Skipped</c>、不 insert。
    /// </remarks>
    /// <param name="fixedCandidates">固定編號候選，需依 PreservedNumber 升冪。</param>
    /// <param name="nonFixedCandidates">非固定編號候選，需依來源 Number 升冪。</param>
    Task<PrepayLoadResponse> InsertPrepayBatchAsync(
        int targetYear,
        Guid targetCeremonyId,
        int signupType,
        IReadOnlyList<PrepayCandidate> fixedCandidates,
        IReadOnlyList<PrepayCandidate> nonFixedCandidates,
        CancellationToken ct = default);
}
