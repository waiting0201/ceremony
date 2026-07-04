---
title: POST /api/v1/prepay/load
purpose: 將某年某法會的預繳信眾批次載入到目標年某法會（核心業務，6 種信眾分類各自處理）
status: shipped
endpoint: post-prepay-load
http_method: POST
route: /api/v1/prepay/load
legacy_form: LoadPrepayForm.cs
legacy_lines: 45-824
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../../design/database-design.md
  - ../../design/backend-design.md
  - ../prepay-loading.md
  - ../legacy-coverage/load-prepay-form.md
  - post-signups.md
keywords: [prepay, load, batch, strategy, idempotent, 6 case]
last_updated: 2026-07-04
---

## 規格

`POST /api/v1/prepay/load`，需要 JWT。

### Request DTO

```jsonc
{
  "sourceYear": 114,                                  // 預繳資料所在年份
  "sourceCeremonyId": "guid",                         // 預繳資料所在法會
  "targetYear": 115,                                  // 載入到的目標年份
  "targetCeremonyId": "guid",                         // 載入到的目標法會
  "believerGroup": 1                                  // 1..6（見下方分組表）
}
```

### Response

```jsonc
// 200 OK
{
  "loaded": 25,                                       // 本次新建的 Signup 數
  "skipped": 3,                                       // 已存在（idempotency 跳過）數
  "details": {
    "fixedLoaded": 8,
    "nonFixedLoaded": 17,
    "carriedForwardPrepay": 5,                        // 含未來年度預繳的筆數
    "filledGaps": [10, 12, 18]                        // 用固定編號跳號填補的編號
  }
}
```

### 錯誤碼

| HTTP | errorCode | message verbatim | 觸發 |
|---|---|---|---|
| 400 | `VALIDATION_REQUIRED` | `請選擇年份` | targetYear 為 0 |
| 400 | `VALIDATION_REQUIRED` | `請選擇法會` | targetCeremonyId 為 Guid.Empty |
| 400 | `VALIDATION_INVALID` | `無效的信眾類別` | believerGroup 不在 1..6 |
| 404 | `CATEGORY_NOT_FOUND` | `找不到目標法會` | targetCeremonyId 不存在於 CeremonyCategorys |
| 401 | – | – | 無 JWT |

## 6 個信眾分組（strategy table）

| 編號 | 名稱 | SignupType | EmployeeType | 對應舊 case |
|---|---|---|---|---|
| 1 | 非員工一般 | 1 (一般) | 1 (非員工) | `case 1` line 73-197 |
| 2 | 地藏殿員工一般 | 1 (一般) | 3 (地藏殿) | `case 2` line 199-321 |
| 3 | 寺方 | 2 (寺方) | – (any) | `case 3` line 323-445 |
| 4 | 觀音會 | 3 (觀音會) | – (any) | `case 4` line 447-569 |
| 5 | 大殿員工郵撥 | 5 (郵撥) | 2 (大殿) | `case 5` line 571-693 |
| 6 | 非員工郵撥 | 5 (郵撥) | 1 (非員工) | `case 6` line 695-817 |

**6 cases 的結構幾乎完全相同**，僅差 SignupType + EmployeeType filter。本實作用 strategy table（`Domain.Services.PrepayGroups`）統一處理。

## 演算法（refactored from 780-line legacy switch）

### Step 1: 驗證輸入
- targetYear 必填、targetCeremonyId 必填且存在於 CeremonyCategorys
- believerGroup 1..6

### Step 2: 查目標法會 Sort

```sql
SELECT Sort FROM dbo.CeremonyCategorys WHERE CeremonyCategoryID = @TargetCeremonyId
```

### Step 3: 查目標年度法會的最大編號（決定起始號）— **交易內、上鎖**

```sql
SELECT ISNULL(MAX(Number), 0) FROM dbo.Signups WITH (UPDLOCK, HOLDLOCK)
WHERE Year=@TargetYear AND CeremonyCategoryID=@TargetCeremonyId AND SignupType=@SignupType
```

