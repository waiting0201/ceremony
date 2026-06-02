namespace Ceremony.Application.Admins;

/// <summary>
/// 管理者列表 read-model（**不含 Password**）。
/// 對應 API GET /api/v1/admins response items。
/// </summary>
public sealed record AdminListItem(int Id, string Username, string? Name);

public sealed record AdminListResponse(IReadOnlyList<AdminListItem> Items, int Total);

/// <summary>
/// 新增管理者請求。對應 AdminsForm.cs:88-105 的 insert path。
/// 確認密碼欄位由前端送出前比對；API 只收 1 個 password。
/// </summary>
public sealed record CreateAdminRequest(string Username, string Password, string? Name);

/// <summary>
/// 更新管理者請求。Username 不可變更（對齊 AdminsForm.cs:84 `txtUsername.Enabled = false`）。
/// Password 為 null 視為「不變更密碼」；提供值則需通過驗證。
/// </summary>
public sealed record UpdateAdminRequest(string? Password, string? Name);
