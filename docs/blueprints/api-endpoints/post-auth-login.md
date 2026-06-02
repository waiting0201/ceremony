---
title: POST /api/v1/auth/login
purpose: 管理者登入，明文密碼比對 + 後門帳號，回傳 JWT bearer
status: shipped
endpoint: post-auth-login
http_method: POST
route: /api/v1/auth/login
legacy_form: LoginForm.cs
legacy_lines: 31-55, 58-81
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../../design/api-design.md
  - ../../design/backend-design.md
  - ../../design/database-design.md
  - ../../design/security.md
  - ../legacy-coverage/login-form.md
  - ../auth-and-admin.md
keywords: [auth, login, jwt, backdoor, weypro, admins]
last_updated: 2026-05-27
---

## 規格

### Route + Method

`POST` `/api/v1/auth/login`

### Request DTO

```jsonc
{
  "username": "string (required, max 50)",
  "password": "string (required, max 20)"
}
```

### Response DTO

```jsonc
// 200 OK
{
  "token": "eyJhbGciOiJI...",  // JWT bearer token
  "user": {
    "id": 0,                    // AdminID（後門帳號 = 0）
    "username": "weypro",
    "name": "Administrator"     // Admins.Name；後門 = "Administrator"
  }
}
```

### 錯誤碼

| HTTP | errorCode | message (verbatim 對齊舊 MessageBox) | 觸發條件 |
|---|---|---|---|
| 400 | `VALIDATION_REQUIRED` | `請輸入帳號！` | username 空 |
| 400 | `VALIDATION_REQUIRED` | `請輸入密碼！` | password 空 |
| 401 | `AUTH_INVALID_CREDENTIALS` | `帳號或密碼錯誤！` | 比對失敗 或 IsEnabled=false |
| 423 | `AUTH_ACCOUNT_LOCKED` | `登入失敗次數過多，請 15 分鐘後再試` | 失敗次數 ≥ `Auth:FailedLoginThreshold`（in-memory 計數） |

詳見 [api-design.md 業務錯誤碼表](../../design/api-design.md)。

## 舊系統對照（規則 A — forward）

### 對應 Form / 事件

| 舊方法/事件 | 舊 code line | 行為摘要 |
|---|---|---|
| `LoginForm.btnConfirm_Click` | `LoginForm.cs:31-55` | 驗證帳密；空值檢查；關閉視窗或顯示錯誤 |
| `LoginForm.ValidateUser()` | `LoginForm.cs:58-81` | 檢查硬編碼帳密（後門）或查詢 DB；設定 Global 登入狀態 |

### 驗證規則對照

| 欄位 | 舊驗證 (line) | 新驗證 | 差異 / 取捨 |
|---|---|---|---|
| `username` | `LoginForm.cs:33-37` 空字串檢查 + MessageBox | `[Required, MaxLength(50)]` + FluentValidation | 等價 |
| `password` | `LoginForm.cs:39-43` 空字串檢查 + MessageBox | `[Required, MaxLength(20)]` | 等價（Admins.Password 為 nvarchar(20)） |

### 業務邏輯區塊

1. **後門帳號（不寫入 DB）**（舊：`LoginForm.cs:60-65`）
   - 舊行為：`if (username == "weypro" && password == "weypro12ab") { Global.AdminID = 0; ... }`
   - 新實作：同邏輯，受 `Auth:BackdoorEnabled` config 控制；後門啟用時直接發 JWT (`sub=0, name="weypro"`)
   - 差異 / 為什麼：客戶要求保留後門但可關閉

2. **DB 查詢明文比對**（舊：`LoginForm.cs:67-78`）
   - 舊行為：`db.Admins.Where(a => a.Username == u && a.Password == p && a.IsEnabled).FirstOrDefault()`
   - 新實作：Dapper 撈 `SELECT TOP 1 AdminID, Name, Username, Password, IsEnabled FROM Admins WHERE Username = @u AND IsEnabled = 1`，**應用層用 `CryptographicOperations.FixedTimeEquals`** 比對 password（避免 timing attack）
   - 差異 / 為什麼：常數時間比對 + 仍維持明文（DB 凍結）

