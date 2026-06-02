namespace Ceremony.Domain.Services;

/// <summary>
/// 避 4 顯示格式化器。個位為 4 時顯示 "3-1"，其餘維持原數字。
/// </summary>
/// <remarks>
/// Legacy: NewSignupForm.cs:736-751 + SignupForm.cs:1912-1927 (GetNumberText helper)
/// 注意：純 display；DB 的 Number 欄位仍存實際 int（不跳號）。
/// 範例：4 → "3-1" / 14 → "13-1" / 24 → "23-1" / 5 → "5" / 13 → "13"
/// </remarks>
public static class AvoidFourFormatter
{
    public static string Format(int number)
    {
        if (number < 0)
            return number.ToString();

        var str = number.ToString();
        var lastDigit = number % 10;
        if (lastDigit != 4)
            return str;

        var left = str.Length > 1 ? str[..^1] : string.Empty;
        return left + "3-1";
    }
}
