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
last_updated: 2026-05-27
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

### Step 3: 查目標年度法會的最大編號（決定起始號）

```sql
SELECT ISNULL(MAX(Number), 0) FROM dbo.Signups
WHERE Year=@TargetYear AND CeremonyCategoryID=@TargetCeremonyId AND SignupType=@SignupType
```

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

# 第一批：fixed
for source in fixed_sources ordered by Number:
  if already_loaded(targetYear, targetCeremonyId, source.BelieverID, signupType):
    skipped++
    continue
  insert_clone(source, preserve_number=source.Number)
  if nextNo != source.Number:
    gaps.extend(range(nextNo, source.Number))
  nextNo = max(nextNo, source.Number + 1)

# 第二批：non-fixed
gapIdx = 0
for source in nonFixed_sources ordered by Number:
  if already_loaded(...): skipped++; continue
  if gapIdx < len(gaps): number = gaps[gapIdx]; gapIdx++
  else: number = nextNo; nextNo++
  insert_clone(source, number=number)
```

### Step 6: PrepayYear 結轉邏輯（舊 line 113-120）

```
if (source.PrepayYear == targetYear AND source.PrepayCeremonySort > targetSort)
   OR source.PrepayYear > targetYear:
  carry_forward_prepay_to_new_signup
else:
  new_signup.PrepayYear = null, PrepayCeremonyCategoryId = null   # 已結算
```

### Step 7: Idempotency（**新版補強，舊系統無**）

每筆 source 插入前先 check：
```sql
SELECT COUNT(1) FROM dbo.Signups
WHERE Year=@TargetYear AND CeremonyCategoryID=@TargetCeremonyId
  AND SignupType=@SignupType AND BelieverID=@BelieverId
```
若已存在 → 計入 `skipped`，不重複 insert。

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
| `Library.GetSignupNumber` 取最大 | (per case 重複) | 1 次 `SELECT MAX + UPDLOCK` |
| Fixed 跳號→gaps 收集 | line 125-136 | 演算法 step 5 同邏輯 |
| Non-fixed 從 gaps 取號 | line 187-196 | 同 |
| PrepayYear 結轉 | line 113-120 | step 6 同條件 |
| 無 idempotency | – | **新版 step 7 補強** |
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
- [x] Fixed/non-fixed gap-fill 對齊舊行為
- [x] PrepayYear 結轉條件對齊舊行為
- [x] SignupLog 同步寫入（補強）
- [x] 對應 [load-prepay-form.md](../legacy-coverage/load-prepay-form.md) rows 2, 3 ✅
- [x] 含 unit tests（演算法）+ integration test (real DB)
- [ ] 通過 [code-review](../../workflows/code-review.md)

## 風險與未解問題

- **並發兩個 admin 同時 load 同一組**：UPDLOCK 編號分配保證 Number 不重；但兩人都會跑完且 idempotency check 會讓第二人全 skip
- **大批次 transaction 太長**：500+ 筆同 transaction 可能 lock 太久；目前單一 transaction，未來可改 chunked commit
- **PrepayCeremonyCategorys 在源年度已不存在**：query 用 LEFT JOIN 避免崩；Sort=null 視為 0