> MAX 讀取與後續 insert 同一 transaction，`UPDLOCK, HOLDLOCK` 範圍鎖持有到 commit，
> 擋住並發的一般報名/預繳插入，杜絕重號。整段另以 `sp_getapplock`（同 year×ceremony×signupType）序列化。

### Step 4: 查源信眾（含 fixed 與 non-fixed 兩批）

```sql
SELECT s.*, b.IsFixedNumber, b.EmployeeType, c.Sort AS PrepayCeremonySort
FROM dbo.Signups s
INNER JOIN dbo.Believers b ON b.BelieverID = s.BelieverID
LEFT JOIN dbo.CeremonyCategorys c ON c.CeremonyCategoryID = s.PrepayCeremonyCategoryID
WHERE s.Year = @SourceYear
  AND s.CeremonyCategoryID = @SourceCeremonyId
  AND s.SignupType = @SignupType
  AND (@EmployeeType IS NULL OR b.EmployeeType = @EmployeeType)
  AND s.PrepayYear IS NOT NULL
  AND (
       (s.PrepayYear = @TargetYear AND c.Sort >= @TargetSort)
    OR (s.PrepayYear > @TargetYear AND s.PrepayCeremonyCategoryID IS NOT NULL)
  )
ORDER BY b.IsFixedNumber DESC, s.Number
```

> `IsFixedNumber DESC` 讓 fixed 先處理（決定 gaps），non-fixed 後處理（填 gaps）。

### Step 5: 兩階段配號（對齊舊 line 113-196 行為）

```
gaps = []
nextNo = maxNumber + 1

# 先剔除已存在的信眾（idempotency，交易內比對 BelieverID）→ skipped
# 第一批：fixed（保留原號、收集跳號）
for n in fixed_preserved_numbers ordered asc:
  fixed_number = n                       # 保留原號
  if nextNo < n:
    gaps.extend(range(nextNo, n))
  nextNo = n + 1                          # 一律 n+1（含往回設，對齊舊 line 132/136；不用 max()）

# 第二批：non-fixed（先填 gaps，取完續 nextNo）
gapIdx = 0
for _ in nonFixed:
  if gapIdx < len(gaps): number = gaps[gapIdx]; gapIdx++
  else: number = nextNo; nextNo++
```

> 配號抽為純函式 `Domain.Services.PrepayNumberAllocator`（可單元測試）。
> `nextNo = n + 1`（非 `max`）刻意對齊舊系統：固定號小於計數器時會往回設，僅在「目標已有既存資料」邊界發生。

### Step 6: PrepayYear 結轉邏輯（舊 line 113-120）

```
if (source.PrepayYear == targetYear AND source.PrepayCeremonySort > targetSort)
   OR source.PrepayYear > targetYear:
  carry_forward_prepay_to_new_signup
else:
  new_signup.PrepayYear = null, PrepayCeremonyCategoryId = null   # 已結算
```

### Step 7: Idempotency（**新版補強，舊系統無**）

交易內一次撈出目標 `(Year, Ceremony, SignupType)` 已存在的 BelieverID：
```sql
SELECT BelieverID FROM dbo.Signups
WHERE Year=@TargetYear AND CeremonyCategoryID=@TargetCeremonyId AND SignupType=@SignupType
```
候選中 BelieverID 已存在者 → 計入 `skipped`、不 insert（配號只在剩餘候選上進行）。

**設計理由**：業務上每信眾於某年某法會某類型只能有 1 筆 signup，這是自然唯一鍵。re-run 變成 no-op + report skipped，比 idempotency token 更貼合語意。

### Step 8: SignupLog 同步寫入

每筆新建的 Signup 同交易插入對應的 SignupLog（與 [post-signups.md](post-signups.md) 同 pattern）。

