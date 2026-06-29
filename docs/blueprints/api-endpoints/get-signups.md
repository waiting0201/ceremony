---
title: GET /api/v1/signups
purpose: 報名查詢（含動態 AND/OR 條件，對齊舊 PredicateBuilder 邏輯）
status: shipped
endpoint: get-signups
http_method: GET
route: /api/v1/signups
legacy_form: SignupForm.cs
legacy_lines: 807-864
related_agents:
  - backend-engineer
related_docs:
  - ../../design/database-design.md
  - ../legacy-coverage/signup-form.md
  - get-believers.md
keywords: [signups, search, predicatebuilder, signupview]
last_updated: 2026-06-29
---

## 規格

`GET /api/v1/signups?year=&isScope=&ceremonyCategoryId=&signupType=&number=&searchKey=&scopeName=&scopeLivingName=&scopeDeadName=&scopePhone=&scopeRemark=&isFixedNumber=`，需要 JWT。

### Query parameters

| 名稱 | 型別 | 預設 | 行為 |
|---|---|---|---|
| `year` | int? | – | 民國年（>= 100）；空時忽略 |
| `isScope` | bool | false | true → 範圍 (`Year >= year`)；false → 等值 (`Year == year`) |
| `ceremonyCategoryId` | guid? | – | `Guid.Empty` 視為未選 |
| `signupType` | int? | – | 1=一般 2=寺方 3=觀音會 4=普桌 5=郵撥；`-1` 視為全部 |
| `number` | int? | – | `0` 視為未填 |
| `searchKey` | string? | – | 跨欄關鍵字（搭配 scope* 旗標） |
| `scopeName` | bool | false | searchKey LIKE `Name` |
| `scopeLivingName` | bool | false | searchKey LIKE 6 個 `LivingNameOne..Six` (OR) |
| `scopeDeadName` | bool | false | searchKey LIKE 6 個 `DeadNameOne..Six` (OR) |
| `scopePhone` | bool | false | searchKey LIKE `Phone` |
| `scopeRemark` | bool | false | searchKey LIKE `Remark`（新版加入，舊系統無此搜尋欄位） |
| `isFixedNumber` | bool | false | 含「固定編號 = true」OR 條件 |

**OR 群組規則**（沿用舊系統 line 825-830，`scopeRemark` 為新版擴充）：
- 任一 `scope*` 旗標需配合 `searchKey` 非空才生效
- `isFixedNumber=true` 獨立加入 OR 群組
- 若 OR 群組有任何條件 → 整個 OR 群組 AND 進 WHERE；否則略過

### Response DTO

```jsonc
// 200 OK
{
  "items": [
    {
      "id": "guid",
      "year": 115,
      "ceremonyCategoryId": "guid",
      "ceremonyTitle": "梁皇寶懺",
      "signupType": 1,
      "numberTitle": "No",
      "number": 42,
      "fee": 1200,
      "employee": "非員工",
      "name": "...",
      "hallName": "...",
      "phone": "0912345678",
      "isFixedNumber": false,
      "livingNames": ["", "", "", "", "", ""],
      "deadNames":   ["", "", "", "", "", ""],
      "mailCity": "", "mailZone": "", "mailZipcode": "", "mailAddress": "",
      "textCity": "", "textZone": "", "textZipcode": "", "textAddress": "",
      "prepayYear": null,
      "prepayCeremonyCategoryId": null,
      "prepayCeremonyTitle": null,
      "remark": "",
      "adminName": "alice",
      "createDate": "2026-05-27T..."
    }
  ],
  "total": 1
}
```

`TOP 200` 限制（與 `get-believers.md` 一致；未來加分頁）。

### 錯誤碼

| HTTP | 觸發 |
|---|---|
| 401 | 無 JWT |
| 400 | 異常 query 解析（int parse 失敗，自動由 routing 觸發） |

> ❌ **不像舊系統**那樣強制至少一個搜尋條件 — REST `GET /signups` 預設 list semantics。前端可加 UI guard 對齊舊體感。

