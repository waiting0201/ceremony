---
title: BelieverForm Legacy Coverage
purpose: 反向稽核 — BelieverForm 所有方法/事件的新系統對應狀態
applicable_when: 完成 believers CRUD endpoint 後勾選；月度稽核；上線前 gate
legacy_form: BelieverForm.cs
legacy_path: reference/old/Ceremony/BelieverForm.cs
legacy_lines: 516
audit_status: complete
coverage_percentage: 100
last_audited: 2026-06-02
baseline_completed: 2026-05-27
total_methods: 17
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../api-endpoints/README.md
  - README.md
keywords: [legacy, coverage, believer]
last_updated: 2026-06-02
---

> ✅ **完成 (2026-06-02)**：17 個方法全部已實作。CRUD + 右鍵選單 + 城市/區域連動下拉 + 同寄件地址 + 表單模式切換全 ship（地址連動由 signup 表單 port 至 `believer-edit-form`）。

## 稽核總覽

| 項目 | 值 |
|---|---|
| 總方法 / 事件 / 邏輯區塊數 | 17 |
| ✅ 已實作 | 17 |
| ❌ 故意捨棄 | 0 |
| ⏳ 缺口待補 | 0 |
| 🤔 待確認 | 0 |
| 覆蓋率 | 100% |

## 方法 / 事件 / 邏輯區塊清單

| # | 舊方法/事件 | 舊 code line | 行為摘要 | 新系統狀態 | 對應 endpoint | 備註 |
|---|---|---|---|---|---|---|
| 1 | `BelieverForm()` constructor | 24-33 | 初始化表單與服務，禁用編輯模式 | ✅ 已實作 | – | `believer-edit-form` constructor：`loadCities()` + effect（believer 為 null → `reset()` 新增模式）|
| 2 | `btnSearch_Click` | 35-44 | 驗證搜尋條件 + 載入信眾列表 | ✅ 已實作 | `GET /api/v1/believers` | `SearchBelieversHandler` 含「請輸入搜尋條件」verbatim 訊息 |
| 3 | `btnNew_Click` | 46-55 | 新增模式，清空表單並載入城市與員工類型 | ✅ 已實作 | 前端 form mode + `POST /api/v1/believers` | API 已 ship；UI 行為由前端負責 |
| 4 | `dgvBelievers_CellClick` | 57-99 | 行選擇後載入該筆信眾資料至表單 | ✅ 已實作 | `GET /api/v1/believers/:id` | `GetBelieverHandler`；含 404 case |
| 5 | `btnConfirm_Click` | 101-191 | 驗證必填欄位 + 新增/修改信眾 + 刷新列表 | ✅ 已實作 | `POST /api/v1/believers` + `PUT /api/v1/believers/:id` | `CreateBelieverHandler` + `UpdateBelieverHandler` 共用 `BelieverWriteValidator`（trim, 全→半形 phone, -1 zipcode 正規化, 6 元素名單檢查） |
| 6 | `btnCancel_Click` | 193-200 | 清空選擇，清空表單，禁用編輯模式 | ✅ 已實作 | 前端 form reset | `believers-page` 關閉 overlay 清 editTarget；`believer-edit-form.reset()` 清全欄位 + 清區域清單 + `markAsPristine()` |
| 7 | `dgvBelievers_RowHeaderMouseClick` | 202-209 | 右鍵點擊顯示上下文菜單 | ✅ 已實作 | 前端 context menu | `believers-page` 用共用 `ContextMenuService`（右鍵 + 列尾 ⋮ kebab 兩入口）；選單含「編輯 / 刪除」（舊 cmsBelievers 僅「刪除」，編輯走 cell-click；新版合併，對齊 admins/signups pattern） |
| 8 | `tsmiDelete_Click` | 211-250 | 檢查報名無衝突 + 確認刪除並刷新列表 | ✅ 已實作 | `DELETE /api/v1/believers/:id` | `DeleteBelieverHandler`：`HasSignupsAsync` 衝突檢查回 409 + verbatim「{Name} 已有報名資料，不能刪除！」；硬刪除（非軟刪除，沿用舊行為） |
| 9 | `dlMailCity_SelectedIndexChanged` | 252-271 | 城市變更時載入對應區域選項 | ✅ 已實作 | `GET /api/v1/zipcodes/areas?city=X` | `believer-edit-form.onCityChange('mail')` → `applyAddress` 載入區域、清已選、`refreshZipcode` 更新郵遞區號（自 signup 表單 port）|
| 10 | `dlTextCity_SelectedIndexChanged` | 273-292 | 城市變更時載入對應區域選項 | ✅ 已實作 | 同上 | `onCityChange('text')` → `applyAddress`（同一連動邏輯）|
| 11 | `cbSameMailAddress_CheckedChanged` | 294-318 | 勾選同郵寄地址時複製，取消勾選時清空 | ✅ 已實作 | 前端 form logic | `onSameMailAddressChange()`：勾選複製寄件城市/區號/地址至文件，mail 地址為空時阻止勾選並提示「請先輸入寄件地址」；取消時清空文件地址（對齊舊 :294-318）|
| 12 | `txtPhone_Validating` | 320-351 | 驗證電話號碼格式（0 開頭數字） | ✅ 已實作 (部分) | `POST/PUT /api/v1/believers` | `BelieverWriteValidator.ToNarrow` 全→半形；regex `^0[0-9]*$` 留前端 validator（API 接受任何字串保彈性） |
| 13 | `LoadBelievers()` helper | 353-409 | 依搜尋條件查詢信眾 + 建立 ViewModel + 繫結至表格 | ✅ 已實作 | `GET /api/v1/believers` | `BelieverRepository.SearchAsync` 含動態 WHERE / 6 欄 OR (Living/Dead) / Zipcodes LEFT JOIN / EmployeeType 轉中文 / SQL LIKE 參數化 escape |
| 14 | `LoadCity()` helper | 411-426 | 載入所有城市至寄件與文件城市下拉選單 | ✅ 已實作 | `GET /api/v1/zipcodes/cities` | `believer-edit-form.loadCities()`（constructor 呼叫）→ `cities` signal 餵兩個城市下拉 |
| 15 | `LoadEmployeeType()` helper | 428-452 | 載入員工類型列表至下拉選單 | ✅ 已實作 | enum | `employeeType` select 內嵌 3 選項（1 非員工 / 2 大殿員工 / 3 地藏殿員工），對齊舊員工類型清單 |
| 16 | `PanelFormSwitch()` helper | 454-482 | 循環啟用/禁用表單內 TextBox、Button、CheckBox、ComboBox | ✅ 已實作 | 前端 form mode | `believers-page` overlay（editTarget 控制顯示）+ `believer-edit-form` create/edit 模式 |
| 17 | `PanelFormEmpty()` helper | 484-514 | 清空所有下拉選單、文本框與複選框 | ✅ 已實作 | 前端 form reset | `believer-edit-form.reset()` 清全欄位（含 6 陽上/6 亡名 FormArray）+ 區域清單 + 郵遞區號 |
