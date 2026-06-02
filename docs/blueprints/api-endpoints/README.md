---
title: API Endpoints Blueprints Index
purpose: 所有 API endpoint 藍圖的索引；每個 endpoint 一份 markdown，含舊系統對照
applicable_when: 要新增 / 修改 API endpoint、要查某個 endpoint 對應的舊 Form 與 line ref
related_agents:
  - backend-engineer
  - system-analyst
related_docs:
  - ../../design/api-design.md
  - ../legacy-coverage/README.md
keywords: [api, endpoint, blueprint, index, 舊系統對照]
last_updated: 2026-05-27.4
---

> 本目錄是規則 A（forward）的落地處：**每個 API endpoint 必須有一份 blueprint**，列出舊系統對照、業務規則、資料存取與驗收標準。

## 命名規則

- 檔名：`<verb>-<resource>.md`
  - 例：`post-signups.md`、`get-believers.md`、`put-admins.md`、`delete-categories.md`
  - 子資源用連字號：`get-signups-logs.md`、`post-signups-prepay-load.md`
- 路徑大小寫一律小寫
- 路徑變數忽略不入檔名：`PUT /api/v1/admins/:id` → `put-admins.md`

## 維護規則

1. **新增 endpoint 前**：複製 `_template.md`，填完「舊系統對照」段才能開工 code
2. **實作完成後**：回頭把對應 [legacy-coverage/<form>.md](../legacy-coverage/) 行勾選為 `✅ 已實作`，並把本檔 `status` 改為 `shipped`
3. **PR 描述**必須含：(a) 本 blueprint 連結 (b) 對應 `legacy-coverage/<form>.md` 已勾選的行號
4. **不在 plan 階段預先建滿所有 endpoint**：實作該 endpoint 時才建檔

## 索引（依舊 Form 分組）

> 進度：**30 個 endpoint shipped**（含全部 5 個單筆列印變體 + batch + backup + backup download + logout）。列印 PoC 已確認 **QuestPDF 路徑**（RDLC 在 .NET 10 不可行）；批次列印用 **PdfSharp 6.2.4** 合併；列印 5 變體已產出真實 PDF（worship batch 1..200 → 2634 頁），variant-specific 座標 / worship2.png 背景 / PhotoAddress PNG 仍待印表機實機驗收後精修。Auth 完整含 logout（JWT 黑名單）。

### Auth & Admin（LoginForm + MainForm + AdminsForm）

| Endpoint | Blueprint | 舊 Form line | Legacy Coverage | Status |
|---|---|---|---|---|
| `POST /api/v1/auth/login` | [post-auth-login.md](post-auth-login.md) | `LoginForm.cs:31-55, 58-81` | [login-form.md](../legacy-coverage/login-form.md) row 2-3 ✅ | **shipped** |
| `POST /api/v1/auth/logout` | [post-auth-logout.md](post-auth-logout.md) | (N/A — 舊系統無；新需求) | – (新需求；不影響舊 Form coverage) | **shipped** |
| `POST /api/v1/backup` | — | `MainForm.cs:95-113` (btnBackup_Click) | [main-form.md](../legacy-coverage/main-form.md) row 8 ✅ | **shipped** |
| `GET /api/v1/backup/:file/download` | [get-backup-download.md](get-backup-download.md) | (N/A — 新需求；Electron 另存) | – (新需求；不影響舊 Form coverage) | **shipped** |
| `GET /api/v1/admins` | [get-admins.md](get-admins.md) | `AdminsForm.cs:207-213` (LoadAdmins) | [admins-form.md](../legacy-coverage/admins-form.md) row 1,12 ✅ | **shipped** |
| `POST /api/v1/admins` | [post-admins.md](post-admins.md) | `AdminsForm.cs:88-122, 160-205` | [admins-form.md](../legacy-coverage/admins-form.md) rows 3,6,9,10 ✅ | **shipped** |
| `PUT /api/v1/admins/:id` | — | `AdminsForm.cs:88-122` (同上 update path) | [admins-form.md](../legacy-coverage/admins-form.md) row 6 ✅ | **shipped** |
| `DELETE /api/v1/admins/:id` | — | `AdminsForm.cs:134-158` (tsmiDelete 軟刪 IsEnabled=0) | [admins-form.md](../legacy-coverage/admins-form.md) row 8 ✅ | **shipped** |

