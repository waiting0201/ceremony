---
title: NewSignupForm Legacy Coverage
purpose: 反向稽核 — NewSignupForm 所有方法/事件的新系統對應狀態（1118 行）
applicable_when: 完成 POST /signups + 編號分配 endpoint 後勾選；月度稽核；上線前 gate
legacy_form: NewSignupForm.cs
legacy_path: reference/old/Ceremony/NewSignupForm.cs
legacy_lines: 1118
audit_status: complete
coverage_percentage: 100
last_audited: 2026-06-02
baseline_completed: 2026-05-27
total_methods: 34
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../api-endpoints/README.md
  - ../signup-management.md
  - README.md
keywords: [legacy, coverage, new-signup, 報名建立]
last_updated: 2026-07-17 (信眾搜尋改常駐 in-form 列表對齊舊 dgvBelievers；未選信眾自動建信眾由前端 orchestration 補齊——rows 3/4/6/24 與頂部註記更新)
---

> ✅ **完成 (2026-06-02 交叉稽核)**：核心 POST /signups + 表單編排對齊舊版 + 地址 city/area 連動下拉皆 shipped；剩餘 WinForms 列印內部事件（PrintDocument/EMF/列印對話）統一 ❌ 故意捨棄（改 server-side QuestPDF→PDF + 瀏覽器預覽，與 SignupForm rows 34-37 一致，不受列印 PoC 影響）。
> ⚠️ 已知關鍵段落：
> - 表單驗證 + 編號分配 ✅ 已實作（UPDLOCK + HOLDLOCK 取代舊系統 race window）
> - 避 4 規則 ✅ 已實作（`Domain.Services.AvoidFourFormatter` 純函式）
> - 兩步驟流程（基本資料 → 名單）對應**新版單頁表單**（mockup v4 已決議）；2026-05-29 單頁**欄位編排對齊舊版**（法會→信眾→基本→地址→名單→編號/費用→備註/預繳）
> - 地址 city→area 連動下拉 ✅（新增 `GET /zipcodes/cities` + `GET /zipcodes?city=`）+ 同寄件地址 checkbox
> - inline 新建 Believer ✅ 已實作（2026-07-17 補齊）——API 層維持不做（`CreateSignupRequest.BelieverId` 必填），由前端 orchestration：未選信眾送出時 `submit()` 先 `POST /believers`（employeeType=1/isFixedNumber=false 同舊預設）再 `POST /signups`。此前前端漏做（believerId 掛 required），「沒選信眾就無法新增」與舊 `btnConfirm_Click:186-223` 自動建信眾行為不符——已修。inline **編輯**既有信眾屬性仍 ❌ 故意捨棄（於信眾維護調整）；員工類型 + 固定編號於報名表單唯讀顯示
> - 選信眾自動帶入「預繳歷史」+ 固定編號顯示已補（2026-06-02，`BelieverListItem.IsFixedNumber` + `GET /prepay?believerId`）
> - **重複報名警示 ➕ 新版增強（2026-06-30，無對應 legacy 行）**：舊 NewSignupForm 不檢查信眾重複報名；新版選信眾後即時查 `(Year, CeremonyCategoryID)` 同信眾既有報名（忽略 SignupType）跳警示但不阻擋。見 [get-signup-duplicates.md](../api-endpoints/get-signup-duplicates.md)。不影響本表覆蓋率（非 legacy 方法）。
> - **插入並順移 ➕ 新版增強（2026-07-04，無對應 legacy 行）**：舊系統只能自動 `MAX+1` 或手動指定空號（指定已佔用號被擋）；新版於報名維護列表右鍵「在此前插入」，可插入到已佔用編號位並讓其後 `Number ≥ N` 的既有報名 +1 順移（主要用於預繳載入後補插）。見 [post-signups-insert-shift.md](../api-endpoints/post-signups-insert-shift.md)。不影響本表覆蓋率（非 legacy 方法）。

## 稽核總覽

| 項目 | 值 |
|---|---|
| 總方法 / 事件 / 邏輯區塊數 | 34 |
| ✅ 已實作 | 28 |
| ❌ 故意捨棄 | 6 (btnNextStep 單頁表單取代；+ 5 WinForms 列印內部事件改 server-side PDF) |
| ⏳ 缺口待補 | 0 |
| 🤔 待確認 | 0 |
| 覆蓋率 | 100% |

## 方法 / 事件 / 邏輯區塊清單

