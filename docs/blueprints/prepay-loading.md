---
title: 載入預繳
purpose: 將前一年/前一法會的預繳資料批次建立到目標年份；對應舊 LoadPrepayForm
status: draft
applicable_when: 要修改預繳邏輯、調整六 case 分類、改變編號填補規則
related_agents:
  - software-architect-blueprint
  - backend-engineer
  - frontend-architect
related_docs:
  - ../design/database-design.md
  - signup-management.md
  - ceremony-category.md
keywords: [prepay, 預繳, 載入預繳, LoadPrepayForm, 固定編號, 空號填補]
last_updated: 2026-07-17
---

## 背景與動機

預繳是寺方常用功能 — 信眾繳費時可同時預繳未來年份/法會的報名。年初開新法會時，將去年/前法會中標記為「預繳至本期」的信眾批次建立新一筆 Signup 紀錄。舊系統 6 個 case（依 SignupType × EmployeeType）+ 固定編號優先 + 空號填補的複雜邏輯必須完整保留。

## 範圍

### 做什麼
- 從來源（年+法會）查詢有預繳的 Signups
- 依 6 case 分類處理
- 每 case 內：固定編號優先保留 + 非固定填補空號 + 續序
- 批次建立到目標（年+法會），單一 transaction commit
- 預覽模式（不寫入，僅顯示預期結果）

### 不做什麼
- 預繳金額計算 / 收款
- 跨多目標年同時載入（一次只能載一個目標）
- 自動排程載入（手動觸發）

## 使用者流程

```
1. 進入 /prepay
2. 選來源年 + 來源法會
3. 選目標年 + 目標法會
4. 選 dlBeliever（6 個 case 之一）
5. 「載入」→ 後端執行 → 顯示「載入 N 筆、跳過 M 筆」摘要
```

> **預覽模式未實作（對齊舊系統）**：舊 LoadPrepayForm 僅有一個 Yes/No 確認框、無預覽，直接載入。新版對齊此行為，只在載入後回傳筆數/跳過/填補空號摘要，不做「預覽不寫入」的獨立步驟。

### 前端控件對齊（2026-07-04，對齊舊 LoadPrepayForm 畫面）

| 控件 | 對齊做法 |
|---|---|
| 來源/目標法會下拉 | **只列根法會**（`ParentID==null` 依 Sort），不攤平子法會 — 舊 `LoadSelectCeremony`/`LoadCeremony` |
| 來源年下拉 | 本年往前 5 年（`LoadSelectYear`）|
| 目標年下拉 | 本年 + 明年（`LoadYear`）|
| 信眾分組 | 6 項標籤用舊詞序（一般非員工／一般地藏殿員工／寺方／觀音會／郵撥大殿員工／郵撥非員工，`LoadBeliever`）|
| 載入前 | `confirm("是否載入…?")` 二次確認（舊 `btnConfirm_Click` MessageBox）|
| 載入結果 | **保留新版 KPI 卡**（loaded/skipped/固定/非固定/延展/補號）— 刻意的增強，不退回舊 MessageBox |

年份下拉在前端用 `[ngValue]` 綁定以保留 number 型別（避免變字串送到後端 int 欄位）。

## 六 Case 完整邏輯

| Case | dlBeliever 顯示 | SignupType | EmployeeType | 過濾條件 |
|---|---|---|---|---|
| 1 | 一般非員工 | 1 | 1 | SignupType==1 && EmployeeType==1 |
| 2 | 一般地藏殿員工 | 1 | 3 | SignupType==1 && EmployeeType==3 |
| 3 | 寺方 | 2 | any | SignupType==2 |
| 4 | 觀音會 | 3 | any | SignupType==3 |
| 5 | 郵撥大殿員工 | 5 | 2 | SignupType==5 && EmployeeType==2 |
| 6 | 郵撥非員工 | 5 | 1 | SignupType==5 && EmployeeType==1 |

### 每 Case 內的子流程

**Step A — 固定編號信眾（IsFixedNumber=true）**：保留原號碼、記錄空號

```pseudo
nextNumber = (lastsignup ? lastsignup.Number + 1 : 1)
gapList = []

foreach fixedSignup in fixedSignups (排序 by Number):
    create new Signup { Number = fixedSignup.Number, ... }
    if (nextNumber != fixedSignup.Number):
        // 中間有空號 → 記錄
        for x in [nextNumber .. fixedSignup.Number - 1]:
            gapList.append(x)
        nextNumber = fixedSignup.Number + 1
    else:
        nextNumber += 1
```

**Step B — 非固定編號信眾（IsFixedNumber=false）**：先填空號、再續序