### Believer（BelieverForm）

| Endpoint | Blueprint | 舊 Form line | Legacy Coverage | Status |
|---|---|---|---|---|
| `GET /api/v1/believers` | [get-believers.md](get-believers.md) | `BelieverForm.cs:35-44, 353-409` | [believer-form.md](../legacy-coverage/believer-form.md) rows 2,13 ✅ | **shipped** |
| `GET /api/v1/believers/:id` | [get-believer-by-id.md](get-believer-by-id.md) | `BelieverForm.cs:57-99` | [believer-form.md](../legacy-coverage/believer-form.md) row 4 ✅ | **shipped** |
| `POST /api/v1/believers` | [post-believers.md](post-believers.md) | `BelieverForm.cs:101-152, 320-351` | [believer-form.md](../legacy-coverage/believer-form.md) rows 3,5,12 ✅ | **shipped** |
| `PUT /api/v1/believers/:id` | [put-believer.md](put-believer.md) | `BelieverForm.cs:154-185` | [believer-form.md](../legacy-coverage/believer-form.md) row 5 ✅ | **shipped** |
| `DELETE /api/v1/believers/:id` | [delete-believer.md](delete-believer.md) | `BelieverForm.cs:211-250` | [believer-form.md](../legacy-coverage/believer-form.md) row 8 ✅ | **shipped** |

### Signup（SignupForm + NewSignupForm + EditSignupForm + SignupLogForm）

| Endpoint | Blueprint | 舊 Form line | Legacy Coverage | Status |
|---|---|---|---|---|
| `GET /api/v1/signups` | [get-signups.md](get-signups.md) | `SignupForm.cs:71-74, 807-864` | [signup-form.md](../legacy-coverage/signup-form.md) rows 1,2,24 ✅ | **shipped** |
| `POST /api/v1/signups` | [post-signups.md](post-signups.md) | `NewSignupForm.cs:151-362` | [new-signup-form.md](../legacy-coverage/new-signup-form.md) rows 6,14-18,25 ✅ | **shipped** |
| `GET /api/v1/signups/:id` | [get-signup-by-id.md](get-signup-by-id.md) | `EditSignupForm.cs:70-73, 562-626` | [edit-signup-form.md](../legacy-coverage/edit-signup-form.md) row 2 ✅ | **shipped** |
| `PUT /api/v1/signups/:id` | [put-signup.md](put-signup.md) | `EditSignupForm.cs:186-368` | [edit-signup-form.md](../legacy-coverage/edit-signup-form.md) rows 9-13 ✅ | **shipped** |
| `DELETE /api/v1/signups/:id` | [delete-signup.md](delete-signup.md) | `SignupForm.cs:405-426` | [signup-form.md](../legacy-coverage/signup-form.md) row 14 ✅ | **shipped** |
| `GET /api/v1/signups/:id/logs` | [get-signup-logs.md](get-signup-logs.md) | `SignupLogForm.cs:26-45` | [signup-log-form.md](../legacy-coverage/signup-log-form.md) rows 1-2 ✅ | **shipped** |
| `POST /api/v1/signups/export` | [post-signups-export.md](post-signups-export.md) | `SignupForm.cs:655-728` | [signup-form.md](../legacy-coverage/signup-form.md) row 17 ✅ | **shipped** |

### Prepay（LoadPrepayForm）

| Endpoint | Blueprint | 舊 Form line | Legacy Coverage | Status |
|---|---|---|---|---|
| `POST /api/v1/prepay/load` | [post-prepay-load.md](post-prepay-load.md) | `LoadPrepayForm.cs:45-824` | [load-prepay-form.md](../legacy-coverage/load-prepay-form.md) **all 8 rows** ✅ | **shipped** |
| `GET /api/v1/prepay?believerId=` | [get-prepay-believer-latest.md](get-prepay-believer-latest.md) | `NewSignupForm.cs:1102-1115` | [new-signup-form.md](../legacy-coverage/new-signup-form.md) **row 34** ✅ | **shipped** |

