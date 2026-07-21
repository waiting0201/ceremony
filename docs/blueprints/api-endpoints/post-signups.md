---
title: POST /api/v1/signups
purpose: 新增報名（含 UPDLOCK 編號分配、SignupType→NumberTitle 推導、SignupLog 同步寫入）
status: shipped
endpoint: post-signups
http_method: POST
route: /api/v1/signups
legacy_form: NewSignupForm.cs
legacy_lines: 151-362
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../../design/database-design.md
  - ../../design/backend-design.md
  - ../legacy-coverage/new-signup-form.md
  - post-believers.md
  - get-signups.md
keywords: [signups, create, post, updlock, number, avoid4, signuplog]
last_updated: 2026-07-21 (地址非必填客訴：mailAddress 由 required 改可空，空白 normalize 為空字串照常寫入；CreateSignupHandler/UpdateSignupHandler/InsertShiftSignupHandler 皆放寬，未選信眾自動建立走的 BelieverWriteValidator 同步放寬；移除「請輸入寄件地址」400 錯誤碼)
---

## 規格

`POST /api/v1/signups`，需要 JWT。

### Request DTO (`CreateSignupRequest`)

```jsonc
{
  "year": 115,                                       // 民國年，required
  "ceremonyCategoryId": "guid",                      // required
  "signupType": 1,                                   // 1=一般 2=寺方 3=觀音會 4=普桌 5=郵撥，required
  "believerId": "guid",                              // required（**API 不再內嵌新建 Believer**，前端先 POST /believers）
  "keepNumber": false,                               // true → 用 customNumber；false → 系統分配
  "customNumber": null,                              // keepNumber=true 時必填
  "fee": 1200,                                       // optional
  "name": "string (required, max 30)",
  "phone": "string? (max 30)",                       // 全→半形 transform
  "hallName": "string? (max 10)",
  "isFixedNumber": false,
  "mailZipcodeId": null,
  "mailAddress": "string? (max 250)",                // 地址非必填（2026-07-21）：空 → 存空字串
  "textZipcodeId": null,
  "textAddress": "string? (max 250)",                // 空時自動 fallback 至 mailAddress
  "livingNames": ["", "", "", "", "", ""],
  "deadNames":   ["", "", "", "", "", ""],
  "remark": "string? (max 250)",
  "prepayYear": null,
  "prepayCeremonyCategoryId": null
}
```

### Response

`201 Created` + `Location: /api/v1/signups/{id}` + 完整 `SignupListItem`。

### 錯誤碼

| HTTP | errorCode | message verbatim | 觸發 |
|---|---|---|---|
| 400 | `VALIDATION_REQUIRED` | `請輸入編號` | keepNumber=true + customNumber 空 |
| 400 | `VALIDATION_REQUIRED` | `請輸入姓名` | name trimmed 為空 |
| 400 | `VALIDATION_INVALID` | `報名類型錯誤` | signupType 不在 1..5 |
| 409 | `SIGNUP_NUMBER_CONFLICT` | `{year} {ceremony} {signupTypeName} 編號重複，請重新確認！` | keepNumber=true + (year, ceremony, type, customNumber) 已存在 |
| 404 | `BELIEVER_NOT_FOUND` | `找不到信眾` | believerId 不存在 |
| 401 | – | – | 無 JWT |

## 舊系統對照（forward）

### 對應 Form / 事件

| 舊方法/事件 | 行 | 對應新版 |
|---|---|---|
| `NewSignupForm.btnConfirm_Click` | 151-362 | `CreateSignupHandler.HandleAsync` |
| `Library.GetSignupNumber` | `Library.cs:20-32` | `Domain.Services.NumberGenerator`（用 SQL UPDLOCK + HOLDLOCK 防 race） |
| `NewSignupForm.GetNumberText` | 736-751 | `Domain.Services.AvoidFourFormatter`（display 用，DB 仍存 int） |
| SignupType → NumberTitle switch | 281-298 | `Domain.Services.NumberTitleResolver`（5 case） |

### 業務邏輯區塊

1. **編號分配（race-safe）**（舊 `Library.cs:20-32` 無 lock）：
   - 舊：`Get().Where(...).OrderByDescending(Number).FirstOrDefault()` — race window 存在
   - 新：`SELECT TOP 1 ISNULL(MAX(Number), 0) + 1 FROM dbo.Signups WITH (UPDLOCK, HOLDLOCK) WHERE Year=@Y AND CeremonyCategoryID=@C AND SignupType=@T` 在交易內執行
   - 接著 `INSERT` + `INSERT SignupLog` + `COMMIT`
   - **行為差異**：新版用 UPDLOCK 防並發重複；舊版實際上仰賴單一桌面 client 不會並發

