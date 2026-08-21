using Ceremony.Domain.Exceptions;

namespace Ceremony.Application.Signups;

/// <summary>
/// 把一筆既有報名移動到同群組 (Year, CeremonyCategoryID, SignupType) 內的指定編號，
/// 中間區段自動 ±1 讓位。對應「報名維護列表右鍵 → 移動插入至…」。
/// </summary>
/// <remarks>
/// Legacy: 無對應（舊系統只能逐筆改編號、改到已佔用號會被「編號重複」擋下）。新版增強。
/// Blueprint: docs/blueprints/api-endpoints/post-signups-move-number.md
///
/// 與 <see cref="InsertShiftSignupHandler"/> 的分工：那支是**新增一筆**（總筆數 +1、其後全部 +1），
/// 本支是**移位**（總筆數不變、只有起訖之間讓位、不留空號）。
/// 群組判定、範圍檢查與實際搬移都在 repository 的單一交易內做（要在 applock 內才安全）。
/// </remarks>
public sealed class MoveSignupNumberHandler(ISignupRepository signupRepo)
{
    public async Task<SignupListItem> HandleAsync(Guid signupId, MoveSignupNumberRequest req, CancellationToken ct = default)
    {
        if (req.TargetNumber is not { } target)
            throw new DomainException("VALIDATION_REQUIRED", "請輸入目標編號");
        if (target <= 0)
            throw new DomainException("VALIDATION_INVALID", "目標編號必須大於 0");

        await signupRepo.MoveNumberAsync(signupId, target, ct);

        var moved = await signupRepo.GetByIdAsync(signupId, ct);
        return moved ?? throw new DomainException("INTERNAL_ERROR", "移動後無法讀回該筆報名");
    }
}
