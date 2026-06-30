---
title: GET /api/v1/signups/duplicates
purpose: 重複報名警示——查某信眾在同一 (year, ceremonyCategoryId) 是否已有報名（忽略 signupType），供新增/編輯表單即時提示
status: shipped
endpoint: get-signup-duplicates
http_method: GET
route: /api/v1/signups/duplicates
legacy_form: N/A（新版增強，舊系統不檢查信眾重複報名）
legacy_lines: N/A
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../../design/api-design.md
  - ../../design/backend-design.md
  - ../../design/database-design.md
  - ../legacy-coverage/new-signup-form.md
  - ../legacy-coverage/edit-signup-form.md
keywords: [重複報名, duplicate, 信眾, 年份, 法會, 警示, signups]
last_updated: 2026-06-30
---

## 規格

### Route + Method

`GET` `/api/v1/signups/duplicates?year=&ceremonyCategoryId=&believerId=&excludeSignupId=`

需 JWT（`[Authorize]`）。

### Request（query string）

| 參數 | 型別 | 必填 | 說明 |
|---|---|---|---|
| `year` | int | 是 | 民國年 |
| `ceremonyCategoryId` | guid | 是 | 精確比對的法會分類（root 或子法會皆可；與 Number 唯一性鍵一致） |
| `believerId` | guid | 是 | 信眾 |
| `excludeSignupId` | guid | 否 | 編輯模式排除自己這一筆 |

三個必填鍵任一為空/`Guid.Empty`/`year<=0` → 回空清單（不丟例外）。

### Response DTO

```jsonc
// 200
{
  "items": [
    { "signupId": "…", "signupType": 1, "numberTitle": "No", "number": 12, "name": "王小明" }
  ],
  "total": 1
}
```

### 錯誤碼

無業務錯誤碼。純讀取查詢；查無回 `{ items: [], total: 0 }`。

## 舊系統對照（規則 A — forward）

**N/A（新需求）**。舊 `NewSignupForm` / `EditSignupForm` **完全沒有**信眾層級的重複報名檢查；
舊 `Library.GetSignupNumber`（`reference/old/Ceremony/Commons/Library.cs:20-32`）只做 `(Year, CeremonyCategoryID, SignupType)` 的 `MAX(Number)+1`，不看 BelieverID。

本 endpoint 為新版刻意增強：實務上常見「同一信眾同年同法會被重複建檔」的人為失誤，於輸入階段即時提醒（**僅警示、不阻擋**，使用者仍可儲存）。

## 業務規則

- 判定鍵 = `(Year, CeremonyCategoryID, BelieverID)`，**忽略 SignupType**。
  - 這會把「同信眾同年同法會、不同報名類型」這個**既有合法情境**（見 [glossary.md](../../glossary.md) §1.4、[business-rules-implicit.md](../../business-rules-implicit.md)）也提示出來——這是刻意的（提醒而非禁止）。
- 比對**精確的 CeremonyCategoryID**：若新報名掛季別 root、既有報名掛子法會，視為不同法會、不提示（與 `Number` 唯一性鍵的比對粒度一致）。
- 僅警示，不影響 `SIGNUP_NUMBER_CONFLICT`（409）等既有編號重複行為。

## 資料存取

### 相關資料表

| Table | 用途 | 索引 | 注意 |
|---|---|---|---|
| `dbo.Signups` | 直接查（非 SignupView，只需 5 欄） | – | 過濾 Year + CeremonyCategoryID + BelieverID |

### 預期 SQL

```sql
SELECT SignupID, SignupType, NumberTitle, Number, Name
FROM dbo.Signups
WHERE Year = @Year AND CeremonyCategoryID = @Cat AND BelieverID = @Believer
  AND (@Exclude IS NULL OR SignupID <> @Exclude)
ORDER BY SignupType, Number;
```

### Repository 方法

| 方法 | 檔案 | 行為 |
|---|---|---|
| `ISignupRepository.FindDuplicatesByBelieverAsync` | `Ceremony.Application/Signups/ISignupRepository.cs` | 介面 |
| `SignupRepository.FindDuplicatesByBelieverAsync` | `Ceremony.Infrastructure/Repositories/SignupRepository.cs` | Dapper 實作 |
| `CheckSignupDuplicatesHandler.HandleAsync` | `Ceremony.Application/Signups/CheckSignupDuplicatesHandler.cs` | 鍵齊全檢查 + 包裝 response |

## 前端整合

- API：`SignupApi.checkDuplicates()`（`frontend/src/app/core/api/signups/signup.api.ts`）
- 表單：`SignupEditFormComponent`（`signup-edit-form.component.ts`）以 `combineLatest`（year / ceremonyCategoryId / believerId 三 control，`debounceTime(300)` + `distinctUntilChanged`）即時觸發；編輯模式帶 `excludeSignupId = signupId()`。
- UI：信眾區塊下方 `.alert-warn` 警示，逐筆列「`numberTitle``number` · 報名類型」；`submit()` 不受影響。

## 驗收標準

- [x] 三鍵齊全才查；任一缺回空清單
- [x] 忽略 SignupType
- [x] 編輯模式 `excludeSignupId` 排除自己
- [x] 僅警示，不阻擋儲存、不影響 409 編號衝突
- [ ] 端到端：重複信眾跳警示、換信眾/改年法會警示消失、編輯不含自己

## 參考

- 舊 Form：N/A（無對應）
- 相關：[post-signups.md](post-signups.md)、[put-signup.md](put-signup.md)