| # | 舊方法/事件 | 舊 code line | 行為摘要 | 新系統狀態 | 對應 endpoint | 備註 |
|---|---|---|---|---|---|---|
| 1 | `NewSignupForm()` constructor | 58-83 | 初始化表單、服務、設定面板 | ✅ 已實作 | 前端 form init | `signup-edit-form.component` constructor：`loadCategories()` + `loadCities()` + create/edit mode effect |
| 2 | `btnNextStep_Click` | 85-112 | 下一步：載入城市、員工類型，代入邏輯 | ❌ 故意捨棄 | – | 新版單頁表單（mockup v4），無「下一步」流程 |
| 3 | `btnBelieverSearch_Click` | 114-124 | 驗證搜尋條件後查詢信眾 | ✅ 已實作 | `GET /api/v1/signups`（`searchKey`+4 scope flags） | signup-edit-form 信眾**常駐 in-form 搜尋**（2026-07-17 改：搜尋列+結果列表直接常駐於表單頂部，對齊舊 txtQ+dgvBelievers 常駐面板型態，取代 modal picker；最多 render 前 200 列+總數提示防 DOM 卡頓）。搜尋語意沿用 2026-07-02：單一輸入框 OR 比對 Name/Phone/6組陽上/6組往生共 14 欄，按鈕/Enter 觸發 |
| 4 | `dgvBelievers_CellClick` | 126-137 | 選擇信眾行並加載其資料 | ✅ 已實作 | 前端 row select + `GET /api/v1/believers/{id}` | `pickBeliever` 選定後（2026-07-02：改用該列 believerId 查 /believers/{id} 取完整明細）預填表單（基本資料 + 地址 city/area + 陽上/往生名單）；2026-07-17：選定後**列表保留**、選定列高亮，可隨時再點別筆改選覆蓋（同舊 CellClick 重跑 BelieverSelected） |
| 5 | `cbKeepNumber_CheckedChanged` | 139-149 | 切換編號手動輸入啟用狀態 | ✅ 已實作 | 前端 form logic | `keepNumber` checkbox 控制「編號」欄顯示/送出 |
| 6 | `btnConfirm_Click` | 151-362 | **複合邏輯：表單驗證 + 編號分配 + 新增報名**（211 行核心方法；186-223 未選信眾時自動 INSERT Believers） | ✅ 已實作 | `POST /api/v1/signups`（未選信眾時前端先 `POST /believers`） | `CreateSignupHandler` + `SignupRepository.InsertWithLogAsync` 含 UPDLOCK + HOLDLOCK + transaction + 同步寫 SignupLog；行為改善（舊系統無 lock，有 race window）。自動建信眾分支＝前端 orchestration（2026-07-17 補齊，見頂部註記） |
| 7 | `btnCancel_Click` | 364-369 | 返回第一步並清空表單 | ✅ 已實作 | 前端 form reset | overlay 關閉 / `form.reset` + dirty 確認（form-overlay） |
| 8 | `btnPrintDataCard_Click` | 371-404 | 列印剛新增報名的資料卡 | ✅ 已實作 | `GET /api/v1/reports/datacard` | endpoint 已 shipped；新增後列印走報名維護右鍵列印 / 列印預覽頁（不在建立流程內 auto-print） |
| 9 | `dlMailCity_SelectedIndexChanged` | 406-424 | 更新郵寄區域下拉清單 | ✅ 已實作 | `GET /api/v1/zipcodes?city=` | `onCityChange('mail')` 載入該城市區域（區域 option value=ZipcodeID） |
| 10 | `dlMailZone_SelectedIndexChanged` | 426-439 | 填入郵寄郵遞區號 | ✅ 已實作 | 前端 cascading | `onAreaChange('mail')` 顯示選定區域的郵遞區號（read-only） |
| 11 | `dlTextCity_SelectedIndexChanged` | 441-460 | 更新簽署區域下拉清單 | ✅ 已實作 | `GET /api/v1/zipcodes?city=` | `onCityChange('text')` |
| 12 | `dlTextZone_SelectedIndexChanged` | 462-475 | 填入簽署郵遞區號 | ✅ 已實作 | 前端 cascading | `onAreaChange('text')` |
| 13 | `cbSameMailAddress_CheckedChanged` | 477-502 | 複製郵寄地址到簽署地址或清空 | ✅ 已實作 | 前端 form logic | `onSameMailAddressChange`：勾選複製 mail→text；mail 空 → verbatim「請先輸入寄件地址」並取消勾選；取消勾選清空文牒 |
| 14 | `txtYear_Validating` | 504-518 | 驗證年份格式及範圍 (須 ≥ 當年) | ✅ 已實作 (部分) | `POST /api/v1/signups` | API 收 int；regex/notInPast guard 留前端 |
| 15 | `txtPhone_Validating` | 520-551 | 驗證電話格式 (0 開頭) | ✅ 已實作 (部分) | `POST /api/v1/signups` | 全→半形轉換在 handler；regex 留前端 |
| 16 | `txtNumber_Validating` | 553-574 | 驗證編號格式及重複性 | ✅ 已實作 | `POST /api/v1/signups` | `NumberExistsAsync` 重複檢查 + verbatim「編號重複，請重新確認！」訊息 |
| 17 | `txtFee_Validating` | 576-584 | 驗證費用格式 (數字) | ✅ 已實作 (部分) | `POST /api/v1/signups` | API 收 int?；前端 input mask |
| 18 | `txtPrepayYear_Validating` | 586-614 | 驗證預繳年份格式並載入法會 | ✅ 已實作 (部分) | validator + cascading | 同 rows 14/15/17：預繳年份/法會欄存在；regex/notInPast guard 留前端 |
| 19 | `LoadCeremony1()` helper | 616-624 | 載入主法會下拉清單 | ✅ 已實作 | `GET /api/v1/categories` | 法會分類下拉（`flatCategories`） |
| 20 | `LoadSignupType()` helper | 626-660 | 載入報名類型清單 (5 類) | ✅ 已實作 | enum（前端 `SIGNUP_TYPES`） | 報名類型下拉 |
| 21 | `LoadCity()` helper | 662-677 | 載入城市下拉清單 | ✅ 已實作 | `GET /api/v1/zipcodes/cities` | `ZipcodeRepository.GetCitiesAsync`（GROUP BY City ORDER BY City，對齊舊；未過濾 IsDisplay） |
| 22 | `LoadEmployeeType()` helper | 679-703 | 載入員工類型清單 (3 類) | ✅ 已實作 | `GET /api/v1/believers` | 員工類型 + **固定編號**於 signup 表單**唯讀顯示**（`employeeTypeTitle` / `isFixedNumber`）；inline 編輯信眾屬性故意捨棄（新流程不於報名改信眾主檔，於信眾維護調整）。`BelieverListItem` 已補 `IsFixedNumber`（2026-06-02）|
| 23 | `LoadPrepayCeremony()` helper | 705-713 | 載入預繳法會下拉清單 | ✅ 已實作 | `GET /api/v1/categories` | 預繳法會下拉（共用 `flatCategories`） |
| 24 | `LoadBelievers()` helper | 715-734 | 查詢並顯示信眾清單 | ✅ 已實作 | `GET /api/v1/signups` | 常駐 in-form 結果清單（2026-07-17 改：由 modal picker 移回表單頂部常駐顯示，同舊 dgvBelievers；16 欄表格、每報名一列、1:1 對齊舊可見欄位；最多 render 前 200 列+總數提示） |
| 25 | `GetNumberText()` helper | 736-751 | **避 4 規則** (個位 4 → "3-1") | ✅ 已實作 | `Domain.Services.AvoidFourFormatter` | 純函式，11 個 case xUnit 覆蓋；display only，DB 仍存 int |
| 26 | `PanelFormSwitch()` helper | 753-793 | 切換表單面板控制項狀態 | ✅ 已實作 | 前端 form mode | Angular form mode / overlay state（create vs edit） |
| 27 | `PanelFilterSwitch()` helper | 795-817 | 切換篩選面板控制項狀態 | ✅ 已實作 | 前端 form mode | 同上（單頁表單無獨立篩選面板，狀態由 form mode 控制） |
| 28 | `PanelFormEmpty()` helper | 819-859 | 清空所有表單欄位 | ✅ 已實作 | 前端 form reset | `reset()`（create 模式初始化） |
| 29 | `PrintDataCard()` helper | 861-911 | 列印資料卡 (RDLC 渲染) | ❌ 故意捨棄 | – | server-side QuestPDF→PDF 取代 WinForms RDLC 渲染（與 SignupForm row 35 一致）；資料卡輸出邏輯在 `GenerateDataCardHandler` |
| 30 | `CreateStream()` helper | 914-919 | 建立列印用記憶流 | ❌ 故意捨棄 | – | WinForms 列印 stream 內部；web/PDF path 不需 |
| 31 | `BeginPrint()` event | 921-924 | 初始化列印頁面索引 | ❌ 故意捨棄 | – | WinForms PrintDocument 事件；web/PDF path 不需 |
| 32 | `PrintPage()` event | 927-952 | 繪製列印頁面 (EMF → 影像) | ❌ 故意捨棄 | – | WinForms EMF 繪製；改 server-side PDF |
| 33 | `printPreview_PrintClick` | 954-989 | 啟動列印對話 (含紙張設定) | ❌ 故意捨棄 | – | WinForms 列印對話；改瀏覽器 PDF 預覽（reports preview 頁） |
| 34 | `BelieverSelected()` helper | 991-1116 | **複合邏輯：填入信眾資料 + 地址 + 預繳歷史** (125 行) | ✅ 已實作 | `GET /api/v1/believers`（picker）+ `GET /api/v1/prepay?believerId` | `pickBeliever` 預填姓名/電話/堂號/固定編號/寄件+文牒地址（city/area cascade）/陽上+往生名單；**預繳歷史自動預填**（舊 1102-1115）由新 `GET /prepay?believerId&year` 反查最新報名預繳年/法會，`prepayYear` 非 null 才帶入（2026-06-02 補；blueprint：[get-prepay-believer-latest.md](../api-endpoints/get-prepay-believer-latest.md)）|