2. **keepNumber 路徑**（舊 line 155-176）：
   - 用戶手動指定編號 → 先查重 → 重複拋 `SIGNUP_NUMBER_CONFLICT`
   - 訊息含 year + ceremonyTitle + signupTypeName（與舊系統 verbatim 對齊）

3. **NumberTitle 推導**（舊 line 281-298）：
   - SignupType `1→"No", 2→"寺", 3→"觀", 4→"普", 5→"郵"`
   - 應用層強制（DB 無 CHECK constraint）

4. **TextAddress fallback**（舊 line 246-247, 250）：
   - textAddress 空 → 取 mailAddress 為值
   - textZipcodeId 空 + textAddress 空 → 取 mailZipcodeId
   - 此 fallback 用在「印疏文用同寄件地址」場景

5. **SignupLog 同步寫入**（舊 line 309-348）：
   - 與 Signup 同一交易；Admin/Createdate 從 JWT claim + DateTime.UtcNow
   - **快照欄位**：CeremonyCategoryTitle、PrepayCeremonyCategoryTitle（用 join 取 title 字串）

6. **新建 Believer 路徑** ❌ **故意捨棄**（舊 line 188-228）：
   - 舊版允許 form 內聯新建 Believer
   - 新版要求前端先 `POST /api/v1/believers` 取得 `believerId` 再 `POST /api/v1/signups`（cleaner REST，2 步流程）
   - 此差異需在前端 UX 補上（mockup 已對應這 2 步邏輯）

### 邊界 case

| 場景 | 舊行為 | 新版 |
|---|---|---|
| keepNumber=true + 空 number | MessageBox + return | 400 `請輸入編號` |
| keepNumber=true + 重複 | MessageBox 含 year/ceremony/type | 409 同訊息 verbatim |
| 系統分配（auto）| `Library.GetSignupNumber` 取 max+1 | UPDLOCK + max+1 同 |
| 同一秒 2 個 client 同 (year, ceremony, type) | 可能重複 | UPDLOCK 序列化，不會重複 |
| signupType=99 | switch 不 hit → NumberTitle null | 400 `報名類型錯誤` |
| phone 全形 | `Strings.StrConv Narrow` 轉半 | 同 |
| textAddress 空 | 取 mailAddress | 同 |

## 業務規則

- **編號唯一性鍵**：`(Year, CeremonyCategoryID, SignupType, Number)`，DB 無 unique index，service 層 enforce
- **NumberTitle 由 SignupType 推導**，請求 body **不收**該欄位
- **避4 規則**：Display only（前端 `AvoidFourFormatter`）；DB Number 仍為實際序號（如 4, 14, 24）

## 資料存取

```sql
BEGIN TRANSACTION;

-- 編號分配（race-safe）
SELECT @NextNumber = ISNULL(MAX(Number), 0) + 1
FROM dbo.Signups WITH (UPDLOCK, HOLDLOCK)
WHERE Year = @Year AND CeremonyCategoryID = @CategoryId AND SignupType = @SignupType;

INSERT INTO dbo.Signups (SignupID, Year, CeremonyCategoryID, SignupType, BelieverID,
  NumberTitle, Number, Fee, Name, Phone, ...全 24 欄..., AdminID, Createdate)
VALUES (...);

INSERT INTO dbo.SignupLogs (SignupLogID, SignupID, Year, CeremonyCategoryTitle, ...,
  Admin, Createdate)
VALUES (...);

COMMIT;
```

JOIN 取 CeremonyCategoryTitle 與 PrepayCeremonyCategoryTitle 在 Handler 前置完成（兩次 lookup）。

## 驗收

- [x] 規格段所有欄位有 DTO 型別 + 範例
- [x] 6 邊界 case 對齊舊 verbatim
- [x] `Domain.Services.AvoidFourFormatter`、`NumberTitleResolver` 拉到 Domain（純函式可單測）
- [x] UPDLOCK + HOLDLOCK + Transaction：integration test 含並發場景（簡化版）
- [x] SignupLog 同交易插入
- [x] 對應 [new-signup-form.md](../legacy-coverage/new-signup-form.md) rows 6, 14-18, 25 ✅
- [x] 含 unit + integration tests
- [ ] 通過 [code-review](../../workflows/code-review.md)
- [ ] 「inline 新建 Believer」by frontend orchestration ❌ 故意捨棄

## 風險與未解問題

- **並發測試**：integration test 用 sequential 兩次模擬；真正高並發測試 deferred
- **AdminID = 0（後門帳號）**：DB schema `AdminID int NOT NULL` 接受 0；SignupLog.Admin 取 JWT `name` claim（後門時為 "Administrator"）
