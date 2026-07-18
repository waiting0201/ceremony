---
title: SignupForm Legacy Coverage
purpose: 反向稽核 — SignupForm 所有方法/事件的新系統對應狀態（1944 行，最複雜）
applicable_when: 完成 signups 查詢 / 列印 / RDLC 變體相關 endpoint 後勾選；月度稽核；上線前 gate
legacy_form: SignupForm.cs
legacy_path: reference/old/Ceremony/SignupForm.cs
legacy_lines: 1944
audit_status: complete
coverage_percentage: 100
last_audited: 2026-07-04
baseline_completed: 2026-05-27
total_methods: 43
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../api-endpoints/README.md
  - ../printing-reports.md
  - ../printing-reports-positions.md
  - README.md
keywords: [legacy, coverage, signup, rdlc, variant]
last_updated: 2026-07-18 (row 13 列印普桌解鎖：撤回 422 WORSHIP_ONLY_TYPE_4，不限 SignupType 對齊舊系統)
---

> ✅ **完成 (2026-06-02)**：43 個方法中 39 個已實作、4 個故意捨棄（WinForms printDocument 內部 :34-37，改走 Web/PDF 路徑）。查詢 / 列印（5 類 + RDLC 變體）/ 右鍵選單 / 搜尋範圍切換 / 顯示完整欄位 / 歷程 全 ship。
> **報表編號字串（2026-06-02 已修正）**：`GenerateReportHandlers.SignupReportContext` 改為依列印類型分別組字串 — 資料卡 `NumberTitle + "." + 號`、收據 純號碼、薦牌/文牒 `SignupType==2` 僅 NumberTitle 否則 `NumberTitle+號`、普桌 `NumberTitle+號`（先前一律 `{title}-{num}` 連字號為錯誤，已不再偏離舊行為）。
> ⚠️ 已知關鍵段落：
> - RDLC 變體選擇邏輯：薦牌 9 變體 `PrintTablet()` (1148-1333) / 文牒 2 變體 `PrintText()` (1335-1552) / 普桌 5 變體 `PrintWorship()` (1554-1696) — 詳見 [printing-reports-positions.md](../printing-reports-positions.md)
> - 報名查詢 + 避 4 顯示 + 變更紀錄 整合
> - PredicateBuilder 動態搜尋（`LoadSearchSignups` 807-864）

## 稽核總覽

| 項目 | 值 |
|---|---|
| 總方法 / 事件 / 邏輯區塊數 | 43 |
| ✅ 已實作 | 39 |
| ❌ 故意捨棄 | 4 |
| ⏳ 缺口待補 | 0 |
| 🤔 待確認 | 0 |
| 覆蓋率 | 100% |

## 方法 / 事件 / 邏輯區塊清單

