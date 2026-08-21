---
title: POST /api/v1/signups/{id}/move-number
purpose: 把一筆既有報名移動到同群組 (Year, CeremonyCategoryID, SignupType) 內的指定編號，中間區段自動 ±1 讓位（總筆數不變、不留空號）
status: shipped
endpoint: post-signups-move-number
http_method: POST
route: /api/v1/signups/{id}/move-number
legacy_form: N/A（新版增強，舊系統改編號遇已佔用號只會被「編號重複」擋下）
legacy_lines: N/A
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../../design/api-design.md
  - ../../design/backend-design.md
  - ../signup-management.md
  - post-signups-insert-shift.md
  - ../legacy-coverage/new-signup-form.md
keywords: [move, reorder, 移動, 移位, 重排, 順移, 遞補, 編號, signups]
last_updated: 2026-08-21 (新建：現場回報「在此前插入」不適用於移位——用插入做移位得先插新的再刪舊的，中間必留一個空號)
---

## 規格

`POST /api/v1/signups/{id}/move-number`，需 JWT。

### Request DTO

```jsonc
{
  "targetNumber": 2   // 目標編號，必填、> 0，且必須落在該群組現有 MIN..MAX 內
}
```

### Response

`200 OK`，body 為移動後的 `SignupListItem`（與 `GET /signups/{id}` 同形狀）。

### 錯誤碼

| HTTP | errorCode | message | 觸發 |
|---|---|---|---|
| 400 | `VALIDATION_REQUIRED` | `請輸入目標編號` | `targetNumber` 為 null |
| 400 | `VALIDATION_INVALID` | `目標編號必須大於 0` | `targetNumber ≤ 0` |
| 400 | `VALIDATION_INVALID` | `此筆報名沒有編號，無法移動` | 該筆 `Number` 為 null |
| 400 | `VALIDATION_NUMBER_RANGE` | `目標編號超出範圍（目前 {min}–{max}）` | 目標不在該群組現有 MIN..MAX 內 |
| 404 | `SIGNUP_NOT_FOUND` | `找不到報名資料` | id 不存在 |
| 409 | `SIGNUP_BUSY` | `另一筆報名編號作業進行中，請稍後再試` | `sp_getapplock` 逾時 30s |
| 401 | – | – | 無 JWT |

> `VALIDATION_NUMBER_RANGE` 走 `VALIDATION_` 前綴是刻意的：`ExceptionMiddleware.MapStatus` 對
> `_ when errorCode.StartsWith("VALIDATION_")` 已回 400，fallback 才是 409 —— **不需要動 middleware**。

## 行為（移位語意）

「該筆佔據目標編號，中間區段整體讓位」，上移／下移對稱：

| 情境 | 目標 | 讓位範圍 | 結果 |
|---|---|---|---|
| 上移 | `to < from` | `[to, from-1]` 全部 **+1** | 例：1–10 把 5 移到 2 → 原 2,3,4 變 3,4,5，該筆 = 2 |
| 下移 | `to > from` | `(from, to]` 全部 **-1** | 例：1–10 把 2 移到 5 → 原 3,4,5 變 2,3,4，該筆 = 5 |
| 同號 | `to == from` | 無 | no-op（回 200，不報錯） |

在**單一交易**內：

1. `SELECT Year, CeremonyCategoryID, SignupType, Number FROM dbo.Signups WHERE SignupID=@id`
   —— applock 的 resource 名要用到 year/cat/type，**必須先讀才知道鎖誰**。
   讀不到 → `SIGNUP_NOT_FOUND`；`Number` 為 null → `VALIDATION_INVALID`。
   ⚠️ **這一句刻意不加 `UPDLOCK/HOLDLOCK`**，理由見下方「取鎖順序」。
2. `sp_getapplock @Resource='signup-number:{year}:{cat}:{type}'`（`Exclusive` / `Transaction` / 30s）
   —— 與**「在此前插入」和預繳載入共用**同一 resource 命名空間。逾時 → `SIGNUP_BUSY`。
3. 一次群組掃描（`WITH (UPDLOCK, HOLDLOCK)`）同時取回 `MIN(Number)` / `MAX(Number)` /
   `MAX(CASE WHEN SignupID=@id THEN Number END)`（＝鎖內的現號）。
   超出 MIN..MAX → `VALIDATION_NUMBER_RANGE`（**不 clamp**）；現號取不到（該列已被刪）→ `SIGNUP_NOT_FOUND`。
4. 區間讓位：一句 set-based `UPDATE ... SET Number = Number ± 1 WHERE 群組 AND <區間> AND SignupID <> @id`
   （`(Year,Cat,Type,Number)` **無 unique index**，故無中間衝突；`SignupID <> @id` 是防禦——DB 允許重號，
   排除自己才不會被自己的區間掃到）。
5. `UPDATE dbo.Signups SET Number = @target WHERE SignupID = @id`。
6. commit（失敗全 rollback）。

### 取鎖順序（踩過的坑）

`dbo.Signups` 在 `(Year, CeremonyCategoryID, SignupType)` 上**沒有索引**，所以所有配號路徑那句
`SELECT MAX(Number) ... WITH (UPDLOCK, HOLDLOCK) WHERE Year/Cat/Type` 實際是**掃描**、會在整段掃過的列上留 U 鎖。
既有路徑（`InsertWithLogAsync` / `InsertWithShiftAsync` / 預繳載入）一律是「**先掃群組、再動自己的列**」這個順序取鎖。

