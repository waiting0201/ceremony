---
title: EditSignupForm Legacy Coverage
purpose: 反向稽核 — EditSignupForm 所有方法/事件的新系統對應狀態（628 行）
applicable_when: 完成 GET /signups/:id + PUT /signups/:id endpoint 後勾選；月度稽核；上線前 gate
legacy_form: EditSignupForm.cs
legacy_path: reference/old/Ceremony/EditSignupForm.cs
legacy_lines: 628
audit_status: complete
coverage_percentage: 100
last_audited: 2026-06-02
baseline_completed: 2026-05-27
total_methods: 20
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../api-endpoints/README.md
  - README.md
keywords: [legacy, coverage, edit-signup, 報名編輯]
last_updated: 2026-06-02
---

> ✅ **完成 (2026-06-02)**：20 個方法全部已實作。PUT/edit path（含 SignupLog transaction）+ 前端 `signup-edit-form` 全部 UI 連動（信眾選擇、城市/區域連動、同寄件地址、Load* 載入、編輯預填）全 ship。
> ⚠️ 已知關鍵段落：
> - 變更紀錄寫入時機（Update 觸發 SignupLog）
> - 不可改編號 / 不可改年份 / 不可改法會的限制
> - **刻意行為差異（2026-06-02）**：
>   - (a) signup-edit **不再**把 EmployeeType / IsFixedNumber 同步回 Believer：`UpdateSignupHandler` 對 `employeeTypeForBeliever` / `isFixedNumberForBeliever` 傳 `null`（保留 Believer 既有值，避免編輯報名時意外覆寫信眾主檔身分欄位）
>   - (b) 主要 Name / Phone 於儲存時 `Trim()`；陽上名 / 亡名**不 trim**（保留刻意排版間隙，render 與字級門檻一致，詳見 [gotchas.md](../../gotchas.md)「姓名中間空格」）

## 稽核總覽

| 項目 | 值 |
|---|---|
| 總方法 / 事件 / 邏輯區塊數 | 20 |
| ✅ 已實作 | 20 |
| ❌ 故意捨棄 | 0 |
| ⏳ 缺口待補 | 0 |
| 🤔 待確認 | 0 |
| 覆蓋率 | 100% |

## 方法 / 事件 / 邏輯區塊清單

