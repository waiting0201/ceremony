---
title: AdminsForm Legacy Coverage
purpose: 反向稽核 — AdminsForm 所有方法/事件的新系統對應狀態
applicable_when: 完成 admins CRUD endpoint 後勾選；月度稽核；上線前 gate
legacy_form: AdminsForm.cs
legacy_path: reference/old/Ceremony/AdminsForm.cs
legacy_lines: 241
audit_status: complete
coverage_percentage: 100
last_audited: 2026-06-02
baseline_completed: 2026-05-27
total_methods: 14
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../api-endpoints/README.md
  - README.md
keywords: [legacy, coverage, admins]
last_updated: 2026-06-02
---

> ✅ **完成 (2026-06-02)**：14 個方法中 13 個已實作、1 個故意捨棄（`ProcessCmdKey` Enter→Tab，改用標準 Enter=submit）。CRUD + 軟刪除 + 前端 form 行為全 ship。

## 稽核總覽

| 項目 | 值 |
|---|---|
| 總方法 / 事件 / 邏輯區塊數 | 14 |
| ✅ 已實作 | 13 |
| ❌ 故意捨棄 | 1 |
| ⏳ 缺口待補 | 0 |
| 🤔 待確認 | 0 |
| 覆蓋率 | 100% |

## 方法 / 事件 / 邏輯區塊清單

| # | 舊方法/事件 | 舊 code line | 行為摘要 | 新系統狀態 | 對應 endpoint | 備註 |
|---|---|---|---|---|---|---|
| 1 | `AdminsForm()` constructor | 21-31 | 初始化表單與服務，清空表單並載入管理員列表 | ✅ 已實作 | `GET /api/v1/admins` | 已驗證 6 個真實 admin 從 (local) Ceremony DB 取出 |
| 2 | `ProcessCmdKey()` override | 39-48 | Enter 鍵轉換為 Tab 鍵功能 | ❌ 故意捨棄 | – | **新版改用標準 Enter=submit**（[frontend-design.md](../../design/frontend-design.md) 已決議）；Web 慣例不做 Enter→Tab 轉換 |
| 3 | `btnNew_Click` | 50-56 | 清空選擇，清空表單，啟用編輯模式 | ✅ 已實作 | 前端 form mode + `POST /api/v1/admins` | UI 行為由前端負責；後端 endpoint 已 ship |
| 4 | `btnCancel_Click` | 58-64 | 清空選擇，清空表單，禁用編輯模式 | ✅ 已實作 | 前端 form reset | `admins-page.onOverlayClose()` 清 editTarget；`admin-edit-form` effect `form.reset(...)` |
| 5 | `dgvAdmins_CellClick` | 66-86 | 行選擇後載入該筆管理員資料至表單 | ✅ 已實作 | 前端 row select | `admins-page.startEdit(admin)` 設 editTarget；`admin-edit-form` effect 預填並 `usernameCtrl.disable()`（對齊舊 username 不可改）|
| 6 | `btnConfirm_Click` | 88-122 | 驗證表單 + 新增/修改管理員 + 刷新列表 | ✅ 已實作 | `POST /api/v1/admins` + `PUT /api/v1/admins/:id` | `CreateAdminHandler` (insert) + `UpdateAdminHandler` (update path：只改 name/password，username 不變更，對齊 :84/:108-114) |
| 7 | `dgvAdmins_RowHeaderMouseClick` | 124-132 | 右鍵點擊顯示上下文菜單 | ✅ 已實作 | 前端 context menu | `admins-page.openRowMenu()`（右鍵）+ `openRowMenuFromButton()`（kebab）走共用 `ContextMenuService`；選單含「編輯 / 刪除」|
| 8 | `tsmiDelete_Click` | 134-158 | 確認刪除，軟刪除管理員並刷新列表 | ✅ 已實作 | `DELETE /api/v1/admins/:id` | `DeleteAdminHandler` → `AdminRepository.SoftDeleteAsync`：`UPDATE dbo.Admins SET IsEnabled = 0`（對齊 :143-146 軟刪除）；前端 ConfirmDialog |
| 9 | `txtUsername_Validating` | 160-187 | 驗證帳號不空 + 檢查重複性 | ✅ 已實作 | `POST /api/v1/admins` validator | `CreateAdminHandler`：trim+empty check + `UsernameExistsAsync` SQL COUNT；excludeId 邏輯給 PUT 使用 |
| 10 | `txtPassword_Validating` | 189-196 | 驗證密碼不空 | ✅ 已實作 | `POST /api/v1/admins` validator | trim+empty check + MaxLength 20 (DB schema nvarchar(20)) |
| 11 | `txtConfirmPassword_Validating` | 198-205 | 驗證密碼確認一致性 | ✅ 已實作 | 前端 form validator | `admin-edit-form` group validator `matchPasswords`（password ≠ confirmPassword → `passwordMismatch`）|
| 12 | `LoadAdmins()` helper | 207-213 | 從服務載入全部管理員並繫結至表格 | ✅ 已實作 | `GET /api/v1/admins` | `AdminRepository.GetAllEnabledAsync` (Dapper)，**SQL 不 select Password**（read-model `AdminListItem`） |
| 13 | `PanelFormSwitch()` helper | 215-231 | 循環啟用/禁用表單內所有 TextBox 和 Button | ✅ 已實作 | 前端 form mode | `admins-page` `app-form-overlay`（overlayOpen 控制顯示）+ `admin-edit-form` create/edit 模式切換 validators/disabled |
| 14 | `PanelFormEmpty()` helper | 233-239 | 清空所有表單欄位文本 | ✅ 已實作 | 前端 form reset | `admin-edit-form` effect `form.reset({...})` + `markAsPristine()` |