### Category（CeremonyCategoryForm）

| Endpoint | Blueprint | 舊 Form line | Legacy Coverage | Status |
|---|---|---|---|---|
| `GET /api/v1/categories` | [get-categories.md](get-categories.md) | `CeremonyCategoryForm.cs:167-195` | [ceremony-category-form.md](../legacy-coverage/ceremony-category-form.md) rows 1,7,8,9 ✅ | **shipped** |
| `POST /api/v1/categories` | [post-categories.md](post-categories.md) | `CeremonyCategoryForm.cs:94-114` | [ceremony-category-form.md](../legacy-coverage/ceremony-category-form.md) rows 3,4 ✅ | **shipped** |
| `PUT /api/v1/categories/:id` | [put-category.md](put-category.md) | `CeremonyCategoryForm.cs:115-127` | [ceremony-category-form.md](../legacy-coverage/ceremony-category-form.md) row 4 ✅ | **shipped** |
| `DELETE /api/v1/categories/:id` | [delete-category.md](delete-category.md) | `CeremonyCategoryForm.cs:143-165` | [ceremony-category-form.md](../legacy-coverage/ceremony-category-form.md) row 6 ✅ | **shipped** |

### Reports

| Endpoint | Blueprint | 舊 Form line | Legacy Coverage | Status |
|---|---|---|---|---|
| `GET /api/v1/reports/datacard` | (in [printing-reports.md](../printing-reports.md)) | `SignupForm.cs:956-1050` + tmpDataCard.rdlc | [signup-form.md](../legacy-coverage/signup-form.md) rows 9,28 ✅ | **shipped (PoC)** |
| `GET /api/v1/reports/receipt` | — | `SignupForm.cs:1052-1146` (PrintReceipt) | [signup-form.md](../legacy-coverage/signup-form.md) rows 10,29 ✅ | **shipped** |
| `GET /api/v1/reports/tablet` | — | **`SignupForm.cs:1148-1333`** (PrintTablet 9 變體) | [signup-form.md](../legacy-coverage/signup-form.md) rows 11,30 ✅ | **shipped (selector + base render)** |
| `GET /api/v1/reports/text` | — | **`SignupForm.cs:1335-1552`** (PrintText 2 變體) | [signup-form.md](../legacy-coverage/signup-form.md) rows 12,31 ✅ | **shipped (selector + base render)** |
| `GET /api/v1/reports/worship` | — | **`SignupForm.cs:1554-1696`** (PrintWorship 5 變體) | [signup-form.md](../legacy-coverage/signup-form.md) rows 13,32 ✅ | **shipped (selector + base render; only SignupType=4)** |
| `POST /api/v1/reports/batch` | [post-reports-batch.md](post-reports-batch.md) | `SignupForm.cs:447-653` (btnPrint_Click 編號範圍) + `CombinePDFs` 1698-1722 | [signup-form.md](../legacy-coverage/signup-form.md) rows 16, 33 ✅ | **shipped** |

> 列印變體選擇邏輯（薦牌 9 / 文牒 2 / 普桌 6）已收斂到 `Domain.Services.PrintTemplateSelector` 純函式 + xUnit 測試覆蓋。Renderer 目前以「base variant 座標」實作，variant-specific 細部偏移（薦牌 9 個位置 / 文牒 2 個版型 / 普桌字級切換）已預留 TODO；最終 ground truth 仍是 [printing-reports-positions.md](../printing-reports-positions.md)。客端實機印表測試後再決定是否進一步精修。

## 相關文件

- [api-design.md](../../design/api-design.md) — REST 通則 / 統一錯誤回應 / 業務錯誤碼
- [backend-design.md](../../design/backend-design.md) — 模組結構 / 業務邏輯歸屬
- [legacy-coverage/README.md](../legacy-coverage/README.md) — 反向稽核索引