3. **失敗鎖定**（舊：無）
   - 舊行為：無鎖定機制
   - 新實作：in-memory `IMemoryCache` 計數，per-username 連續失敗 ≥ `Auth:FailedLoginThreshold`(5) 鎖 `Auth:FailedLoginLockMinutes`(15) 分鐘
   - 差異 / 為什麼：補強安全；in-memory 重啟即清，可接受

### 邊界 case

| 場景 | 舊 code 行為 (line) | 新版行為 | 對應測試 |
|---|---|---|---|
| 帳號或密碼為空 | `LoginForm.cs:33-43` 各自 MessageBox | FluentValidation 400 + 兩個 errorCode | TestLogin_EmptyUsername / EmptyPassword |
| 帳號不存在 | `LoginForm.cs:73` 回傳 null → MessageBox | 401 + `AUTH_INVALID_CREDENTIALS`（不洩漏「帳號不存在」） | TestLogin_UnknownUser |
| IsEnabled=false | `LoginForm.cs:67` 條件已過濾 | 401（同上） | TestLogin_DisabledUser |
| 後門帳號 | `LoginForm.cs:60-65` 硬編碼 | 同邏輯，受 `Auth:BackdoorEnabled` config | TestLogin_Backdoor + TestLogin_BackdoorDisabled |
| 密碼大小寫敏感 | 明文 `==` 比對 | `FixedTimeEquals(bytes)` 同樣 case-sensitive | TestLogin_CasePassword |
| 連續失敗 ≥ 5 次 | 無限制 | 423 + 鎖 15 分鐘 | TestLogin_LockoutAfterThreshold |

## 業務規則

- [business-rules-implicit.md](../business-rules-implicit.md) — Admins.Password 明文 (客戶接受)
- [security.md](../../design/security.md) — 已知接受風險：明文密碼 + sa 帳號 + 後門帳號

## 資料存取

### 相關資料表

| Table | 用途 | 索引 | 注意 |
|---|---|---|---|
| `Admins` | 主表 | 無 UNIQUE on Username（應用層 enforce） | 明文 password |

### 預期 SQL

```sql
SELECT TOP 1 AdminID, Name, Username, Password, IsEnabled
FROM dbo.Admins
WHERE Username = @Username AND IsEnabled = 1
```

### Repository 方法

| 舊 Service / Repository 方法 | line | 行為 |
|---|---|---|
| `db.Admins.Where(...)` (LINQ via EF6) | `LoginForm.cs:67-78` | 直接在 Form 內查詢 |

新實作：`Infrastructure.Persistence.AdminRepository.GetByUsernameAsync(string username) → Admin?`

## 驗收標準

- [x] 規格段所有欄位有 DTO 型別 + 驗證 + 範例
- [x] 舊系統對照表已逐行對到舊 code line ref（無遺漏）
- [x] 錯誤碼與舊 MessageBox 文字 verbatim
- [x] 對應的 `legacy-coverage/login-form.md` row 2-3 已勾選為 `✅ 已實作`
- [x] 手動 smoke test 通過 4 case（backdoor 200 / empty 400 / wrong-pwd 401 / DB SELECT 1）
- [ ] 含舊系統行為對照測試（xUnit + 整合測試）— 後續任務
- [ ] 通過 [code-review](../../workflows/code-review.md)
- [ ] 通過 [qa-testing](../../workflows/qa-testing.md)

## 風險與未解問題

- **後門帳號密碼是否要從 config 讀**：目前在 code 硬編碼（沿用舊系統）；建議改為 `Auth:BackdoorPassword` 但與舊系統行為對齊則維持硬編碼
- **失敗鎖定 in-memory 不跨 instance**：未來水平擴展時需改 Redis 或 DB 計數；目前單 instance 部署可接受

## 參考

- 舊 Form：`reference/old/Ceremony/LoginForm.cs:31-81`
- Repository：`reference/old/Ceremony.Models/Repository/GenericRepository.cs`
- Legacy coverage：[../legacy-coverage/login-form.md](../legacy-coverage/login-form.md)
- 相關 blueprint：[../auth-and-admin.md](../auth-and-admin.md)
