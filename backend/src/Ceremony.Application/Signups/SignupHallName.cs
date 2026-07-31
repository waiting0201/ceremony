namespace Ceremony.Application.Signups;

/// <summary>
/// 報名堂號（per-signup 覆寫欄）的寫入語意。Create / Update / InsertShift 三個 handler 共用。
/// </summary>
/// <remarks>
/// <para>
/// <c>SignupView.HallName</c> 是 <c>COALESCE(S.HallName, B.HallName)</c>——null 代表「這筆報名沒有自己的
/// 堂號」，顯示時回退信眾主檔（並行期舊系統寫入的列即為此情形，見 DbUp 0003）。
/// </para>
/// <para>
/// 因此「使用者把堂號清空」不能存 null——存 null 會被 COALESCE 補回信眾堂號，畫面上又長回來，
/// 等於刪不掉（2026-07-31 使用者回報）。改以空字串當「明確清空」的哨兵值：COALESCE 取到 ""
/// → 顯示空白，且不影響舊系統（它讀 view 拿到空字串一樣是空白）。
/// </para>
/// <list type="bullet">
///   <item><c>null</c>（欄位未提供）→ null，維持回退信眾堂號</item>
///   <item>空字串 / 純空白（使用者清空）→ <c>""</c>，顯示空白＝真的刪掉</item>
///   <item>其餘 → trim 後原值</item>
/// </list>
/// Blueprint: docs/blueprints/signup-hallname-isolation.md
/// </remarks>
internal static class SignupHallName
{
    public static string? Normalize(string? hallName)
        => hallName is null ? null : hallName.Trim();
}
