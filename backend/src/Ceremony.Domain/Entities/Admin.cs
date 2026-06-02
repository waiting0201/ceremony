namespace Ceremony.Domain.Entities;

/// <summary>
/// 管理者帳號實體（對映既有 dbo.Admins 表）。
/// </summary>
/// <remarks>
/// Legacy schema: Admins (AdminID int IDENTITY PK, Name nvarchar(50) NULL, Username nvarchar(50) NOT NULL,
///                Password nvarchar(20) NOT NULL **plaintext**, IsEnabled bit NOT NULL).
/// 客戶接受 password 明文（DB 凍結），應用層用 CryptographicOperations.FixedTimeEquals 比對。
/// </remarks>
public sealed class Admin
{
    public int AdminId { get; init; }
    public string? Name { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
    public bool IsEnabled { get; init; }
}
