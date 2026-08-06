---
title: GET /api/v1/believers
purpose: 信眾搜尋查詢（至少一個搜尋條件；含陽上/往生名單跨欄位 OR 查詢）
status: shipped
endpoint: get-believers
http_method: GET
route: /api/v1/believers
legacy_form: BelieverForm.cs
legacy_lines: 35-44, 353-409; NewSignupForm.cs:715-722 (searchKey)
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../../design/api-design.md
  - ../../design/database-design.md
  - ../legacy-coverage/believer-form.md
keywords: [believers, search, query, dapper, livingname, deadname, searchKey, 未報名信眾]
last_updated: 2026-08-06 (`phone` 查詢條件先 `ToNarrow` 轉半形再查：寫入端（BelieverWriteValidator / *SignupHandler）一律存半形，條件留全形永遠撈不到；只轉電話、其餘條件的全形字是資料本身。配合前端電話欄 `appNarrowInput`，見 business-rules-implicit §10。先前 2026-07-27 (加 searchKey 單一關鍵字參數：OR 比對 Name/Phone/陽上1-6/往生1-6 共 14 欄，對齊舊 NewSignupForm.cs:715-722 LoadBelievers 的 txtQ；供新增報名信眾搜尋補「從未報名過的信眾」——/signups 只回報名紀錄，等效舊 BelieverView 的 LEFT JOIN))
---

## 規格

### Route + Method

`GET` `/api/v1/believers?name=&phone=&hallName=&livingName=&deadName=&searchKey=`

需要 JWT。

### Query parameters

| 名稱 | 型別 | 必填 | 行為 |
|---|---|---|---|
| `name` | string | 0..1 (見下) | LIKE `%name%` 對 `Believers.Name` |
| `phone` | string | 0..1 | **先 `ToNarrow` 轉半形**再 LIKE `%phone%` 對 `Believers.Phone`（**2026-08-06 加**；寫入端一律存半形，條件留全形永遠撈不到。其他條件不轉，全形是資料本身） |
| `hallName` | string | 0..1 | LIKE `%hallName%` 對 `Believers.HallName` |
| `livingName` | string | 0..1 | LIKE `%livingName%` 對 6 個 `LivingNameOne..Six` 任一欄（OR） |
| `deadName` | string | 0..1 | LIKE `%deadName%` 對 6 個 `DeadNameOne..Six` 任一欄（OR） |
| `searchKey` | string | 0..1 | **（2026-07-27 加）** 單一關鍵字 OR 比對 `Name`/`Phone`/`LivingNameOne..Six`/`DeadNameOne..Six` 共 14 欄；與上列其他條件為 AND |

**至少需給一個非空條件**（沿用舊行為 line 37-41；`searchKey` 單獨給也算數），否則 400。

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
| 400 | `VALIDATION_REQUIRED` | `請輸入搜尋條件` | 6 個參數全空（含 searchKey；全空白字元同視為空） |
| 401 | (空) | – | 無 JWT |

## 舊系統對照（規則 A — forward）

### 對應 Form / 事件

| 舊方法/事件 | 舊 code line | 行為摘要 |
|---|---|---|
| `BelieverForm.btnSearch_Click` | `BelieverForm.cs:35-44` | 5 欄位空檢查 + 呼叫 `LoadBelievers` |
| `BelieverForm.LoadBelievers` | `BelieverForm.cs:353-409` | 動態 IQueryable 串接 5 個條件 + Map 成 ViewModel + 含 EmployeeType 轉中文 + join Zipcodes |
| `NewSignupForm.LoadBelievers` | `NewSignupForm.cs:715-722` | 新增報名的信眾搜尋：**單一 `txtQ`** OR 比對 Name/Phone/陽上 1-6/往生 1-6 共 14 欄，資料源 `BelieverView`（Believers LEFT JOIN Signups，**沒報名過的信眾也在內**，舊 code 註解「如果沒有報名過就查不到」正是把 `SignupView` 換成 `BelieverView` 的原因）→ 新版對應 `searchKey` |

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
6. **單一關鍵字 14 欄 OR（`searchKey`，2026-07-27 加）**：對應舊 `NewSignupForm.cs:715-722`
   - 新版：`AND (b.Name LIKE @SearchKey OR b.Phone LIKE @SearchKey OR b.LivingNameOne..Six LIKE @SearchKey OR b.DeadNameOne..Six LIKE @SearchKey)`
   - 用途：新增報名的信眾搜尋以 `/signups`（每筆報名一列）為主清單，再用本參數補「從未報名過的信眾」列，合起來等效舊 `BelieverView`；不在後端做 JOIN 是為了不新增 view/端點（見 [signup-management.md](../signup-management.md) §信眾搜尋）
7. **無資料訊息**（舊 line 407）：「無資料，請重新搜尋！」MessageBox
   - 新版：API 回 `items:[]`；前端 mockup 已有 verbatim 文字（[visual-design.md](../../design/visual-design.md)）

### 邊界 case

| 場景 | 舊行為 | 新版行為 | 對應測試 |
|---|---|---|---|
| 5 條件全空 | 400 +「請輸入搜尋條件」 | 同 | TestSearch_NoCriteria |
| 只給 name | LIKE 查詢 | 同 | TestSearch_ByName |
| 只給 livingName | 6 欄 OR | 同 | TestSearch_ByLivingName (DB) |
| 完全無命中 | MessageBox + 空 grid | `items:[], total:0` | TestSearch_NoResults |
| 只給 searchKey（未報名過的信眾） | 舊 BelieverView 亦回該列（Signup 欄位 null） | 200，命中該信眾 | `GET_believers_searchKey_finds_believer_with_no_signup_by_name_or_deadName`（Integration） |
| searchKey 全空白 | – | 400 `VALIDATION_REQUIRED` | `SearchKeyWhitespace_only_still_throws_VALIDATION_REQUIRED` |
| searchKey 前後空白 | – | trim 後查詢 | `SearchKeyOnly_passes_validation_and_reaches_repo_trimmed` |
| phone 條件含全形數字/符號 | 舊 grid 直接查，全形撈不到（存的是半形） | `ToNarrow` 後查詢 | `PhoneCriterion_is_converted_to_narrow_before_repo` |
| name 條件含全形字 | LIKE 原樣 | 同（**不轉半形**） | `NameCriterion_keeps_fullwidth_characters` |
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
  [AND (b.Name LIKE @SearchKey OR b.Phone LIKE @SearchKey
     OR b.LivingNameOne..Six LIKE @SearchKey OR b.DeadNameOne..Six LIKE @SearchKey)]
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
