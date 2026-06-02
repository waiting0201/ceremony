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

    /// <summary>找目標 (Year, Ceremony, SignupType) 的最大 Number；無資料回 0。</summary>
    Task<int> GetMaxNumberAsync(int targetYear, Guid targetCeremonyId, int signupType, CancellationToken ct = default);

    /// <summary>查指定信眾在目標年度法會是否已有報名（idempotency check）。</summary>
    Task<bool> SignupExistsAsync(int targetYear, Guid targetCeremonyId, int signupType, Guid believerId, CancellationToken ct = default);

    /// <summary>
    /// 批次插入。每筆 (Signup, SignupLog) 同交易；整個批次同交易；UPDLOCK 避免並發 Number 碰撞。
    /// 呼叫者需準備好 Number 已分配的 model。
    /// </summary>
    Task InsertBatchAsync(IReadOnlyList<(SignupWriteModel Signup, SignupLogWriteModel Log, int Number)> batch, CancellationToken ct = default);
}
