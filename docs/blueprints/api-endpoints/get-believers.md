---
title: GET /api/v1/believers
purpose: 信眾搜尋查詢（至少一個搜尋條件；含陽上/往生名單跨欄位 OR 查詢）
status: shipped
endpoint: get-believers
http_method: GET
route: /api/v1/believers
legacy_form: BelieverForm.cs
legacy_lines: 35-44, 353-409
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../../design/api-design.md
  - ../../design/database-design.md
  - ../legacy-coverage/believer-form.md
keywords: [believers, search, query, dapper, livingname, deadname]
last_updated: 2026-05-27
---

## 規格

### Route + Method

`GET` `/api/v1/believers?name=&phone=&hallName=&livingName=&deadName=`

需要 JWT。

### Query parameters

| 名稱 | 型別 | 必填 | 行為 |
|---|---|---|---|
| `name` | string | 0..1 (見下) | LIKE `%name%` 對 `Believers.Name` |
| `phone` | string | 0..1 | LIKE `%phone%` 對 `Believers.Phone` |
| `hallName` | string | 0..1 | LIKE `%hallName%` 對 `Believers.HallName` |
| `livingName` | string | 0..1 | LIKE `%livingName%` 對 6 個 `LivingNameOne..Six` 任一欄（OR） |
| `deadName` | string | 0..1 | LIKE `%deadName%` 對 6 個 `DeadNameOne..Six` 任一欄（OR） |

**至少需給一個非空條件**（沿用舊行為 line 37-41），否則 400。

### Response DTO

```jsonc
// 200 OK
{
  "items": [
    {
      "id": "guid",
      "employeeType": 1,
      "employeeTypeTitle": "非員工",
      "hallName": "...",
      "name": "...",
      "phone": "...",
      "mailZipcodeId": null,
      "mailCity": "",
      "mailArea": "",
      "mailAddress": "...",
      "textZipcodeId": null,
      "textCity": "",
      "textArea": "",
      "textAddress": "...",
      "livingNames": ["", "", "", "", "", ""],
      "deadNames":   ["", "", "", "", "", ""]
    }
  ],
  "total": 1
}
```

無資料時：`{items:[], total:0}`（舊系統的「無資料，請重新搜尋！」訊息改由前端顯示）。

### 錯誤碼

| HTTP | errorCode | message verbatim | 觸發 |
|---|---|---|---|
| 400 | `VALIDATION_REQUIRED` | `請輸入搜尋條件` | 5 個欄位全空 |
| 401 | (空) | – | 無 JWT |

## 舊系統對照（規則 A — forward）

### 對應 Form / 事件

| 舊方法/事件 | 舊 code line | 行為摘要 |
|---|---|---|
| `BelieverForm.btnSearch_Click` | `BelieverForm.cs:35-44` | 5 欄位空檢查 + 呼叫 `LoadBelievers` |
| `BelieverForm.LoadBelievers` | `BelieverForm.cs:353-409` | 動態 IQueryable 串接 5 個條件 + Map 成 ViewModel + 含 EmployeeType 轉中文 + join Zipcodes |

### 業務邏輯區塊

1. **至少一個條件**（舊 line 37-41）：5 欄位全空 → MessageBox「請輸入搜尋條件」+ return
   - 新版：handler 檢查 5 個 trimmed 條件至少 1 個非空，否則拋 `VALIDATION_REQUIRED`
2. **動態 WHERE 串接**（舊 line 356-360）：IQueryable.Where 鏈式
   - 新版：Dapper 動態 SQL（用 StringBuilder + params 集合，避免 ad-hoc 字串拼接）
3. **6 欄位 OR 查詢**（舊 line 359, 360）：LivingNameOne..Six / DeadNameOne..Six 任一含關鍵字
   - 新版：`(LivingNameOne LIKE @ln OR LivingNameTwo LIKE @ln OR ...)` 共 6 個 OR
4. **EmployeeType 轉中文**（舊 line 372）：`1=非員工 / 2=大殿 / 3=地藏殿` 三元式
   - 新版：DTO 帶 `employeeTypeTitle` 計算欄位（Application 層 map）