```pseudo
i = 0
foreach nonFixedSignup in nonFixedSignups:
    create new Signup { ... }
    if (i < gapList.length):
        signup.Number = gapList[i]
        i += 1
    else:
        signup.Number = nextNumber
        nextNumber += 1
```

例：固定編號 [1, 2, 5, 6]（空號 [3, 4]）+ 2 筆非固定 → 非固定編號 [3, 4]；下一筆續為 7。

> **計數器一律 `nextNo = 固定號 + 1`（含往回設，對齊舊系統）**：舊 LoadPrepayForm 每處理一個固定號後一律把計數器設為 `固定號 + 1`（[LoadPrepayForm.cs:132](../../reference/old/Ceremony/LoadPrepayForm.cs#L132)/136）。當固定號**小於**當前計數器（僅在「目標年/法會已有既存資料且固定號落在既存範圍」的邊界才發生）時，舊系統會把計數器「往回設」。新版 `PrepayNumberAllocator` **刻意保留同一行為**（不用 `Math.Max`），以完全對齊舊輸出；此邊界的取捨記於 [gotchas.md](../gotchas.md)。

### 預繳條件 Predicate（全 case 共用）

```sql
WHERE prepay_year IS NOT NULL
  AND (
    (prepay_year = @target_year AND prepay_ceremony.sort >= @target_ceremony.sort)
    OR
    (prepay_year > @target_year AND prepay_ceremony_category_id IS NOT NULL)
  )
```

意義：
- 預繳年 = 目標年 時，只取「排序在目標法會之後（含）」的預繳
- 預繳年 > 目標年 時，全部載入（保留 prepay info 給更後續法會）

## 複製欄位 vs 不複製

**複製**：SignupType / BelieverID / NumberTitle / Fee / 6×LivingName / 6×DeadName / MailZipcodeID+MailAddress / TextZipcodeID+TextAddress / Remark / 符合條件的 prepay info

> 註：舊系統另會複製 `MailZipcode`/`TextZipcode` 非正規化**字串**欄；新版（含一般報名 POST /signups）一律不寫這兩欄——顯示端（SignupView 城市/區域、表單區號、列印）皆由 `MailZipcodeID` join Zipcodes 推導，該字串欄無讀取方，屬無實質影響的系統性差異（2026-07-17 對齊核對確認）。

**不複製**：Name / Phone（新建 Signup 此兩欄為 **null**；列印時若需姓名則從 Believer 取）

> ✅ **已對齊（2026-07-04）**：舊 LoadPrepayForm 建立的 Signup 完全不設 Name/Phone（[LoadPrepayForm.cs:84-115](../../reference/old/Ceremony/LoadPrepayForm.cs#L84-L115)），而一般報名 NewSignupForm **有**設（[NewSignupForm.cs:253-254](../../reference/old/Ceremony/NewSignupForm.cs#L253)）。新版 `PrepayLoadHandler.BuildCandidate` 曾誤從來源複製 Name/Phone，已改為 `null`，與舊系統一致。
>
> 🔧 **修正（2026-07-17）**：7/4 版把 **SignupLog 快照的 Name 也設成 null**，但 `dbo.SignupLogs.Name` 是 **NOT NULL**（`dbo.Signups.Name` 才是 nullable）→ 每次真實載入必觸發 SqlException 515、整批 rollback、前端顯示「未預期的伺服器錯誤」。SignupLog 是新版補強（舊系統載入預繳**不寫 log**），不受「對齊舊系統」約束——改為比照 POST /signups 的 log 語意寫入**信眾姓名快照**（`Believers.Name`，NOT NULL 保證有值）；Signup.Name 維持 null 不變、兩者 Phone 皆 null。當時 326 測試全綠仍漏掉的原因見 [gotchas.md](../gotchas.md)「SignupLogs.Name NOT NULL」條。

## 設計決策

### 關鍵選擇

- **保留 6 case 結構**（不重構為 generic strategy）
  - 理由：6 case 對應具體業務分類，明確優於抽象
- **單一 EF transaction**
  - 舊：6 case Create 完一次 SaveChanges
  - 新：用 IDbTransaction 明確包覆
- **新增 idempotency 檢查**（**舊系統無**，重要安全網）
  - 載入時於交易內 SELECT 目標 `(year, ceremony, signup_type)` 已存在的 BelieverID
  - 已存在的信眾 → 不重複 insert，計入 `skipped`（re-run 變 no-op，比 409 更貼合語意）
- **並行鎖已實作**（[PrepayRepository.InsertPrepayBatchAsync](../../backend/src/Ceremony.Infrastructure/Repositories/PrepayRepository.cs)）
  - 整個「讀 MAX → 配號 → insert」收在**單一 transaction**
  - `SELECT MAX(Number) WITH (UPDLOCK, HOLDLOCK)`：範圍鎖擋住並發的一般報名/預繳插入，杜絕重號（與 `SignupRepository.InsertWithLogAsync` 同一套機制）
  - `sp_getapplock`（Exclusive / Transaction / 30s）：序列化「同 year×ceremony×signupType」的並發載入；逾時回 `PREPAY_BUSY`
- **不實作「預覽」模式**（對齊舊系統，見上方使用者流程說明）
- **gap list 維持 stateful in-memory**（抽為純函式 `PrepayNumberAllocator`，可單元測試）
- **CeremonyCategorys 用 sort 排序**

### 取捨

- 取了：可審計（loaded/skipped/filledGaps 摘要）、單一 transaction、並行安全、演算法可單元測試
- 捨了：不做「預覽」步驟（對齊舊系統的單鍵載入，避免多一步操作）

## 跨層影響

| 層級 | 是否影響 | 變動摘要 |
|---|---|---|
| 視覺 | 是 | LoadPrepayForm 對應頁，固定小窗 modal |
| 前端 | 是 | prepay feature（單一「載入」動作，無預覽） |
| 後端 | 是 | PrepayLoadHandler + PrepayNumberAllocator（純函式配號） |
| API | 是 | `POST /prepay/load`（無 `/preview`） |
| 資料庫 | 是 | 用 signups 表既有 schema；補 prepay 查詢 index |
| 安全 | 部分 | Admin role only |

## 驗收標準

- [x] 6 case 用 `PrepayGroups` 表驅動（SignupType × EmployeeType）
- [x] gap-filling 演算法與舊系統輸出對齊（含 `nextNo = 固定號+1` 往回設；`PrepayNumberAllocatorTests`）
- [x] 預繳條件 predicate 完整支援同年/跨年情境
- [x] 不複製 Name / Phone（新 Signup 兩欄為 null；SignupLog 快照 Name＝信眾姓名（DB NOT NULL）、Phone＝null；`PrepayLoadHandlerTests` + 整合測試真實載入回歸鎖）
- [x] 載入失敗整個 rollback（單一 transaction）
- [x] 並行安全：UPDLOCK/HOLDLOCK 讀 MAX + `sp_getapplock`（真實 MSSQL 整合測試）
- [ ] 通過 [qa-testing](../workflows/qa-testing.md)（實機多筆載入驗收待印表機/現場）

## ⚠️ 舊系統無 idempotency（**新版必修**）

舊 LoadPrepayForm（line 45-823）的問題：

| 問題 | 細節 |
|---|---|
| 無 idempotency 檢查 | 連按確認 / 重啟後再跑 → **產生重複資料** |
| 唯一防護 | `btnConfirm.Enabled = false`（line 63）— UI 鎖，可被外部事件繞過 |
| 無顯式 transaction | EF SaveChanges 自帶；6 case 全成功才 commit |
| 部分失敗難判斷 | 中斷後使用者無法得知做到哪 |

**新版**：詳見「關鍵選擇」§ idempotency。前端按鈕點擊鎖 + 後端業務鎖雙重保護。

## 風險與未解問題

- 同 case 內信眾排序：來源查詢 `ORDER BY IsFixedNumber DESC, Number`，fixed/non-fixed 再各自 `OrderBy(Number)` — 對齊舊系統的 `OrderBy(o => o.Number)`
- ~~gap-filling 演算法易誤解~~ ✅ 抽為純函式 `PrepayNumberAllocator` + 專屬單元測試（含往回設邊界）
- ~~舊系統無 idempotency~~ ✅ **新版已標準化** idempotency 檢查（交易內比對已存在 BelieverID）
- ~~舊系統無並行鎖~~ ✅ **新版已補** UPDLOCK/HOLDLOCK + `sp_getapplock`
- **re-run 邊界**：idempotency 以「已存在 BelieverID」跳過；配號的 gap 計算仍以 `MAX(Number)` 為基準，不回填目標既有編號序列中的任意歷史空洞（舊系統亦然，屬正常年初空法會載入以外的邊界，不特別處理）

## 參考資料

- [scratch/04-signup-create-edit-prepay-category.md](../../.scratch/explore/04-signup-create-edit-prepay-category.md) §C
- 舊原始碼：[reference/old/Ceremony/LoadPrepayForm.cs](../../reference/old/Ceremony/LoadPrepayForm.cs)
