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
last_updated: 2026-05-26
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
5. 「預覽」→ 顯示預期建立筆數與編號分配
6. 「載入」→ 後端執行 → 「載入完成，共建立 N 筆」
7. 自動跳回 SignupForm 顯示新建紀錄
```

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

**複製**：SignupType / BelieverID / NumberTitle / Fee / 6×LivingName / 6×DeadName / Mail{Zipcode/Address} / Text{Zipcode/Address} / Remark / 符合條件的 prepay info

**不複製**：Name / Phone（新建 Signup 此兩欄為 null；列印時若需姓名則從 Believer 取）

## 設計決策

### 關鍵選擇

- **保留 6 case 結構**（不重構為 generic strategy）
  - 理由：6 case 對應具體業務分類，明確優於抽象
- **單一 EF transaction**
  - 舊：6 case Create 完一次 SaveChanges
  - 新：用 IDbTransaction 明確包覆
- **新增 idempotency 檢查**（**舊系統無**，重要安全網）
  - 載入前 SELECT 目標 `(year, ceremony_id, signup_type 範圍)` 是否已有 Signup
  - 若已有 → 回 409 Conflict，要求確認後加 `--force` 才能重跑
  - 用 `sp_getapplock` 防止並行載入
- **新增「預覽」模式**
  - 舊系統無；新版讓使用者先看清預期結果
- **gap list 維持 stateful in-memory**
- **CeremonyCategorys 用 sort 排序**

### 取捨

- 取了：可預覽、可審計、單一 transaction
- 捨了：使用者可能不適應「預覽」多一步；提供「直接載入」快速鈕

## 跨層影響

| 層級 | 是否影響 | 變動摘要 |
|---|---|---|
| 視覺 | 是 | LoadPrepayForm 對應頁，固定小窗 modal |
| 前端 | 是 | prepay feature |
| 後端 | 是 | LoadPrepayHandler / PrepayPreviewHandler |
| API | 是 | `/prepay/load`、`/prepay/preview` |
| 資料庫 | 是 | 用 signups 表既有 schema；補 prepay 查詢 index |
| 安全 | 部分 | Admin role only |

## 驗收標準

- [ ] 6 case 全部測試（每 case 含 fixed/non-fixed 子情境）
- [ ] gap-filling 演算法與舊系統輸出 1:1 比對
- [ ] 預繳條件 predicate 完整支援同年/跨年情境
- [ ] 不複製 Name / Phone（驗證新 Signup 兩欄為 null）
- [ ] 載入失敗整個 rollback
- [ ] 預覽不寫入 DB
- [ ] 通過 [qa-testing](../workflows/qa-testing.md)

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

- gap-filling 演算法易誤解 — 建議 unit test 用真實案例驅動
- 同 case 內信眾排序對結果影響 — 確認舊系統用何順序（推測：BelieverID 或 Number）；測試前需驗證
- 預覽 vs 載入結果是否完全一致 — 兩者用同一 handler，分支只在 SaveChanges
- ~~舊系統無 idempotency~~ ✅ **新版已標準化** idempotency 檢查（見上）

## 參考資料

- [scratch/04-signup-create-edit-prepay-category.md](../../.scratch/explore/04-signup-create-edit-prepay-category.md) §C
- 舊原始碼：[reference/old/Ceremony/LoadPrepayForm.cs](../../reference/old/Ceremony/LoadPrepayForm.cs)
