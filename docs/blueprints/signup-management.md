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
keywords: [signup, 報名, 報名維護, 編號, NumberTitle, 避4, PredicateBuilder, SignupForm, context-menu, 右鍵, 多選, 批次列印]
last_updated: 2026-06-30 (新增重複報名警示：同信眾同年同法會即時提示但不阻擋)
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
| 2 | 修改資料 | navigate `/signups/:id/edit` | **單選** | 全部 | 對應 `tsmiEdit_Click` (cs:168)：未選列 verbatim「尚未選擇報名資料」 |
| 3 | 列印資料卡 | `GET /api/v1/reports/datacard?signupId=` ×N（或前端拼批次） | 單 / 多選皆可 | 全部 | 對應 `tsmiPrintDataCard_Click` (cs:188) |
| 4 | 列印收據 | `GET /api/v1/reports/receipt?signupId=` | 單 / 多選 | 全部 | `tsmiPrintReceipt_Click` (cs:242) |
| 5 | 列印薦牌 | `GET /api/v1/reports/tablet?signupId=` | 單 / 多選 | 全部 | `tsmiPrintTablet_Click` (cs:273)；含 HallName 拆字邏輯（前端不重作，後端已處理） |
| 6 | 列印文牒 | `GET /api/v1/reports/text?signupId=` | 單 / 多選 | 全部 | `tsmiPrintText_Click` (cs:323) |
| 7 | 列印普桌 | `GET /api/v1/reports/worship?signupId=` | 單 / 多選 | **選取每一列 `signupType == 4` 才啟用**（與搜尋篩選無關）；混選非普桌即 grey out | `tsmiPrintWorship_Click` (cs:380)；backend 已硬擋（單筆非 4 回 422 `WORSHIP_ONLY_TYPE_4`、批次強制 type=4）；前端 2026-06-29 起改驗證選取列而非搜尋篩選 |
| 8 | 刪除資料 | `DELETE /api/v1/signups/:id` ×N | 單 / 多選 | 全部 | `tsmiDelete_Click` (cs:405)；多選逐筆呼叫；需二次確認 dialog |
| 9 | 瀏覽歷程 | navigate `/signups/:id/logs` | **單選** | 全部 | `tsmiLog_Click` (cs:428) |

**選列邏輯**：
- 未選 (count == 0)：除 3–7（多選列印）走「印出當前篩選結果」可選擇 disable 全部以維持舊行為；採後者，**未選列右鍵不開選單**或開但全部 disable（取後者，使用者察覺自己沒選到）
- 單選 (count == 1)：全部 enable（含 1, 2, 9）；列印走單筆 endpoint
- 多選 (count > 1)：1, 2, 9 disable；3–7 enable（多筆呼叫 batch endpoint 或前端逐筆）；8 enable

**普桌啟用條件（2026-06-29 改）**：
- 改看**實際選取列**：`selected.length >= 1 && selected.every(r => r.signupType === 4)` → 「列印普桌」enable
- 混選含非普桌 → grey out + tooltip「選取含 N 筆非普桌資料，僅普桌(類型 4)可列印」
- **與搜尋篩選 `signupType` 解耦**：在「全部」篩選下挑出純普桌列也能列印（舊版需先把篩選切成普桌）
- 安全性未降低：後端單筆 by-id 驗證 type=4、批次 `BatchReportHandler` 強制 type=4，前端只放行純普桌選取（詳見 [business-rules-implicit.md §16](../business-rules-implicit.md)）

**多筆列印實作策略**：
- 選 ≤ 3 筆 → 逐筆呼叫單筆 endpoint，前端用 PdfMerger（或 lazy：開多個 tab）
- 選 > 3 筆 → 直接呼叫 `POST /api/v1/reports/batch`（需湊出連續編號區間；不連續時改用 numberStart=min, numberEnd=max + 前端 filter，trade-off：可能多印不在選取的編號）
- **暫定 v1**：只支援單筆 + batch 區間（兩條路）；右鍵列印改成「以選列的編號區間批次列印」並顯示 confirmation；多筆「選 8 筆但只想印這 8 筆」未來再做
- 觸發方式：右鍵 menu / 列尾 kebab menu / 鍵盤 `Menu` 鍵 / 長按（touch）

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
法會資料   民國年(預設 TaiwanCalendar.GetYear) / 法會分類 / 報名類型
信眾       modal picker 搜尋既有信眾 → 選定後預填（取代舊 in-form dgvBelievers）
基本資料   員工類型(唯讀) / 堂號 / 姓名 / 聯絡電話
地址       寄件城市→區域(連動下拉)→郵遞區號(唯讀) / 寄件地址
           ☑ 同寄件地址（複製 mail→text；mail 空 → 「請先輸入寄件地址」）
           文牒城市→區域→郵遞區號 / 文牒地址
名單       陽上 ×6 / 往生 ×6
編號/費用  ☑ 指定編號 + 編號 / 費用
備註/預繳  備註 / 預繳民國年 / 預繳法會
```

- **法會分類依當月自動帶季別（新版加值，2026-06-23）**：新增模式下載完分類樹後，依當前月份自動把「法會分類」預設為對應季別 root（1-4月→春季 / 5-8月→中元 / 9-12月→秋季，見 [business-rules-implicit.md](../business-rules-implicit.md) §月→季）。為**可編輯的預設**：使用者仍可改選任何季別或子法會（子法會仍人工挑選，月份只決定季別）。僅在 create 模式且使用者尚未選值時帶入；編輯模式不覆蓋既有 ceremony。實作：`util/ceremony-season.ts`（`currentSeason` / `resolveSeasonRootId`，GUID 優先、title 退場）+ `signup-edit-form` `applySeasonDefault()`
- 城市/區域連動下拉資料源：`GET /zipcodes/cities`、`GET /zipcodes?city=`（見 [get-zipcodes.md](api-endpoints/get-zipcodes.md)）；對齊舊 `LoadCity` / `dlMailCity_SelectedIndexChanged`
- **員工類型 + 固定編號唯讀顯示**：新流程不於報名建立時改信眾屬性（inline 新建/編輯 Believer 故意捨棄，於信眾維護調整）。`BelieverListItem` 已含 `IsFixedNumber`（2026-06-02），報名表單唯讀顯示「固定編號 是/否」
- **選信眾自動帶入預繳歷史**：`pickBeliever` 呼叫 `GET /prepay?believerId&year`，最新報名有預繳則帶入預繳年/法會（對齊舊 `BelieverSelected:1102-1115`；見 [get-prepay-believer-latest.md](api-endpoints/get-prepay-believer-latest.md)）
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
- [ ] 「列印普桌」僅當選取列全為 SignupType=4 時啟用（與搜尋篩選解耦）
- [ ] 編號重複（含編輯排除自身）回 409 + 訊息 verbatim
- [ ] 編號 4 顯示為「3-1」
- [ ] NumberTitle 無法手動覆寫（API 無此參數）
- [ ] 新增/編輯為單一 transaction，中斷 rollback
- [ ] 多用戶同時新增同年同法會同類型，編號不衝突
- [ ] 歷程頁面 Createdate DESC（依舊 schema，無 action 欄位）
- [ ] **Grid 右鍵 context menu 9 項齊備**（代入新增 / 修改資料 / 列印 5 種 / 刪除 / 瀏覽歷程）
- [ ] **右鍵啟用規則對齊舊系統**：代入新增 / 修改資料 / 瀏覽歷程 → 單選 only；列印 / 刪除 → 單 + 多選
- [ ] **「列印普桌」grey-out 規則**：選取列 `every(signupType === 4)` 才 enable；混選非普桌 grey out + tooltip
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
