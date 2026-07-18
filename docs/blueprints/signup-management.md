---
title: 報名管理（搜尋、新增、編輯、歷程）
purpose: 報名核心業務 — 對應舊 SignupForm / NewSignupForm / EditSignupForm / SignupLogForm
status: draft
applicable_when: 要修改報名欄位、調整搜尋邏輯、調整編號生成、修改變更歷程
related_agents:
  - software-architect-blueprint
  - backend-engineer
  - frontend-architect
related_docs:
  - ../design/database-design.md
  - ../design/api-design.md
  - ../design/backend-design.md
  - ../design/visual-design.md
  - ../design/performance.md
  - prepay-loading.md
  - printing-reports.md
keywords: [signup, 報名, 報名維護, 編號, NumberTitle, 避4, PredicateBuilder, SignupForm, context-menu, 右鍵, 多選, 批次列印, 勾選列印, signupIds]
last_updated: 2026-07-18 (右鍵「列印普桌／普桌資料卡」解鎖：前端不再檢查選取列型別、恆啟用，防呆交後端過濾/驗證；先前 2026-07-17 新增報名表單對齊舊系統四項：信眾搜尋改常駐 in-form 結果列表、地址寄件上/文牒下、名單往生上/陽上下且無底色、未選信眾自動先建新信眾（前端 POST /believers orchestration）)
---

## 背景與動機

報名是系統核心，舊 SignupForm 超過 1900 行；含 40 欄 grid、AND/OR PredicateBuilder 搜尋、多選列印、批次範圍列印、Excel 匯出、新增/編輯/歷程子頁面。新版必須完整重現業務邏輯與介面編排，並修正三段 SaveChanges 非原子、編號 race condition 等問題。

## 範圍

### 做什麼
- 報名搜尋（年份 / 法會 / 類型 / 編號 / 關鍵字 + 5 範圍 checkbox〔姓名/陽上/往生/電話/備註〕+ 固定編號）
- 報名 CRUD：新增（兩步驟）/ 編輯 / 刪除（多選） / 歷程
- 編號生成（GetSignupNumber）+ 避 4 顯示（GetNumberText）
- NumberTitle 由 SignupType 自動推導（不可手動）
- 信眾資料帶入優先順序（Signup → grid row → Believer）
- 預繳資料自動帶入（「今年以前最新報名」）
- 地址 fallback（疏文 ← 寄件）
- Excel 匯出（32 欄）
- 批次列印（編號範圍 + 5 種報表）

### 不做什麼
- 報名線上付款（業務未要求）
- 報名通知信 / 簡訊（未來）
- 信眾自助報名（內部使用）

## 使用者流程

### 搜尋（SignupForm 對應）

```
1. 進入 /signups
2. 篩選區：年份 / 範圍勾選 / 法會 / 類型 / 編號 / 關鍵字
   勾選任一搜尋欄位（姓名/陽上/往生/電話/備註）才能填關鍵字
3. 「搜尋」→ 後端套 PredicateBuilder AND/OR
4. DataGrid 顯示 40 欄（預設 32 顯示）
5. 「顯示完整表格」勾選顯示額外 5 欄
6. 列首 checkbox 多選；shift-click 範圍選取；Ctrl/Cmd-click 加入或移除
7. 右鍵（或長按 / 列尾「…」按鈕）開 context menu — **9 項，依選取狀態與 SignupType filter 啟用 / 停用**
8. 上方批次列印面板：起~迄編號 + reportType → 「列印」（呼叫 POST /reports/batch）
9. 「匯出Excel」→ 下載 yyyyMMddHHmmss.xlsx
```

#### Grid Context Menu（cmsSignups 等價，**新版重現**）

