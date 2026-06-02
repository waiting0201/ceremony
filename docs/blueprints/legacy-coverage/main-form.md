---
title: MainForm Legacy Coverage
purpose: 反向稽核 — MainForm 所有方法/事件的新系統對應狀態
applicable_when: 完成 ShellLayout / 主導覽相關 endpoint 後勾選；月度稽核；上線前 gate
legacy_form: MainForm.cs
legacy_path: reference/old/Ceremony/MainForm.cs
legacy_lines: 115
audit_status: complete
coverage_percentage: 100
last_audited: 2026-06-02
baseline_completed: 2026-05-27
total_methods: 8
related_agents:
  - backend-engineer
  - frontend-architect
  - qa-test-engineer
related_docs:
  - ../api-endpoints/README.md
  - README.md
keywords: [legacy, coverage, main, shell]
last_updated: 2026-06-02
---

> ✅ **完成 (2026-06-02)**：8 個方法全部已對應。導覽按鈕 → Angular 路由（`app.routes.ts`）、備份 end-to-end、視窗生命週期以 Web 慣用語重新表達（ShellLayout + authGuard）。

## 稽核總覽

| 項目 | 值 |
|---|---|
| 總方法 / 事件 / 邏輯區塊數 | 8 |
| ✅ 已實作 | 8 |
| ❌ 故意捨棄 | 0 |
| ⏳ 缺口待補 | 0 |
| 🤔 待確認 | 0 |
| 覆蓋率 | 100% |

## 方法 / 事件 / 邏輯區塊清單

| # | 舊方法/事件 | 舊 code line | 行為摘要 | 新系統狀態 | 對應 endpoint / 前端 | 備註 |
|---|---|---|---|---|---|---|
| 1 | `MainForm()` constructor | 18-30 | 顯示登入表單；初始化；設定版本標籤 | ✅ 已實作 | 前端 `ShellLayout` | Web 慣用語重新表達（非缺漏）：`ShellLayout` 為主框架，版本標籤 `shell-layout.html` `{{ appVersion }}`（自 `environment.version`），登入頁版本標籤見 login-form #1 |
| 2 | `LoginFormClosed()` event handler | 32-38 | 未登入時退出應用程式 | ✅ 已實作 | 前端 `authGuard` | Web 慣用語重新表達（非缺漏）：`authGuard` 未登入時 `router.createUrlTree(['/login'])` redirect 至 `/login`（取代桌面「退出應用程式」） |
| 3 | `btnAdmins_Click` event handler | 45-49 | 開啟管理者維護表單 | ✅ 已實作 | 前端 `/admins` 路由 | `app.routes.ts` `path: 'admins'` → `AdminsPage`（authGuard 保護）|
| 4 | `btnBeliever_Click` event handler | 56-60 | 開啟信眾維護表單 | ✅ 已實作 | 前端 `/believers` 路由 | `path: 'believers'` → `BelieversPage` |
| 5 | `btnSignup_Click` event handler | 67-71 | 開啟報名維護表單 | ✅ 已實作 | 前端 `/signups` 路由 | `path: 'signups'` → `SignupListPage` |
| 6 | `btnNewSignup_Click` event handler | 78-82 | 開啟新增報名表單 | ✅ 已實作 | 前端 `/signups/new` 路由 | `path: 'signups/new'` → `SignupEditPage` |
| 7 | `btnPreload_Click` event handler | 89-93 | 開啟預繳載入表單 | ✅ 已實作 | 前端 `/prepay` 路由 | `path: 'prepay'` → `PrepayPage` |
| 8 | `btnBackup_Click` event handler | 95-113 | 執行 SQL 資料庫備份；建立備份資料夾；顯示完成訊息 | ✅ 已實作（**end-to-end 完成 2026-05-29**） | `POST /api/v1/backup` + 前端 `/backup` 頁 | `SqlBackupService` 已**對齊舊 code**：檔名 `{yyyyMMddHHmmssffffff}.bak`（6 位微秒、無前綴）；SQL flags `WITH NOFORMAT, NOINIT, NAME = N'Ceremony-Full Database Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10`；DB 名稱由 `conn.Database` 動態取得；目錄不存在自動建立（優於舊 hardcode `D:\Backup\`）；config 未設則 500 `BACKUP_NOT_CONFIGURED`。**前端「完成訊息」UI 已補**：`backup-page` 走 ConfirmDialog 確認 → 成功 dialog 顯示 fileName / fullPath / size，錯誤顯示後端 verbatim 中文訊息。`BuildBackup()` pure helper + 4 unit test（檔名格式 / 自訂名 / SQL flags verbatim / 識別子跳脫）。**新增（非舊系統行為，2026-05-29）**：`clearLog` 選項 — 備份後依 recovery model 安全清交易紀錄檔（FULL→`BACKUP LOG`+.trn+`SHRINKFILE`、SIMPLE→`CHECKPOINT`+`SHRINKFILE`），`BuildClearLog()` pure helper +6 unit test；前端加 checkbox + 警語 |
