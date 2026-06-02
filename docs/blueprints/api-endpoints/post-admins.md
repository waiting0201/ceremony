---
title: POST /api/v1/admins
purpose: 新增管理者（含 username 唯一性檢查）
status: shipped
endpoint: post-admins
http_method: POST
route: /api/v1/admins
legacy_form: AdminsForm.cs
legacy_lines: 88-122, 160-187, 189-196
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../../design/api-design.md
  - ../../design/database-design.md
  - ../legacy-coverage/admins-form.md
  - get-admins.md
keywords: [admins, create, post, dapper, write]
last_updated: 2026-05-27
---

## 規格

### Route + Method

`POST` `/api/v1/admins`

需要 JWT bearer auth (`Authorization: Bearer ...`)。

### Request DTO

```jsonc
{
  "username": "string (required, max 50, must be unique)",
  "password": "string (required, max 20 — DB schema 凍結)",
  "name": "string (optional, max 50)"
}
```

### Response DTO

```jsonc
// 201 Created
// Location: /api/v1/admins/{id}
{
  "id": 123,
  "username": "alice",
  "name": "Alice Wang"
}
// **不回傳 password**
```

### 錯誤碼

| HTTP | errorCode | message (verbatim 對齊舊 MessageBox) | 觸發條件 |
|---|---|---|---|
| 400 | `VALIDATION_REQUIRED` | `請輸入帳號` | username trimmed 為空 |
| 400 | `VALIDATION_REQUIRED` | `請輸入密碼` | password trimmed 為空 |
| 400 | `VALIDATION_LENGTH` | `密碼最多 20 個字` | password 長度 > 20（DB 凍結） |
| 409 | `ADMIN_DUPLICATE_USERNAME` | `帳號重複，請重新確認！` | username 已存在 |
| 401 | (空) | – | 無 JWT |

## 舊系統對照（規則 A — forward）

### 對應 Form / 事件

| 舊方法/事件 | 舊 code line | 行為摘要 |
|---|---|---|
| `AdminsForm.btnConfirm_Click` (insert path) | `AdminsForm.cs:88-105` | 新建 Admins entity (Username/Password/Name trimmed, IsEnabled=true) → Create → SaveChanges → 訊息「新增帳號成功！」|
| `AdminsForm.txtUsername_Validating` | `AdminsForm.cs:160-187` | 驗證 username 非空 + **唯一性檢查**（新增時：用 Username 查；編輯時：排除自己） |
| `AdminsForm.txtPassword_Validating` | `AdminsForm.cs:189-196` | 驗證 password 非空 |
| `AdminsForm.txtConfirmPassword_Validating` | `AdminsForm.cs:198-205` | 確認密碼比對 — **新版前端負責**（兩個欄位送出前比對；API 只收 1 個 password） |

### 驗證規則對照

| 欄位 | 舊驗證 (line) | 新驗證 | 差異 / 取捨 |
|---|---|---|---|
| `username` | trim+空檢查 `AdminsForm.cs:162` | `[Required] + Trim` + 應用層 unique check | 等價；新版 unique check 走 `IAdminRepository.UsernameExistsAsync` |
| `username` 唯一性 | `adminsService.Get().FirstOrDefault(a => a.Username == ...)` line 173 | `SELECT COUNT(1) FROM Admins WHERE Username=@u`（DB 凍結，無 UNIQUE constraint） | 等價 |
| `password` | trim+空檢查 line 191 | `[Required] + Trim + MaxLength(20)` | 補強 MaxLength（對應 DB nvarchar(20)） |
| `name` | trim only | `Trim + MaxLength(50)` | 補強 |

### 業務邏輯區塊

1. **Trim 處理**：舊系統所有欄位 `Text.Trim()`。新版 handler 也 trim（API 層 boundary）
2. **預設 `IsEnabled=true`**：新建一律啟用（與舊系統 line 98 一致）
3. **無 password 雜湊**：明文存（DB 凍結，客戶接受）— 詳見 [security.md 已知接受風險](../../design/security.md)
4. **舊系統「確認密碼」雙欄位**：在新版 web/前端比對；API 只收 1 個 `password` 欄位

### 邊界 case

| 場景 | 舊行為 | 新版行為 | 對應測試 |
|---|---|---|---|
| 空 username | 400 +「請輸入帳號」 | 400 `VALIDATION_REQUIRED` | TestCreate_EmptyUsername |
| 空 password | 400 +「請輸入密碼」 | 400 `VALIDATION_REQUIRED` | TestCreate_EmptyPassword |
| password > 20 字元 | DB 截斷（隱性） | 400 `VALIDATION_LENGTH` 明確擋下 | TestCreate_PasswordTooLong |
| username 重複 | 409 +「帳號重複，請重新確認！」 | 同 errorCode | TestCreate_DuplicateUsername |
| 所有欄位前後空白 | trim 處理 | 同 | TestCreate_TrimsWhitespace |

## 業務規則

- DB 無 UNIQUE constraint 於 `Username`，應用層必 enforce — 詳見 [database-design.md §1 Admins](../../design/database-design.md)
- 明文密碼（[security.md](../../design/security.md) 已知接受風險）

## 資料存取

### 預期 SQL

```sql
-- 唯一性檢查
SELECT COUNT(1) FROM dbo.Admins WHERE Username = @Username

-- Insert
INSERT INTO dbo.Admins (Name, Username, Password, IsEnabled)
OUTPUT INSERTED.AdminID
VALUES (@Name, @Username, @Password, 1)
```

### Repository 方法（新增）

- `IAdminRepository.UsernameExistsAsync(string username, int? excludeId = null, CancellationToken)` → bool
- `IAdminRepository.InsertAsync(CreateAdminCommand cmd, CancellationToken)` → int (new AdminID)

## 驗收標準

- [x] 規格段所有欄位有 DTO 型別 + 驗證 + 範例
- [x] 舊系統對照表逐行對到舊 code line ref
- [x] 錯誤碼與舊 MessageBox 文字 verbatim（「請輸入帳號」「請輸入密碼」「帳號重複，請重新確認！」）
- [x] 對應 `legacy-coverage/admins-form.md` row 3, 6, 9, 10 已勾選 ✅
- [x] 含 unit tests (CreateAdminHandlerTests)
- [x] 含 integration test (POST 成功 + 重複 username 409)
- [x] **不外洩 Password** — response DTO 無 password 欄位
- [ ] 通過 [code-review](../../workflows/code-review.md)

## 風險與未解問題

- **明文密碼進 HTTP body 是合理嗎**：是；DB 凍結唯一方式。HTTPS 為必要前提（[infrastructure.md](../../design/infrastructure.md)）

## 參考

- 舊 Form：`reference/old/Ceremony/AdminsForm.cs:88-122, 160-205`
- 配對：[get-admins.md](get-admins.md)（list）
- Legacy coverage：[../legacy-coverage/admins-form.md](../legacy-coverage/admins-form.md)
