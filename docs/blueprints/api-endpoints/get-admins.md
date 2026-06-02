---
title: GET /api/v1/admins
purpose: 列出所有啟用的管理者；前端 AdminsListPage 與其他下拉選單使用
status: shipped
endpoint: get-admins
http_method: GET
route: /api/v1/admins
legacy_form: AdminsForm.cs
legacy_lines: 207-213, 21-31
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../../design/api-design.md
  - ../../design/backend-design.md
  - ../../design/database-design.md
  - ../legacy-coverage/admins-form.md
  - ../auth-and-admin.md
keywords: [admins, list, query, dapper]
last_updated: 2026-05-27
---

## 規格

### Route + Method

`GET` `/api/v1/admins`

需要 `Authorization: Bearer <JWT>` header（從 `POST /api/v1/auth/login` 取得）。

### Request

無 body。後續可加 `?includeDisabled=true`（admin only）— 先不做。

### Response DTO

```jsonc
// 200 OK
{
  "items": [
    {
      "id": 1,
      "username": "alice",
      "name": "Alice Wang"
    }
  ],
  "total": 1
}
```

**不回傳 `Password` 欄位**（明文密碼禁止外洩）。

### 錯誤碼

| HTTP | errorCode | 觸發條件 |
|---|---|---|
| 401 | (空 body) | 未帶 JWT 或 token 失效 |
| 500 | `INTERNAL_ERROR` | DB 連線失敗等未預期例外 |

## 舊系統對照（規則 A — forward）

### 對應 Form / 事件

| 舊方法/事件 | 舊 code line | 行為摘要 |
|---|---|---|
| `AdminsForm()` constructor | `AdminsForm.cs:21-31` | 構造表單時呼叫 `LoadAdmins()` |
| `AdminsForm.LoadAdmins()` helper | `AdminsForm.cs:207-213` | 從 `_service.GetAll()` 載入 + 繫結至 `dgvAdmins` |

### 驗證規則對照

無欄位驗證（純查詢）。

### 業務邏輯區塊

1. **載入啟用清單**（舊：`AdminsForm.cs:207-213`）
   - 舊行為：`dgvAdmins.DataSource = _service.GetAll()`（內部 LINQ `Where(a => a.IsEnabled)` 推測）
   - 新實作：`AdminRepository.GetAllEnabledAsync()` 透過 Dapper：`SELECT AdminID, Name, Username, IsEnabled FROM dbo.Admins WHERE IsEnabled = 1 ORDER BY Username`
   - **不選 Password 欄位**（API 層防呆，密碼絕不離開 backend）

### 邊界 case

| 場景 | 舊行為 | 新版行為 | 對應測試 |
|---|---|---|---|
| 無啟用 admin（理論上不該發生，因有後門） | 空 grid | `{items: [], total: 0}` | (Repository test, 後續) |
| 含 1 個 admin | 1 列 | 1 個 item | 整合測試 |
| 軟刪除（IsEnabled=false） | 不顯示 | 不回傳 | (整合測試) |

## 業務規則

- 軟刪除：`Admins.IsEnabled=false` 不回傳（沿用舊系統邏輯）
- **不外洩 password**：Dapper 查詢明確不 select Password 欄位

## 資料存取

### 相關資料表

| Table | 用途 | 索引 | 注意 |
|---|---|---|---|
| `Admins` | 主表 | 無 | 明文 password（**API 層必過濾**） |

### 預期 SQL

```sql
SELECT AdminID, Name, Username, IsEnabled
FROM dbo.Admins
WHERE IsEnabled = 1
ORDER BY Username
```

### Repository 方法

新實作：
- `IAdminRepository.GetAllEnabledAsync(CancellationToken) → IReadOnlyList<Admin>`
- 既有：`GetByUsernameAsync` (for login)

## 驗收標準

- [x] 規格段所有欄位有 DTO 型別 + 範例
- [x] 舊系統對照表已逐行對到舊 code line ref
- [x] **不外洩 Password**：response DTO 無 password 欄位、SQL 不 select Password
- [x] `[Authorize]` 套用（未帶 JWT 回 401）
- [x] 對應的 `legacy-coverage/admins-form.md` row 1, 12 已勾選為 `✅ 已實作`
- [x] 手動 smoke test 通過（未帶 token 401 / 帶 token 200）
- [ ] 含整合測試（WebApplicationFactory）— 後續任務
- [ ] 通過 [code-review](../../workflows/code-review.md)

## 風險與未解問題

- **constructor row 1 的「LoadAdmins 呼叫」邏輯本身已在 helper 涵蓋**：constructor 本體仍含 UI 控件初始化（待前端 AdminsListPage 實作後標 ❌ 故意捨棄 — 對應 UI 行為由前端負責）；目前先標 ✅ 已實作（API 部分），UI 部分由 frontend coverage 處理

## 參考

- 舊 Form：`reference/old/Ceremony/AdminsForm.cs:21-31, 207-213`
- Legacy coverage：[../legacy-coverage/admins-form.md](../legacy-coverage/admins-form.md)
- 相關 blueprint：[../auth-and-admin.md](../auth-and-admin.md)