第一版的 `MoveNumberAsync` 把步驟 1 寫成 `WITH (UPDLOCK, HOLDLOCK)` 的單列讀 —— 變成「先鎖住單列、再去掃描」，
與其他交易**反序** ⇒ 互等死結。症狀是整套測試併行跑時，**預繳載入偶發 500**（6 次全套裡 2 次失敗；
基準線連跑 5 次全綠）。改成「單列讀不上鎖 + 現號改在群組掃描裡一起取」後 6/6 綠。

**動這段程式時請維持這個順序：群組掃描永遠是第一個取鎖的對象。**

**完全不寫 SignupLog**（2026-08-21 使用者定案）：移動那筆與被讓位的列都不 append。理由同「在此前插入」的順移列——
`SignupLogs.Number` 是歷史快照（設計上不隨後續變動），且避免一次移動在大群組灌進上百筆 log。
代價寫在這裡以免日後誤判為 bug：**在「瀏覽歷程」看不出某筆被移過**。

## 與 `POST /signups/insert-shift` 的分工

| | 在此前插入（insert-shift） | 移動插入至…（move-number） |
|---|---|---|
| 用途 | **新增**一筆到指定編號 | **移位**既有的一筆 |
| 總筆數 | +1 | 不變 |
| 影響範圍 | `Number ≥ N` 全部 +1 | 只有起訖之間 ±1 |
| 空號 | 不產生 | 不產生（正是為了修掉「插入＋刪除」留下的空號） |
| 入口 | 右鍵 →「在此前插入」→ 完整報名表單 | 右鍵 →「移動插入至…」→ 只輸入一個數字 |

## 舊系統對照（規則 A — forward）

**N/A（新需求）**。舊 `EditSignupForm` 改編號走的是唯一性檢查（`SignupForm.cs` 的
`編號重複，請重新確認！`），把 5 號改成 2 號會直接被擋；舊系統**沒有**任何「移位並讓中間遞補」的路徑。
現場的變通做法是「用『在此前插入』插一筆新的、再刪掉原本那筆」，中間必然留下一個空號 —— 本 endpoint 就是為此而生。

## 業務規則

- 移動範圍限定同 `(Year, CeremonyCategoryID, SignupType)`，**不跨群組**（跨群組＝換法會/類型，那是編輯報名的事）。
- **刻意不做編號重複檢查**（同 insert-shift）：目標位置本來就被佔用，那正是要讓位的對象。
- 目標超出群組現有 MIN..MAX **擋下、不 clamp**：照字面搬會在中間留一段空號，與「移位不留空號」的需求相反。
- 並發安全靠 `sp_getapplock` + `UPDLOCK/HOLDLOCK` 範圍鎖，不是靠重複檢查。
- 與「固定編號」（`IsFixedNumber`）無關：那面旗只在**預繳載入**時保號，不代表編號永不變（見 [glossary.md](../../glossary.md)）。

## 資料存取 / 元件

| 元件 | 檔案 |
|---|---|
| Handler | `Ceremony.Application/Signups/MoveSignupNumberHandler.cs` |
| Request DTO | `Ceremony.Application/Signups/SignupContracts.cs` → `MoveSignupNumberRequest` |
| Repo 介面/實作 | `ISignupRepository.MoveNumberAsync` / `SignupRepository.MoveNumberAsync` |
| Controller | `SignupsController.MoveNumber`（`[HttpPost("{id:guid}/move-number")]`） |
| applock 共用 | `SignupRepository.InsertWithShiftAsync`、`PrepayRepository.InsertPrepayBatchAsync` |

## 前端整合

- API：`SignupApi.moveNumber(id, targetNumber)`（`frontend/src/app/core/api/signups/signup.api.ts`）
- 入口：報名維護列表**右鍵 →「移動插入至…」**（`signup-list-page.ts` `actionMoveNumber`，icon `move-vertical`）。
  恰好選 1 筆**且該筆有編號**才啟用（否則提示「請先選擇 1 筆有編號的資料」）。
- 對話框：`ConfirmDialogService.askNumber()`（confirm-dialog 擴充 `numberInput`），預填目前編號、值非正整數時確認鈕停用。
- **範圍檢查刻意不在前端做**：列表顯示的是搜尋結果、不等於整個群組的編號範圍，前端自行判斷會誤擋；
  一律由後端在鎖內判定並回帶「目前 N–M」。
- 成功訊息設在 `search()` **之後**：`search()` 開頭會把 `successMessage` 清成 null，先設會被自己蓋掉。

## 驗收標準

- [x] 目標編號必填、> 0（null/≤0 → 400）
- [x] 上移：`[to, from-1]` +1、該筆取 to（真實 MSSQL 整合測試）
- [x] 下移：`(from, to]` -1、該筆取 to
- [x] 同號 → 200 no-op，資料不動
- [x] 超出群組範圍 → 400 `VALIDATION_NUMBER_RANGE`，整筆交易 rollback
- [x] 同年同法會的**其他 SignupType 不受影響**
- [x] 移動後**沒有**新增 SignupLog（被移動那筆仍只有新增當下那一筆）
- [x] 不做編號重複檢查（單元測試 `Times.Never`）
- [x] 前端右鍵啟用條件 + 對話框 + 錯誤訊息透出（vitest）
- [ ] Windows 實機／現場確認（見 [pending-verification.md](../../workflows/pending-verification.md)）

## 參考

- 舊 Form：N/A（無對應）
- 相關：[post-signups-insert-shift.md](post-signups-insert-shift.md)、[put-signup.md](put-signup.md)、[signup-management.md](../signup-management.md)