舊 [SignupForm.Designer.cs:236-313](../../reference/old/Ceremony/SignupForm.Designer.cs#L236-L313) 9 個 `ToolStripMenuItem`，由 [SignupForm.cs:121-149](../../reference/old/Ceremony/SignupForm.cs#L121-L149) `dgvSignups_RowHeaderMouseClick` 觸發（`MouseButtons.Right` + 選中該列）。

| # | 舊 label | 對應 handler / endpoint | 選列規則 | SignupType 規則 | 備註 |
|---|---|---|---|---|---|
| 1 | 代入新增 | navigate `/signups/new?fromSignupId=:id`（前端帶 query 預填信眾） | **單選**（==1 才 enable） | 全部 | 對應 `tsmiAdd_Click` (cs:151)：把選列的 SignupID + Name 帶入 NewSignupForm |
| 1b | **在此前插入**（新版增強，無舊對應） | overlay signup-edit-form 插入模式 → `POST /api/v1/signups/insert-shift` | 觸發列（右鍵那列）有編號才 enable | 全部 | 於觸發列編號位置插入一筆新報名，該群組內 Number ≥ 此編號的既有報名 +1 順移。主要用於**預繳載入後補插**。見下方「插入並順移」段 |
| 2 | 修改資料 | navigate `/signups/:id/edit` | **單選** | 全部 | 對應 `tsmiEdit_Click` (cs:168)：未選列 verbatim「尚未選擇報名資料」 |
| 3 | 列印資料卡 | `GET /api/v1/reports/datacard?signupId=` ×N（或前端拼批次） | 單 / 多選皆可 | 全部 | 對應 `tsmiPrintDataCard_Click` (cs:188) |
| 4 | 列印收據 | `GET /api/v1/reports/receipt?signupId=` | 單 / 多選 | 全部 | `tsmiPrintReceipt_Click` (cs:242) |
| 5 | 列印薦牌 | `GET /api/v1/reports/tablet?signupId=` | 單 / 多選 | 全部 | `tsmiPrintTablet_Click` (cs:273)；含 HallName 拆字邏輯（前端不重作，後端已處理） |
| 6 | 列印文牒 | `GET /api/v1/reports/text?signupId=` | 單 / 多選 | 全部 | `tsmiPrintText_Click` (cs:323) |
| 7 | 列印普桌 / 普桌資料卡 | `GET /api/v1/reports/worship?signupId=` | 單 / 多選 | **完全不鎖型別**（2026-07-18 客訴改）：前端恆啟用、後端選什麼印什麼 | `tsmiPrintWorship_Click` (cs:380) 本就無型別檢查；原新系統「限 type-4」自加防呆（422 `WORSHIP_ONLY_TYPE_4`／批次過濾）已全數撤回 |
| 8 | 刪除資料 | `DELETE /api/v1/signups/:id` ×N | 單 / 多選 | 全部 | `tsmiDelete_Click` (cs:405)；多選逐筆呼叫；需二次確認 dialog |
| 9 | 瀏覽歷程 | navigate `/signups/:id/logs` | **單選** | 全部 | `tsmiLog_Click` (cs:428) |

**選列邏輯**：
- 未選 (count == 0)：除 3–7（多選列印）走「印出當前篩選結果」可選擇 disable 全部以維持舊行為；採後者，**未選列右鍵不開選單**或開但全部 disable（取後者，使用者察覺自己沒選到）
- 單選 (count == 1)：全部 enable（含 1, 2, 9）；列印走單筆 endpoint
- 多選 (count > 1)：1, 2, 9 disable；3–7 enable（多筆呼叫 batch endpoint 或前端逐筆）；8 enable

**普桌／普桌資料卡啟用條件（2026-07-18 改：完全解鎖）**：
- 與其他列印選項完全一致：**有選取列即 enable、選什麼印什麼**，前後端都不檢查 `signupType`（客訴：右鍵選項常被鎖、單選非普桌 422）
- 考據：舊系統 `tsmiPrintWorship_Click` 本就無型別檢查；原「限 type-4」是新系統自加的嚴格化，已全數撤回（詳見 [business-rules-implicit.md §16](../business-rules-implicit.md)）

**多筆列印實作策略（2026-07-03 更新，取代原 v1 限制）**：
- 選 1 筆 → 呼叫單筆 endpoint（`GET /reports/{type}?signupId=`）
- 選 > 1 筆 → 呼叫 `POST /api/v1/reports/batch` 帶 `signupIds: [勾選的 id...]`（見 [post-reports-batch.md](api-endpoints/post-reports-batch.md)），後端依 `SignupID IN (...)` 精準只印勾選的那幾筆，**不論編號是否連續**，不再需要湊區間或多印非選取列
- **已撤回**：原 v1「不連續時退化成 `numberStart=min, numberEnd=max` 編號區間 + 跳確認對話框告知會多印非選取筆數」的近似做法——已被上述精準模式取代，前端 `signup-list-page.ts` 的 `actionPrint` 不再跳該確認框
- 觸發方式：右鍵 menu / 列尾 kebab menu / 鍵盤 `Menu` 鍵 / 長按（touch）

**插入並順移（新版增強，2026-07-04）**：
- 需求：預繳載入後常需在既有連號序列中間補插一筆（漏報/臨時加報），並讓插入點其後的既有報名編號自動 +1 順移。舊系統無此能力（只能自動 MAX+1 或指定空號）。
- 入口：報名維護列表**右鍵某列 →「在此前插入」**（`signup-list-page.ts` `actionInsertBefore`，icon `insert-above`）。開 `signup-edit-form` 插入模式：帶入該列的年/法會/類型（**鎖定唯讀**）+ `keepNumber=true` + `customNumber=該列編號`（預填、可改）；信眾/名單/地址留空給使用者填。overlay title「插入報名（後續順移）」。
- 後端：`POST /api/v1/signups/insert-shift`（`InsertShiftSignupHandler` → `SignupRepository.InsertWithShiftAsync`）。單一交易內 `sp_getapplock`（`signup-number:` resource，**與預繳載入共用**）→ `UPDATE Signups SET Number=Number+1 WHERE ... AND Number >= @N`（set-based，`(Year,Cat,Type,Number)` 無 unique index 故無中間衝突）→ 插入新筆 + SignupLog。**刻意不做編號重複檢查**（插入位置本就佔用）。順移的既有列只 UPDATE Number、不 append SignupLog。詳見 [post-signups-insert-shift.md](api-endpoints/post-signups-insert-shift.md)。
- 範圍：僅 create 情境；編輯改編號仍走 `PUT /signups`（`SIGNUP_NUMBER_CONFLICT` 擋重複、不順移）。

#### 批次列印面板（btnPrint_Click 等價）

舊 [SignupForm.cs:447](../../reference/old/Ceremony/SignupForm.cs#L447) `btnPrint_Click` 從上方 nudStart / nudEnd / dlSearchSignupType 取編號區間 + 類型，**獨立於 grid 選取**。

新版獨立 panel（在 filter 區右側）：

```
┌─ 批次列印 ────────────────────┐
│ 起編號 [   ] ~ 迄編號 [   ]    │
│ 報表類型 [資料卡 ▼]            │
│ [列印批次]                     │
└────────────────────────────────┘
```

- 帶入年份 / 法會 / 類型 / yearGte 等其他 filter（沿用搜尋區當前值，避免使用者重設）
- 呼叫 `POST /api/v1/reports/batch` body `{ reportType, numberStart, numberEnd, year?, ceremonyCategoryId?, signupType? }`
- 回 PDF blob → 開新分頁 / iframe 預覽（同 reports preview page 機制）
- 普桌（worship）強制 SignupType=4；其他 reportType 跟隨當前 signupType filter

### 新增（NewSignupForm → 新版單頁，2026-05-29 欄位編排對齊舊版）

**結構決策**：舊 NewSignupForm 為兩步驟（先選年份/法會/類型 → 再選信眾並填詳細）；新版維持 mockup v4 的**單頁表單**（不重做兩步驟，無「下一步」），但**欄位編排對齊舊 NewSignupForm.cs**，單頁由上到下：

```
法會資料   民國年(預設 TaiwanCalendar.GetYear) / 法會分類 / 報名類型（2026-07-17 使用者指定提到表單最上方）
信眾       常駐搜尋列 + 結果列表直接顯示（2026-07-17 改，對齊舊常駐 dgvBelievers；
           選定後列表保留、可隨時點別筆改選覆蓋欄位；未選信眾也可送出 → 自動建新信眾）
基本資料   員工類型(唯讀) / 堂號(唯讀) / 姓名 / 聯絡電話
地址       寄件在上：寄件城市→區域(連動下拉)→郵遞區號(唯讀) / 寄件地址
           文牒在下：文牒城市→區域→郵遞區號 / 文牒地址 + ☑ 同寄件地址（同列；
           複製 mail→text；mail 空 → 「請先輸入寄件地址」）
           （2026-07-17 改回上下堆疊，對齊舊 Designer 寄件 Y≈222 / 文牒 Y≈311）
名單       往生 ×6 在上、陽上 ×6 在下（2026-07-17 對齊舊 Designer 往生 Y≈401 / 陽上 Y≈517）；
           往生輸入框不加底色（舊系統兩組皆無 BackColor，使用者指定）
編號/費用  ☑ 指定編號 + 編號 / 費用
備註/預繳  備註 / 預繳民國年 / 預繳法會
```

- **法會分類依當月自動帶季別（新版加值，2026-06-23）**：新增模式下載完分類樹後，依當前月份自動把「法會分類」預設為對應季別 root（1-4月→春季 / 5-8月→中元 / 9-12月→秋季，見 [business-rules-implicit.md](../business-rules-implicit.md) §月→季）。為**可編輯的預設**：使用者仍可改選任何季別或子法會（子法會仍人工挑選，月份只決定季別）。僅在 create 模式且使用者尚未選值時帶入；編輯模式不覆蓋既有 ceremony。實作：`util/ceremony-season.ts`（`currentSeason` / `resolveSeasonRootId`，GUID 優先、title 退場）+ `signup-edit-form` `applySeasonDefault()`
- 城市/區域連動下拉資料源：`GET /zipcodes/cities`、`GET /zipcodes?city=`（見 [get-zipcodes.md](api-endpoints/get-zipcodes.md)）；對齊舊 `LoadCity` / `dlMailCity_SelectedIndexChanged`
- **員工類型 + 固定編號唯讀顯示**：新流程不於報名建立時**編輯**既有信眾屬性（inline 編輯 Believer 捨棄，於信眾維護調整）。`BelieverListItem` 已含 `IsFixedNumber`（2026-06-02），報名表單唯讀顯示「固定編號 是/否」
- **未選信眾 → 自動建立新信眾（2026-07-17 補齊，對齊舊 `btnConfirm_Click:186-223`）**：舊系統 `dgvBelievers.SelectedRows.Count == 0` 時當場 `Guid.NewGuid()` INSERT Believers 再建報名；新版 API 層維持不做 inline 建立（`CreateSignupRequest.BelieverId` 必填），由**前端 orchestration**：`submit()` 發現 create 模式且無 believerId → 先 `POST /believers`（employeeType=1 非員工、isFixedNumber=false，同舊表單下拉/checkbox 預設；姓名/電話/兩組地址/陽上/往生取自表單）→ 拿到 id 綁回表單再 `POST /signups`。信眾建立成功但報名失敗時 believerId 已綁回表單，重送不會重複建信眾。此前前端漏做這條路（believerId 掛 required），導致「沒選信眾就完全無法新增」——已修
- **選信眾自動帶入預繳歷史**：`pickBeliever` 呼叫 `GET /prepay?believerId&year`，最新報名有預繳則帶入預繳年/法會（對齊舊 `BelieverSelected:1102-1115`；見 [get-prepay-believer-latest.md](api-endpoints/get-prepay-believer-latest.md)）
- **信眾搜尋 1:1 對齊舊 `dgvBelievers`（2026-07-02 決策，取代先前簡化卡片式設計；2026-07-17 由 modal picker 改為常駐 in-form 列表，完全回到舊系統型態）**：
  - **常駐列表（2026-07-17）**：搜尋框/搜尋鈕/結果表格直接放在表單頂部「信眾」fieldset（全寬），非彈窗——對齊舊 `plStep2` 上常駐的 `txtQ + dgvBelievers`。點列選定後**列表保留**、可隨時再點別筆（每次改選重新覆蓋整份表單欄位，同舊 `dgvBelievers_CellClick`）；選定列高亮 + 「已選信眾」摘要（**僅選定後顯示**；未選時不顯示任何提示文字，「符合 N 筆僅顯示前 200」截斷提示也不顯示——2026-07-17 使用者指定拿掉，截斷本身保留）。結果表格 `max-height: 140px` 內部捲動（舊 dgv 高 117px 同精神），**靜默截斷最多 render 前 200 列**（模糊字如「陳」可命中 2 萬+ 列，全塞 DOM 會卡死；舊 WinForms grid 有虛擬化沒此問題）。**無 row hover 變色**（對齊 vgrid，見配色規範）。**配色/列高對齊報名維護 grid（2026-07-17 使用者指定）**：走全站唯一權威 [visual-design.md「清單/資料格配色規範」](../design/visual-design.md)——`.data-table.dense` 已補直向格線/表頭底線/往生欄右框線與 vgrid 一一對應（Playwright computed-style 9 項比對全同值），cell padding 收為 2px 6px（列高 25px ≈ 報名維護 26px）。編輯模式不顯示搜尋區（不換信眾），僅顯示信眾摘要卡
  - **搜尋**：單一輸入框 + 「搜尋」按鈕觸發（**2026-07-02 改**：原本 `(input)` 即時查詢，即使加 debounce 仍是「打字就打 API」；改回對齊舊 `btnBelieverSearch_Click` 的按鈕觸發語意——文字先落地在框內，按鈕或 Enter 才真正查詢），OR 比對 Name/Phone/6組陽上/6組往生共 14 欄（對齊舊 `NewSignupForm.cs:715-722` `txtQ`/`LoadBelievers`）。**不新增 endpoint**——沿用既有 `GET /api/v1/signups`（`SignupApi.search`）帶 `searchKey` + `scopeName/scopePhone/scopeLivingName/scopeDeadName=true`（`scopeRemark` 不開，舊系統不搜備註），該端點語意與舊 14 欄 OR 搜尋完全對應
  - **清單粒度/欄位**：每筆「報名紀錄」一列（非每信眾一列，同信眾過去報過多次會重複出現多列），16 欄：堂號/姓名/聯絡電話/編號標題/編號/年份/法會/往生1~6/陽上1~6（欄位順序對齊 `NewSignupForm.Designer.cs:1017-1355`）；資料直接來自 `SignupListItem`（`/signups` 既有回應，本已含全部所需欄位，未新增後端 DTO）
  - **選定回填**：點列後用該列 `believerId` 呼叫既有 `GET /believers/{id}`（`BelieverApi.getById`）取完整信眾明細（含 zipcode ID）再走原本 `pickBeliever` 預填邏輯
  - **已知落差**：`/signups` 現有排序為 `Year, CeremonySort, NumberTitle, Number`（ascending，服務主列表用途），未暴露 `CeremonySort` 供前端精確重現舊排序（`Year desc, CeremonySort desc, NumberTitle asc, Number desc`）；前端改用整體反轉近似「新的在前」，未新增後端排序參數
  - 實作：[signup-edit-form.component](../../frontend/src/app/features/signups/signup-edit-form.component.ts) `triggerBelieverSearch` / `runBelieverSearch` / `pickBeliever`
- **重複報名警示（新版加值，2026-06-30）**：選定信眾後（或改年份/法會時）即時查該信眾在同一 `(Year, CeremonyCategoryID)`（**忽略報名類型**）是否已有報名；有則於信眾區塊下方跳 `.alert-warn` 警示並逐筆列「編號 · 報名類型」。**僅提醒、不阻擋**，使用者仍可照常儲存。判定走唯讀 `GET /signups/duplicates`（`SignupApi.checkDuplicates`），前端以 `combineLatest`（year/ceremony/believer 三 control，debounce 300ms）觸發。編輯模式帶 `excludeSignupId` 排除自身。詳見 [get-signup-duplicates.md](api-endpoints/get-signup-duplicates.md)、[business-rules-implicit.md](../business-rules-implicit.md) §1.4
- 「確認」→ `POST /api/v1/signups`（`CreateSignupHandler`，atomic：Insert Signups 自動 Number/NumberTitle + Insert SignupLogs 快照）；成功訊息「編號{number}，新增報名成功」
- 「列印資料卡」路徑仍待列印模組 PoC
- 實作：[signup-edit-form.component](../../frontend/src/app/features/signups/signup-edit-form.component.ts)（create/edit 共用）

### 編輯（EditSignupForm 對應）

```
1. 從 SignupForm 右鍵「修改資料」進入
2. 編輯區帶入既有資料
3. dlBeliever 下拉（AutoComplete）切換信眾：
   自動 sync HallName / EmployeeType / IsFixedNumber
   不自動 sync Name / Phone（保留 Signup 級獨立）
4. 修改 → 「確認」→ atomic transaction：
   1. Update Believers（HallName/EmployeeType/IsFixedNumber）
   2. Update Signups（全欄位含 Name/Phone）
   3. Insert SignupLogs（action=2=Update，完整快照）
   - 編號重複檢查排除自身 SignupID
5. 「修改報名成功」
```

- **重複報名警示同樣適用編輯**（共用 `signup-edit-form`）：若把年份/法會改成與該信眾另一筆相同，跳警示但**排除自己這筆**（`excludeSignupId=signupId`）；僅提醒、不阻擋。見上方「新增」段與 [get-signup-duplicates.md](api-endpoints/get-signup-duplicates.md)。

### 歷程（SignupLogForm 對應）

```
1. 從 SignupForm 右鍵「瀏覽歷程」進入
2. 唯讀 grid，Createdate DESC
3. 顯示 signup_logs 完整快照（含 action 標示新增/編輯/刪除）
```

## 設計決策

### 關鍵選擇

- **MediatR Command + TransactionBehavior** 包覆三段 SaveChanges
  - 舊：3 段 SaveChanges 非原子 → 中斷則資料不一致
  - 新：一個 EF transaction，任一失敗整個 rollback
- **編號生成改用樂觀鎖 + retry**
  - 舊：`MAX(number)+1` 兩人同時做 → race
  - 新：`INSERT ... OUTPUT inserted.number; IF duplicate THEN retry`，或用 sequence
- **NumberTitle 不可手動覆寫**
  - 由 `SignupType` 推導：1→No、2→寺、3→觀、4→普、5→郵
  - 在 Domain Service `NumberTitleResolver` 集中
- **避 4 顯示與資料分離**
  - DB 存實值（含 4）
  - 顯示層 `AvoidFourFormatter`：個位 4 → "3-1"
  - 例：104 → "103-1"
- **PredicateBuilder 改用 EF Core Expression composition**
  - 保留 AND/OR 語意，移除 LinqKit
- **資料帶入優先順序明確化**
  - Name/Phone：Signup → GridRow → Believer
  - Address：Signup → Believer
- **編輯時 Name/Phone 仍允許 Signup 級獨立**
  - 業務需要：報名快照可不同於 Believer 主檔
- **SignupLogs 現況無 `action` 欄位**（沿用既有 schema）
  - 沿用舊行為：同 SignupID 的第一筆 = 新增，後續 = 編輯
  - 刪除：可選擇是否寫 log；若寫，應用層在備註欄補「[Deleted]」標記
  - 若業務需求強烈，可走 DbUp migration 加 `action` 欄位（DB 已解除凍結，待評估）

### 取捨

- 取了：資料完整性、可審計、可平行操作
- 捨了：舊版簡單但有 race / 不原子的「直接寫」便利

## 跨層影響

| 層級 | 是否影響 | 變動摘要 |
|---|---|---|
| 視覺 | 是 | SignupForm / NewSignup / EditSignup / SignupLog 對應頁；訊息 verbatim |
| 前端 | 是 | signups feature 全部；data-grid context menu；wizard 元件；Signal-first store |
| 後端 | 是 | Signup Repository（Dapper）+ 6 個 Handler（Create/Update/Delete/Search/Logs/PrintPrepare） |
| API | 是 | `/signups/*` 完整 |
| 資料庫 | 現況否 | 本功能用既有 schema；DB 已可變更，加 action 欄位/索引為待評估 migration |
| 效能 | **是** | server-side 分頁、Dapper 直接 SQL、UPDLOCK 編號生成、in-memory cache；詳見 [performance.md](../design/performance.md) |
| 安全 | 部分 | 寫操作 Serilog file log（非 DB audit） |

## 業務規則摘要

| 規則 | 描述 |
|---|---|
| NumberTitle 推導 | 1=No, 2=寺, 3=觀, 4=普, 5=郵 |
| 寺方(2) 顯示 | 只顯示 NumberTitle，不加 Number |
| 避 4 顯示 | 個位 4 → "3-1"（DB 存實值） |
| 編號生成 | `MAX(year, ceremonyId, signupType, number)+1`；無記錄則 1 |
| 編號重複檢查 | (year, ceremonyId, signupType, number) 不可重複；編輯時排除自身 |
| 地址 fallback | 疏文空 → 用寄件 |
| 信眾資料帶入 | Name/Phone：Signup > GridRow > Believer；Address：Signup > Believer |
| 預繳自動帶入 | 該信眾「今年以前」最新報名的預繳資訊 |
| 編輯時 Believer 同步 | HallName / EmployeeType / IsFixedNumber；**不**同步 Name / Phone |
| 變更歷程 | 每次 Create/Update/Delete 插入 signup_logs 快照 |

## 驗收標準

- [ ] SignupForm 四面板版型與舊系統一致
- [ ] 40 欄 grid 預設 32 顯示；cbShowAll 控制 5 欄；10 內部欄永遠隱藏
- [ ] DeadName 1..5 欄背景 `#FFE0C0`
- [ ] AND/OR 搜尋邏輯與舊系統一致（全空 → 全部、勾選任一才能填 key）
- [ ] 「列印普桌」「列印普桌資料卡」有選取列即啟用，任何型別皆可印（前後端均不限 type）
- [ ] 編號重複（含編輯排除自身）回 409 + 訊息 verbatim
- [ ] 編號 4 顯示為「3-1」
- [ ] NumberTitle 無法手動覆寫（API 無此參數）
- [ ] 新增/編輯為單一 transaction，中斷 rollback
- [ ] 多用戶同時新增同年同法會同類型，編號不衝突
- [ ] 歷程頁面 Createdate DESC（依舊 schema，無 action 欄位）
- [ ] **Grid 右鍵 context menu 9 項齊備**（代入新增 / 修改資料 / 列印 5 種 / 刪除 / 瀏覽歷程）
- [ ] **右鍵啟用規則對齊舊系統**：代入新增 / 修改資料 / 瀏覽歷程 → 單選 only；列印 / 刪除 → 單 + 多選
- [ ] **「列印普桌／普桌資料卡」不 grey out**：有選取即 enable；選什麼印什麼（對齊舊系統）
- [ ] **多選 checkbox** + shift / cmd 範圍選取
- [ ] **批次列印面板**（起編號 / 迄編號 / reportType）獨立於 grid 選取，呼叫 `POST /reports/batch`
- [ ] 列印結果走新分頁 / iframe 預覽（不再有「PDF / 預覽列印」對話）
- [ ] 通過 [code-review](../workflows/code-review.md) 與 [qa-testing](../workflows/qa-testing.md)

## 效能要點（**資料越來越多會吃這裡；以應用層手段為主，索引可走 migration 補強**）

| 場景 | 措施 | 依據 |
|---|---|---|
| 搜尋大表 | server-side 分頁強制（pageSize ≤ 200）+ UI 必填年份+法會限縮範圍 | [performance.md](../design/performance.md) §2 |
| 編號生成 | `UPDLOCK + HOLDLOCK` 序列化 + 5 次 retry | §6 |
| 批次列印 100 筆 PDF | QuestPDF stream 到 HTTP response，不寫暫存 | §8 |
| Excel 匯出 5k 列 | ClosedXML；> 10k 用 OpenXmlWriter streaming | §9 |
| Grid 顯示大量 | `cdk-virtual-scroll-viewport` + server-side paging | §「DataGrid 虛擬滾動」 |
| AutoComplete 信眾 | 改 typeahead（debounce 300ms，後端模糊查詢回前 20 筆） | §「Search debounce」 |
| 編輯歷程列表 | 只取最近 100 筆 + Createdate DESC | §2 |
| 預繳查詢 | UI 必填來源年+法會限縮範圍 | §2 |
| 靜態資料（法會分類、Zipcodes） | IMemoryCache | §4 |

## 風險與未解問題

- 編號樂觀鎖 retry 上限 — 5 次後仍失敗回 503，前端引導重試
- AutoComplete 載入 50k+ 信眾 — **必須**改為遠端 typeahead，不可一次撈全
- 大表 search export 100k+ 列 → stream + 分批寫 Excel
- SignupLogs 預期成長最快（每次 edit 一筆）— 5 年後評估歸檔策略

## 參考資料

- [scratch/03-signup-main.md](../../.scratch/explore/03-signup-main.md)：SignupForm 四面板、40 欄、AND/OR、批次列印、Excel
- [scratch/04-signup-create-edit-prepay-category.md](../../.scratch/explore/04-signup-create-edit-prepay-category.md) §A/B：NewSignupForm 兩步驟、EditSignupForm Believer sync
- 舊原始碼：[SignupForm.cs](../../reference/old/Ceremony/SignupForm.cs)、[NewSignupForm.cs](../../reference/old/Ceremony/NewSignupForm.cs)、[EditSignupForm.cs](../../reference/old/Ceremony/EditSignupForm.cs)、[SignupLogForm.cs](../../reference/old/Ceremony/SignupLogForm.cs)
- [Library.cs](../../reference/old/Ceremony/Commons/Library.cs)：GetSignupNumber / GetNumberText
