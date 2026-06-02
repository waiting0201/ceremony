using Ceremony.Domain.Exceptions;

namespace Ceremony.Domain.Services;

/// <summary>
/// 由 SignupType 推導 NumberTitle 字串。
/// </summary>
/// <remarks>
/// Legacy: NewSignupForm.cs:281-298 (switch in btnConfirm_Click)
/// 1=一般→No, 2=寺方→寺, 3=觀音會→觀, 4=普桌→普, 5=郵撥→郵
/// </remarks>
public static class NumberTitleResolver
{
    public static string Resolve(int signupType) => signupType switch
    {
        1 => "No",
        2 => "寺",
        3 => "觀",
        4 => "普",
        5 => "郵",
        _ => throw new DomainException("VALIDATION_INVALID", "報名類型錯誤"),
    };

    public static string SignupTypeName(int signupType) => signupType switch
    {
        1 => "一般",
        2 => "寺方",
        3 => "觀音會",
        4 => "普桌",
        5 => "郵撥",
        _ => "未知",
    };
}