## 業務規則

- **每信眾 × 年 × 法會 × SignupType 唯一**：應用層 enforce（DB 無 unique constraint）
- **PrepayYear 結轉**：未來年度持續累積；當年同 ceremony or 更前的 ceremony Sort 已結算（不繼續攜帶）
- **AdminID + AdminName** 從 JWT claim（與 POST /signups 一致）

## 舊系統對照（forward）

| 舊行為 | 行 | 對應新版 |
|---|---|---|
| 6 個 case switch | `LoadPrepayForm.cs:70-818` | 1 個 strategy + `PrepayGroups` table |
| 每 case 各自取最大編號 | line 75/200/324… | 1 次 `SELECT MAX WITH (UPDLOCK,HOLDLOCK)`（交易內） |
| Fixed 跳號→gaps 收集、`oneno = Number+1` | line 125-136 | `PrepayNumberAllocator`（含往回設，`nextNo = n+1`） |
| Non-fixed 從 gaps 取號 | line 184-193 | 同 |
| PrepayYear 結轉 | line 117 | step 6 同條件 |
| **Name/Phone 留 null** | line 84-115（未設） | ✅ 對齊：`BuildCandidate` Name/Phone = null |
| 無 idempotency | – | **新版 step 7 補強** |
| 無並行鎖（單機 WinForms） | – | **新版補** UPDLOCK/HOLDLOCK + `sp_getapplock` |
| 無顯式 transaction（EF SaveChanges） | line 820 | **新版**單一 SqlTransaction，失敗全 rollback |
| 無 SignupLog | – | **新版 step 8 補強**（對齊 POST /signups 一致性） |

## 邊界 case

| 場景 | 行為 |
|---|---|
| 源資料無預繳 | loaded=0, skipped=0 |
| 全部已載入（re-run） | loaded=0, skipped=N |
| 部分已載入 + 部分新 | 只 insert 未載入的 |
| Fixed 編號跳號 | gaps 自動收集 → non-fixed 填補 |
| PrepayYear > targetYear | 結轉 PrepayYear/Category |
| PrepayYear == targetYear AND target.Sort < source.PrepayCeremonySort | 結轉（仍在未來） |
| PrepayYear == targetYear AND target.Sort >= source.PrepayCeremonySort | 不結轉（已結算） |

## 驗收

- [x] 6 strategy cases 用 table-driven，不重複 switch
- [x] Idempotency 自然 dedup（per believer）
- [x] Fixed/non-fixed gap-fill 對齊舊行為（含 `nextNo = n+1` 往回設）
- [x] Name/Phone 留 null（對齊舊系統）
- [x] 並行鎖：UPDLOCK/HOLDLOCK + `sp_getapplock`（交易內配號）
- [x] PrepayYear 結轉條件對齊舊行為
- [x] SignupLog 同步寫入（補強）
- [x] 對應 [load-prepay-form.md](../legacy-coverage/load-prepay-form.md) rows 2, 3 ✅
- [x] 含 unit tests（演算法）+ integration test (real DB)
- [ ] 通過 [code-review](../../workflows/code-review.md)

## 風險與未解問題

- ✅ **並發兩個 admin 同時 load 同一組**：`sp_getapplock` 序列化 → 後者等前者 commit 後再跑，其插入的信眾已存在 → 全 skip；MAX 的 UPDLOCK/HOLDLOCK 另擋一般報名的並發插入
- **大批次 transaction 太長**：500+ 筆同 transaction 且持有範圍鎖，可能 lock 較久；目前單一 transaction，未來可評估 chunked commit（但會犧牲整批原子性）
- **PrepayCeremonyCategorys 在源年度已不存在**：query 用 LEFT JOIN 避免崩；Sort=null 視為 0
- **re-run 的 gap 計算**：以 `MAX(Number)` 為基準續號，不回填目標既有序列中的任意歷史空洞（舊系統亦然）