## 舊系統對照（forward）

| 舊方法/事件 | 行 | 對應新版 |
|---|---|---|
| `SignupForm.btnSearch_Click` | 71-74 | controller action |
| `SignupForm.LoadSearchSignups` | 807-864 | `SearchSignupsHandler` + `SignupRepository.SearchAsync` |
| `PredicateBuilder.New<SignupView>(true)` AND 鏈 | 815-820 | StringBuilder + `WHERE 1=1 AND ...` (Dapper params) |
| `PredicateBuilder.New<SignupView>(false)` OR 鏈 | 822-828 | 動態 OR group `AND (... OR ... OR ...)` |
| `SignupViewService` (既有 DB View) | 832 | 直接 `FROM dbo.SignupView` |
| `OrderBy(Year).ThenBy(CeremonySort).ThenBy(NumberTitle).ThenBy(Number)` | 837 | 同 SQL `ORDER BY` |

### 業務邏輯區塊

1. **使用既有 DB View `SignupView`**（[database-design.md §7](../../design/database-design.md)）：含已 join Believer + CeremonyCategory + Admin 後的欄位（如 `Employee` 字串、`CeremonyTitle`、`AdminName`）
2. **isScope 切換**（line 817-820）：true → `Year >= @Year`；false → `Year == @Year`
3. **OR 群組 short-circuit**（line 833）：若 isOr 標誌為 true 才加 OR clause
4. **LIKE escape**：與 `get-believers.md` 同一 helper（`%` / `_` / `[`）

### 邊界 case

| 場景 | 舊行為 | 新版 |
|---|---|---|
| 全空查詢 | predicateand 為 true (全表) | 同（前端負責加 UI guard） |
| `signupType = -1` | 不加 WHERE | 同 |
| `number = 0` | 不加 WHERE | 同 |
| `ceremonyCategoryId = Guid.Empty` | 不加 WHERE | 同 |
| OR 群組全空（scope* 全 false 且 isFixedNumber=false） | 略過 OR | 同 |
| `isFixedNumber=true` + 任何其他 scope* | OR 兩條件並列 | 同 |

## 資料存取

```sql
SELECT TOP 200
  SignupID, Year, CeremonyTitle, CeremonyCategoryID, SignupType, NumberTitle, Number, Fee,
  Employee, Name, HallName, Phone, IsFixedNumber,
  LivingNameOne..Six, DeadNameOne..Six,
  MailCity, MailZone, MailZipcode, MailAddress,
  TextCity, TextZone, TextZipcode, TextAddress,
  PrepayYear, PrepayCeremonyCategoryID, PrepayCeremonyTitle,
  Remark, AdminName, Createdate
FROM dbo.SignupView
WHERE 1=1
  [AND Year = @Year] | [AND Year >= @Year]
  [AND CeremonyCategoryID = @CeremonyCategoryId]
  [AND SignupType = @SignupType]
  [AND Number = @Number]
  [AND ( ... OR ... )]                  -- 動態 OR 群組
ORDER BY Year, CeremonySort, NumberTitle, Number
```

Repository: `ISignupRepository.SearchAsync(SignupSearchQuery)` → `IReadOnlyList<SignupListItem>`。

## 驗收

- [x] 對應 [signup-form.md](../legacy-coverage/signup-form.md) rows 1, 2, 24 ✅
- [x] AND / OR predicate 邏輯逐條對照舊 line ref
- [x] 含 unit tests（SQL builder 測試）+ integration tests（real SignupView）
- [x] LIKE wildcard escape（與 believers 同 helper 重用）

## 風險與未解

- **SignupView 必須存在於 DB**（既有 schema 一部分）；若 view 失蹤需向客戶確認；目前 (local) DB 已有
- **效能**：50k+ Signups 規模下，無索引 + LIKE %x% 會 full scan；先靠分頁/虛擬滾動緩解，必要時走 DbUp migration 加索引（見 [performance.md](../../design/performance.md)）