5. **Join Zipcodes 兩次**（舊 line 377-382）：MailZipcodeID + TextZipcodeID
   - 新版：SQL `LEFT JOIN Zipcodes mz ON ... LEFT JOIN Zipcodes tz ON ...`
6. **無資料訊息**（舊 line 407）：「無資料，請重新搜尋！」MessageBox
   - 新版：API 回 `items:[]`；前端 mockup 已有 verbatim 文字（[visual-design.md](../../design/visual-design.md)）

### 邊界 case

| 場景 | 舊行為 | 新版行為 | 對應測試 |
|---|---|---|---|
| 5 條件全空 | 400 +「請輸入搜尋條件」 | 同 | TestSearch_NoCriteria |
| 只給 name | LIKE 查詢 | 同 | TestSearch_ByName |
| 只給 livingName | 6 欄 OR | 同 | TestSearch_ByLivingName (DB) |
| 完全無命中 | MessageBox + 空 grid | `items:[], total:0` | TestSearch_NoResults |
| 條件含特殊字元 `%` `_` | LINQ Contains 自動 escape | Dapper 參數化 + 用 `LIKE @p ESCAPE '\\'` 或 explicit escape | TestSearch_SqlInjectionSafe |

## 業務規則

- **電話、姓名、堂號搜尋是 substring**（用 LIKE `%x%`），不是 exact match — 沿用舊 LINQ `Contains`
- **陽上 / 往生跨 6 欄位 OR 查詢**：對應業務需求「找信眾的某個家人」

## 資料存取

### 預期 SQL（動態組合）

```sql
SELECT TOP 200
  b.BelieverID, b.EmployeeType, b.HallName, b.Name, b.Phone,
  b.MailZipcodeID, mz.City AS MailCity, mz.Area AS MailArea, b.MailAddress,
  b.TextZipcodeID, tz.City AS TextCity, tz.Area AS TextArea, b.TextAddress,
  b.LivingNameOne, b.LivingNameTwo, b.LivingNameThree, b.LivingNameFour, b.LivingNameFive, b.LivingNameSix,
  b.DeadNameOne,   b.DeadNameTwo,   b.DeadNameThree,   b.DeadNameFour,   b.DeadNameFive,   b.DeadNameSix
FROM dbo.Believers b
LEFT JOIN dbo.Zipcodes mz ON mz.ZipcodeID = b.MailZipcodeID
LEFT JOIN dbo.Zipcodes tz ON tz.ZipcodeID = b.TextZipcodeID
WHERE 1=1
  [AND b.Name LIKE @Name]
  [AND b.Phone LIKE @Phone]
  [AND b.HallName LIKE @HallName]
  [AND (b.LivingNameOne LIKE @LivingName OR ... OR b.LivingNameSix LIKE @LivingName)]
  [AND (b.DeadNameOne   LIKE @DeadName   OR ... OR b.DeadNameSix   LIKE @DeadName)]
ORDER BY b.Name
```

`TOP 200` 為安全上限（前端 mockup 預設一頁 50，未來加分頁）。

### Repository

`IBelieverRepository.SearchAsync(BelieverSearchQuery query, CancellationToken)` → `IReadOnlyList<BelieverListItem>`

## 驗收標準

- [x] 規格段所有欄位有 DTO 型別 + 範例
- [x] 舊系統對照表逐行對到舊 code line ref
- [x] 「請輸入搜尋條件」訊息 verbatim
- [x] 對應 `legacy-coverage/believer-form.md` row 2, 13 已勾選 ✅
- [x] 含 unit tests (SearchBelieversHandlerTests)
- [x] 含 integration tests（401 / 400 / 200）
- [x] SQL 參數化（無 SQL injection 風險）
- [ ] 通過 [code-review](../../workflows/code-review.md)
- [ ] 分頁（`?page=&pageSize=`）— 後續任務

## 參考

- 舊 Form：`reference/old/Ceremony/BelieverForm.cs:35-44, 353-409`
- Legacy coverage：[../legacy-coverage/believer-form.md](../legacy-coverage/believer-form.md)