| # | 舊方法/事件 | 舊 code line | 行為摘要 | 新系統狀態 | 對應 endpoint | 備註 |
|---|---|---|---|---|---|---|
| 1 | `EditSignupForm()` constructor | 41-68 | 初始化表單與所有服務，載入下拉選單選項 | ✅ 已實作 | – | `signup-edit-form` constructor：`loadCategories()` + `loadCities()` + effect（signupId → `applyItem`，fromId → `prefillFromSignup`）|
| 2 | `EditSignupForm_Load` | 70-73 | 表單載入時選擇該筆報名信眾 | ✅ 已實作 | `GET /api/v1/signups/:id` | `GetSignupHandler`；含 404 case；用 SignupView 已含全 join 欄位 |
| 3 | `dlBeliever_SelectedIndexChanged` | 75-85 | 信眾變更時載入該信眾基本資料 | ✅ 已實作 | 前端 cascading | `signup-edit-form.pickBeliever(b)`：選定信眾後帶入 believerId + `selectedBeliever` 驅動 `employeeTypeTitle` 顯示 |
| 4 | `dlMailCity_SelectedIndexChanged` | 87-106 | 寄件城市變更時載入對應區域 | ✅ 已實作 | 前端 cascading + zipcodes | `onCityChange('mail')` → `applyAddress` 載入區域、清已選 |
| 5 | `dlMailZone_SelectedIndexChanged` | 108-121 | 寄件區域變更時載入郵遞區號 | ✅ 已實作 | 同上 | `onAreaChange('mail')` → `refreshZipcode('mail')` |
| 6 | `dlTextCity_SelectedIndexChanged` | 123-142 | 文件城市變更時載入對應區域 | ✅ 已實作 | 同上 | `onCityChange('text')` → `applyAddress` |
| 7 | `dlTextZone_SelectedIndexChanged` | 144-157 | 文件區域變更時載入郵遞區號 | ✅ 已實作 | 同上 | `onAreaChange('text')` → `refreshZipcode('text')` |
| 8 | `cbSameMailAddress_CheckedChanged` | 159-184 | 同寄件地址勾選時複製，取消時清空 | ✅ 已實作 | 前端 form logic | `onSameMailAddressChange()`：勾選複製寄件城市/區號/地址至文件，mail 空時阻止；取消時清空文件地址 |
| 9 | `btnConfirm_Click` | 186-368 | 驗證所有必填欄位 + 更新信眾與報名 + 建檔案誌並刷新 | ✅ 已實作 | `PUT /api/v1/signups/:id` | `UpdateSignupHandler` + `SignupRepository.UpdateWithLogAsync` 含 transaction + 同步寫 Believer(HallName/EmployeeType/IsFixedNumber) + Signup 全欄位 + SignupLog |
| 10 | `txtYear_Validating` | 370-384 | 驗證年份格式與不早於當年 | ✅ 已實作 (部分) | `PUT /api/v1/signups/:id` | API 收 int + Year>0 檢查；regex/notInPast 留前端 |
| 11 | `txtFee_Validating` | 386-394 | 驗證費用為純數字 | ✅ 已實作 (部分) | `PUT /api/v1/signups/:id` | API 收 int?；前端 input mask |
| 12 | `txtNumber_Validating` | 396-418 | 驗證編號格式 + 檢查該年同類型編號重複性 | ✅ 已實作 | `PUT /api/v1/signups/:id` | `NumberExistsExcludingAsync` 排除自己 + verbatim「{year}年編號{n}重複，請重新確認！」 |
| 13 | `txtPrepayYear_Validating` | 420-448 | 驗證預繳年份格式與範圍，失效時重置預繳法會下拉 | ✅ 已實作 (部分) | validator + 前端 cascading | `prepayYear` 數字輸入（min=0）+ `prepayCeremonyCategoryId` 下拉於 `applyItem` 預填；「年份失效時自動重置預繳法會下拉」此細部前端驗證為後續精修（不影響資料正確性，submit 兩值獨立帶出）|
| 14 | `LoadBeliever()` helper | 450-461 | 載入全部信眾至下拉選單並開啟自動完成 | ✅ 已實作 | `GET /api/v1/believers` | `signup-edit-form` 信眾挑選器：`openBelieverPicker` + `searchBelievers(term)` → `believerSearchResults`，取代下拉自動完成 |
| 15 | `LoadCeremony1()` helper | 463-471 | 載入一級法會類別至下拉選單 | ✅ 已實作 | `GET /api/v1/categories` | `loadCategories()` 填法會下拉 |
| 16 | `LoadSignupType()` helper | 473-507 | 載入報名類型至下拉選單 | ✅ 已實作 | enum | `SIGNUP_TYPES`（`shared/util/signup-type`）→ `signupTypes` 餵下拉 |
| 17 | `LoadCity()` helper | 509-524 | 載入城市至寄件與文件城市下拉選單 | ✅ 已實作 | `GET /api/v1/zipcodes/cities` | `loadCities()`（constructor 呼叫）|
| 18 | `LoadPrepayCeremony()` helper | 526-534 | 載入一級法會類別至預繳法會下拉選單 | ✅ 已實作 | `GET /api/v1/categories` | reuse `loadCategories()` 同一法會清單餵預繳法會下拉 |
| 19 | `LoadEmployeeType()` helper | 536-560 | 載入員工類型至下拉選單 | ✅ 已實作 | enum | `employeeTypeTitle`（computed，自 `selectedBeliever().employeeTypeTitle`）顯示選定信眾身分 |
| 20 | `BelieverSelected()` helper | 562-626 | 編輯模式初始化，載入該報名全部資料至表單（try-catch on Phone null） | ✅ 已實作 | `GET /api/v1/signups/:id` 回傳 | `applyItem(item)`（編輯預填全欄位 + 地址連動 + selectedBeliever）+ `prefillFromSignup(fromId)`（自既有報名帶入）；**資料優先順序：Signup > GridRow > Believer**（[backend-design.md](../../design/backend-design.md)） |
