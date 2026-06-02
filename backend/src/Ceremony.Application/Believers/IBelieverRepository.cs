namespace Ceremony.Application.Believers;

public interface IBelieverRepository
{
    Task<IReadOnlyList<BelieverListItem>> SearchAsync(BelieverSearchQuery query, CancellationToken ct = default);

    Task<BelieverListItem?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Guid> InsertAsync(BelieverWriteModel data, CancellationToken ct = default);

    /// <returns>true if updated; false if id not found</returns>
    Task<bool> UpdateAsync(Guid id, BelieverWriteModel data, CancellationToken ct = default);

    /// <summary>檢查是否有 Signup 引用該信眾（DELETE 衝突保護）</summary>
    Task<bool> HasSignupsAsync(Guid id, CancellationToken ct = default);

    Task<string?> GetNameAsync(Guid id, CancellationToken ct = default);

    /// <returns>true if deleted; false if id not found</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Repository 內部寫入用 model（已 normalize：phone 轉半形、空字串轉 null）。
/// 不直接暴露給 Controller — 由 Handler 從 Request 轉換。
/// </summary>
public sealed record BelieverWriteModel(
    int EmployeeType,
    string Name,
    string MailAddress,
    string? HallName,
    string? Phone,
    bool IsFixedNumber,
    int? MailZipcodeId,
    int? TextZipcodeId,
    string? TextAddress,
    string?[] LivingNames,  // length 6
    string?[] DeadNames);   // length 6