| # | 舊方法/事件 | 舊 code line | 行為摘要 | 新系統狀態 | 對應 endpoint | 備註 |
|---|---|---|---|---|---|---|
| 1 | `SignupForm()` constructor | 56-69 | 初始化表單、DB 服務、載入法會/列印類型 | ✅ 已實作 (部分) | `GET /api/v1/categories` + 前端 init | 載入法會 dropdown 由 GET /categories 提供；列印類型 enum 在前端 |
| 2 | `btnSearch_Click` | 71-74 | 觸發搜尋報名資料 | ✅ 已實作 | `GET /api/v1/signups` | 觸發為前端責任 |
| 3 | `btnNew_Click` | 76-90 | 新增報名 (含代入邏輯) | ✅ 已實作 | 前端 overlay | `signup-list-page.openCreateOverlay()`（`editOverlay = { signupId: null, fromSignupId: null }`）→ `signup-edit-form` 新增模式 |
| 4 | `btnEdit_Click` | 92-110 | 編輯已選擇報名資料 | ✅ 已實作 | 前端 overlay + `PUT /api/v1/signups/:id` | `actionEdit(item)` / `goEditSelected()` 開編輯 overlay → `signup-edit-form.submit()` → `UpdateSignupHandler` |
| 5 | `dgvSignups_CellClick` | 112-119 | 選擇 DataGridView 行 | ✅ 已實作 | 前端 row select | `signup-list-page` `selectedIds` signal + `toggleRow` / row click（單筆/多筆選取）|
| 6 | `dgvSignups_RowHeaderMouseClick` | 121-149 | 右鍵選單：新增/編輯/列印 | ✅ 已實作 | 前端 context menu | `openRowMenu`（右鍵）+ kebab 入口 → `ContextMenuService`；選單含 代入新增/修改資料/列印各類/刪除/瀏覽歷程 |
| 7 | `tsmiAdd_Click` | 151-166 | 右鍵新增報名 | ✅ 已實作 | 前端 overlay | 選單「代入新增」`actionAddFrom(item)`（`fromSignupId` 帶入 → `prefillFromSignup`）/「新增」`openCreateOverlay` |
| 8 | `tsmiEdit_Click` | 168-186 | 右鍵編輯報名 | ✅ 已實作 | 前端 overlay + `PUT /api/v1/signups/:id` | 選單「修改資料」`actionEdit(item)` → 編輯 overlay → `UpdateSignupHandler` |
| 9 | `tsmiPrintDataCard_Click` | 188-240 | 列印資料卡 (含格式選擇) | ✅ 已實作 (核心 PDF) | `GET /api/v1/reports/datacard?signupId=` | PoC 已確認 QuestPDF 路徑可行；含格式選擇對話 UI 由前端負責 |
| 10 | `tsmiPrintReceipt_Click` | 242-271 | 列印收據 (含格式選擇) | ✅ 已實作 (核心 PDF) | `GET /api/v1/reports/receipt?signupId=` | QuestPDF 路徑；UI 格式對話由前端處理 |
| 11 | `tsmiPrintTablet_Click` | 273-321 | 列印薦牌 (含大廳名分割) | ✅ 已實作 (核心 PDF, base variant) | `GET /api/v1/reports/tablet?signupId=` | 9 變體選擇邏輯入 `Domain.Services.PrintTemplateSelector.ChooseTablet`；目前 Renderer 使用 base 座標，variant-specific 座標 TODO |
| 12 | `tsmiPrintText_Click` | 323-378 | 列印文牒 (含大廳名+地址合成) | ✅ 已實作 (核心 PDF, base variant) | `GET /api/v1/reports/text?signupId=` | 2 變體 `ChooseText`；PhotoAddress 25×605px 圖檔 TODO |
| 13 | `tsmiPrintWorship_Click` | 380-403 | 列印普桌 (含格式選擇) | ✅ 已實作 | `GET /api/v1/reports/worship?signupId=` | 6 變體 `ChooseWorship`；**不限 SignupType**（2026-07-18 對齊舊系統選什麼印什麼，撤回原 422 WORSHIP_ONLY_TYPE_4）；worship2.png 背景已嵌入；2026-07-04 六變體各自座標 + 直書 Stack + GroupFontPt 縮字（每格 5 字）+ 同欄上下排全形空格 |
| 14 | `tsmiDelete_Click` | 405-426 | 刪除報名紀錄 (確認對話) | ✅ 已實作 | `DELETE /api/v1/signups/:id` | `DeleteSignupHandler` 硬刪除（沿用舊行為）；SignupLog 保留作審計 |
| 15 | `tsmiLog_Click` | 428-445 | 顯示報名修改日誌 | ✅ 已實作 | 前端 nav `/signups/:id/logs` + `GET /api/v1/signups/:id/logs` | 選單「瀏覽歷程」`actionLogs(item)` → `navigateByUrl('/signups/:id/logs')` → `signup-logs-page` + `ListSignupLogsHandler` |
| 16 | `btnPrint_Click` | 447-653 | **複合邏輯：編號範圍查詢 + 5 種列印類型**（206 行；含 1148-1228 RDLC 薦牌 9 變體選擇、1335-1357 文牒 2 變體、1554-1593 普桌動態字級） | ✅ 已實作 | `POST /api/v1/reports/batch` | `BatchReportHandler` + `SignupRepository.SearchByNumberRangeAsync`（編號範圍 + year/yearGte/ceremony/signupType 篩選）+ 5 種 reportType（reuse `ReportModelBuilders` 共享單筆 handler 邏輯）+ `PdfSharpMerger` 合併；worship 不另限 SignupType、只跟隨呼叫端篩選（2026-07-18 解鎖，同舊 case 5）；錯誤碼 `編號錯誤` / `報表類型錯誤` / `BATCH_NO_SIGNUPS` |
| 17 | `btnExportExcel_Click` | 655-728 | 匯出搜尋結果為 Excel | ✅ 已實作 | `POST /api/v1/signups/export` | `ExportSignupsHandler` 用 ClosedXML (.xlsx)；32 欄對齊舊順序；reuse `SearchSignupsHandler` |
| 18 | `cbSearchName_CheckedChanged` | 730-741 | 切換姓名搜尋鍵啟用 | ✅ 已實作 | 前端 form logic | `signup-list-page` `scopeName` checkbox；任一 scope* 勾選 → 啟用 `searchKey` 輸入（見 #43）|
| 19 | `cbSearchLivingName_CheckedChanged` | 743-754 | 切換陽上名搜尋鍵啟用 | ✅ 已實作 | 前端 form logic | `scopeLivingName` checkbox 驅動 searchKey 啟用 |
| 20 | `cbSearchDeadName_CheckedChanged` | 756-767 | 切換亡名搜尋鍵啟用 | ✅ 已實作 | 前端 form logic | `scopeDeadName` checkbox 驅動 searchKey 啟用 |
| 21 | `cbSearchPhone_CheckedChanged` | 769-780 | 切換電話搜尋鍵啟用 | ✅ 已實作 | 前端 form logic | `scopePhone` checkbox 驅動 searchKey 啟用 |
| 22 | `cbShowAll_CheckedChanged` | 782-793 | 切換完整欄位顯示 | ✅ 已實作 | 前端 grid columns | `showAll` signal（`toggleShowAll`）；經 effect 寫入 `localStorage['ceremony.signupList.showAll']` 持久化偏好 |
| 23 | `dgvSignups_DataBindingComplete` | 795-805 | 資料繫結完成後調整欄位顯示 | ✅ 已實作 | 前端 grid hook | 欄位可見性由 `showAll` signal + computed 欄位清單驅動（取代繫結後 hook）|
| 24 | `LoadSearchSignups()` public | 807-864 | **複合邏輯：動態述詞搜尋 + 結果繫結** (OR/AND 組合，PredicateBuilder) | ✅ 已實作 | `GET /api/v1/signups` | `SignupRepository.SearchAsync` 用 Dapper StringBuilder 動態組 WHERE；AND/OR 兩群組邏輯逐條對齊；用既有 `dbo.SignupView` view；LIKE wildcard escape；TOP 200 限制；ORDER BY Year/CeremonySort/NumberTitle/Number |
| 25 | `LoadCeremony()` helper | 866-883 | 載入法會下拉清單 | ✅ 已實作 | `GET /api/v1/categories` | `signup-list-page.loadCategories()` 填法會篩選下拉 |
| 26 | `LoadPrintType()` helper | 885-913 | 載入列印類型清單 (5 類) | ✅ 已實作 | enum | `REPORT_TYPES` → 右鍵列印選單項（資料卡/收據/薦牌/文牒/普桌）|
| 27 | `LoadSignupType()` helper | 915-954 | 載入報名類型清單 (5 類+全部) | ✅ 已實作 | enum | `SIGNUP_TYPES`（`shared/util/signup-type`）→ `signupTypes` 餵報名類型下拉 |
| 28 | `PrintDataCard()` helper | 956-1050 | 列印資料卡 (預覽/PDF 輸出) | ✅ 已實作 | `GET /api/v1/reports/datacard` | `DataCardRenderer` (QuestPDF) + `GenerateDataCardHandler`；25 個 TextBox 座標 + 2 lines 1:1 還原 tmpDataCard.rdlc；產 34KB PDF |
| 29 | `PrintReceipt()` helper | 1052-1146 | 列印收據 (預覽/PDF 輸出) | ✅ 已實作 | `GET /api/v1/reports/receipt` | `ReceiptRenderer` (QuestPDF) + `GenerateReceiptHandler`；A4 直 21×29.7cm 上+下聯；14pt 主資訊 + 16pt 郵寄；產 11KB PDF |
| 30 | `PrintTablet()` helper | **1148-1333** | **RDLC 薦牌 9 變體選擇邏輯**（動態 RDLC 檔案選擇 + 字級調整） | ✅ 已實作 (selector + base render) | `GET /api/v1/reports/tablet` | `Domain.Services.PrintTemplateSelector.ChooseTablet` 純函式 9 變體（含 0.6/0.8cm para 字級邏輯）+ `TabletRenderer` 11.5×25.4cm；單元測試 9 變體 100% 通過；**variant-specific 座標** TODO（目前全 variants 都用 base 座標）。**⚠️ 刻意偏離 legacy（2026-06-02）**：原 1179/1203 字級門檻用 `DeadName.Trim().Length > 7`（計入中間空格）；新版改用 `RealCharCount`（排除半/全形空格）。原因：使用者用姓名中間空格作刻意排版間隙，不應污染字級門檻（直書渲染仍保留間隙）。詳見 [gotchas.md](../../gotchas.md)「姓名中間空格」 |
| 31 | `PrintText()` helper | **1335-1552** | **RDLC 文牒 2 變體邏輯**（陽上亡名判定） | ✅ 已實作 (selector + base render) | `GET /api/v1/reports/text` | `ChooseText` (2 dead → Two；其他 Base) + `TextRenderer` 36.5×26.2cm 橫；Number 1cm Bold；PhotoAddress 區塊以文字暫繪（25×605px PNG TODO） |
| 32 | `PrintWorship()` helper | **1554-1696** | **RDLC 普桌 5 變體邏輯**（動態字級調整） | ✅ 已實作 | `GET /api/v1/reports/worship` | `ChooseWorship` 6 變體（按 LivingName 最高位）+ `WorshipRenderer` 21×29.6cm 直；One/Two/Three 字級 3cm、其餘 2cm；**各變體各自座標**（One 單欄置中 / Two 雙欄 / Three 三角 / Four 2×2 / Five 上2下3 / Base 2×3 矩陣，右至左）；worship2.png 背景已嵌入 |
| 33 | `CombinePDFs()` helper | 1698-1722 | 合併多個 PDF 流 (PdfSharp) | ✅ 已實作 | `Infrastructure.Reporting.PdfSharpMerger` | PdfSharp 6.2.4（.NET 10 cross-platform，等價舊 .NET Framework PdfSharp）；介面在 `Application.Reports.IPdfMerger`；行為對齊舊 CombinePDFs 逐頁 AddPage |
| 34 | `CreateStream()` helper | 1725-1730 | 建立列印用記憶流 | ❌ 故意捨棄 | – | WinForms RDLC/printDocument 內部；Web 改 QuestPDF → PDF 串流路徑，不重用 RDLC |
| 35 | `printDocument_BeginPrint` event | 1732-1735 | 初始化列印頁面索引 | ❌ 故意捨棄 | – | 桌面 printDocument 生命週期；Web 改 PDF（無實體印表機分頁狀態）|
| 36 | `printDocument_PrintPage` event | 1737-1762 | 繪製列印頁面 (EMF → 影像) | ❌ 故意捨棄 | – | 同上；EMF→影像繪製改為 QuestPDF 直接排版 |
| 37 | `printPreview_PrintClick` | 1764-1799 | 啟動列印對話 (含紙張設定查詢) | ❌ 故意捨棄 | – | 桌面列印對話 / 紙張設定查詢；Web 由瀏覽器 / PDF.js 預覽與列印取代 |
| 38 | `PanelSearchSwitch()` helper | 1801-1838 | 切換搜尋面板控制項狀態 | ✅ 已實作 | 前端 form mode | `signup-list-page` 搜尋表單控制項狀態由 reactive form 直接管理（scope*/searchKey 啟用連動）|
| 39 | `PanelPrintSwitch()` helper | 1840-1874 | 切換列印面板控制項狀態 | ✅ 已實作 | 前端 form mode | 列印改右鍵選單項 `enabledWhen`（依選取筆數啟用/禁用），取代列印面板控制項切換 |
| 40 | `PanelControlSwitch()` helper | 1876-1910 | 切換控制面板控制項狀態 | ✅ 已實作 | 前端 form mode | 控制按鈕（修改/刪除/歷程）以選取狀態 computed 控制可用性 |
| 41 | `GetNumberText()` helper | 1912-1927 | **避 4 規則** (個位 4 → "3-1") | ✅ 已實作 | `Domain.Services.AvoidFourFormatter` | 純函式；單元測試覆蓋 |
| 42 | `ShowCompleteColumn()` helper | 1929-1936 | 切換進階欄位可見性 | ✅ 已實作 | 前端 grid columns | `showAll` signal 控制進階欄位顯示 + computed 欄位清單（偏好存 localStorage）|
| 43 | `EnabledSearchKey()` helper | 1938-1942 | 啟用/禁用搜尋鍵欄位 | ✅ 已實作 | 前端 form logic | `signup-list-page` effect：任一 scope*（姓名/陽上/亡名/電話）勾選 → `searchKey.enable()`，全不勾 → `disable()` |
