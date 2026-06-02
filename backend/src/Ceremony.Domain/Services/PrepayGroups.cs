using Ceremony.Domain.Exceptions;

namespace Ceremony.Domain.Services;

/// <summary>
/// 預繳載入的 6 個信眾分組策略表（refactored from <c>LoadPrepayForm.btnConfirm_Click</c> 的 switch）。
/// </summary>
/// <remarks>
/// 對照舊系統 LoadPrepayForm.cs:70-818 — 6 個 case 結構幾乎完全相同，
/// 只差 SignupType 與 EmployeeType filter。本表把差異萃取為 data。
/// </remarks>
public sealed record PrepayGroup(int Code, string Name, int SignupType, int? EmployeeType);

public static class PrepayGroups
{
    public static readonly IReadOnlyList<PrepayGroup> All =
    [
        new(1, "非員工一般", SignupType: 1, EmployeeType: 1),
        new(2, "地藏殿員工一般", SignupType: 1, EmployeeType: 3),
        new(3, "寺方", SignupType: 2, EmployeeType: null),
        new(4, "觀音會", SignupType: 3, EmployeeType: null),
        new(5, "大殿員工郵撥", SignupType: 5, EmployeeType: 2),
        new(6, "非員工郵撥", SignupType: 5, EmployeeType: 1),
    ];

    public static PrepayGroup Resolve(int code)
    {
        var match = All.FirstOrDefault(g => g.Code == code);
        return match ?? throw new DomainException("VALIDATION_INVALID", "無效的信眾類別");
    }
}
