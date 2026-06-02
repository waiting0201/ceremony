---
title: GET /zipcodes/cities + GET /zipcodes?city=
purpose: 城市清單與某城市的鄉鎮區清單，供新增報名 / 信眾維護的「城市→區域」連動下拉
status: shipped
endpoint: get-zipcodes
http_method: GET
route: /api/v1/zipcodes/cities, /api/v1/zipcodes?city={city}
legacy_form: NewSignupForm.cs
legacy_lines: 406-460, 662-677
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../../design/api-design.md
  - ../../design/database-design.md
  - ../legacy-coverage/new-signup-form.md
keywords: [zipcodes, 郵遞區號, 城市, 區域, cascading, 連動下拉, 地址]
last_updated: 2026-05-29
---

## 規格

### Route + Method

- `GET` `/api/v1/zipcodes/cities` — 縣市清單
- `GET` `/api/v1/zipcodes?city={city}` — 某縣市的鄉鎮區

兩者皆 `[Authorize]`。

### Request

- `/cities`：無參數
- `/?city=`：query `city`（string）。空白 / 未帶 → 回空陣列（不報錯）

### Response DTO

```jsonc
// GET /zipcodes/cities → 200
{ "items": ["台中市", "台北市", "台東縣", ...], "total": 22 }

// GET /zipcodes?city=台北市 → 200
{
  "items": [
    { "zipcodeId": 1, "city": "台北市", "area": "中正區", "zipcode": "100" },
    { "zipcodeId": 2, "city": "台北市", "area": "大同區", "zipcode": "103" }
  ],
  "total": 12
}
```

> `zipcodeId` 即 `Believers.MailZipcodeID` / `Signups.MailZipcodeID` 的 FK；前端區域下拉 option value = `zipcodeId`，送 `POST/PUT /signups` 的 `mailZipcodeId` / `textZipcodeId`。

### 錯誤碼

無業務錯誤碼（純唯讀查詢）。未授權 → 401（全域）。`city` 空 → 200 空陣列。

## 舊系統對照（規則 A — forward）

### 對應 Form / 事件

| 舊方法/事件 | 舊 code line ref | 行為摘要 |
|---|---|---|
| `NewSignupForm.LoadCity` | `NewSignupForm.cs:662-677` | `zipcodesService.Get().GroupBy(City).OrderBy(Key)` 載入城市下拉 |
| `NewSignupForm.dlMailCity_SelectedIndexChanged` | `NewSignupForm.cs:406-424` | `Where(City==x).OrderBy(Zipcode)` 載入區域下拉（+ 末尾「請選擇區域」哨兵 -1） |
| `NewSignupForm.dlTextCity_SelectedIndexChanged` | `NewSignupForm.cs:441-460` | 同上（文牒地址） |
| `BelieverForm` / `EditSignupForm` 同名事件 | – | 共用同一份城市/區域資料源 |

### 驗證規則對照

| 欄位 | 舊行為 (line) | 新行為 | 差異 / 取捨 |
|---|---|---|---|
| 城市排序 | `GroupBy(City).OrderBy(Key)` (662-677) | `GROUP BY City ORDER BY City` | 等價 |
| 區域排序 | `OrderBy(Zipcode)` (410) | `ORDER BY Zipcode` | 等價 |
| `IsDisplay` 過濾 | 舊未過濾 | 新亦未過濾 | 刻意對齊舊（避免隱藏舊系統可見資料） |
| 「請選擇區域」哨兵 (-1) | UI 端加入 (411-415) | 新版前端以 `<option value="">請選擇區域</option>` 取代 | 等價，不污染 API |

### 業務邏輯區塊

1. **城市去重排序**（舊：`NewSignupForm.cs:662-677`）
   - 新實作：`SELECT City FROM dbo.Zipcodes GROUP BY City ORDER BY City`（`ZipcodeRepository.GetCitiesAsync`）
2. **城市→區域連動**（舊：`NewSignupForm.cs:406-424`）
   - 新實作：`SELECT ZipcodeID, City, Area, Zipcode FROM dbo.Zipcodes WHERE City=@City ORDER BY Zipcode`（`GetByCityAsync`）
   - 前端 `signup-edit-form` `onCityChange` 觸發；選定區域後 `onAreaChange` 顯示 `zipcode`（取代舊 `dlMailZone_SelectedIndexChanged` 自動填 `txtMailZipcode`）

### 邊界 case

| 場景 | 舊行為 (line) | 新版行為 | 對應測試 |
|---|---|---|---|
| 未選城市 | 區域下拉空 | `city` 空 → 200 `{items:[],total:0}` | `Areas_*`（controller 早退） |
| 城市無區域 | 空清單 | 200 空陣列 | – |
| 同寄件地址 | `cbSameMailAddress` 複製 city/zone/zipcode/address (477-502) | 前端 `onSameMailAddressChange` 複製；mail 空 → verbatim「請先輸入寄件地址」 | （前端） |

## 資料存取

### 相關資料表

| Table | 用途 | 索引 | 注意 |
|---|---|---|---|
| `dbo.Zipcodes` | 城市/區域/郵遞區號主檔 | PK `ZipcodeID` | 唯讀；schema 見 [database-design.md §3](../../design/database-design.md) |

### 預期 SQL

```sql
-- cities
SELECT City FROM dbo.Zipcodes GROUP BY City ORDER BY City;
-- areas by city
SELECT ZipcodeID AS ZipcodeId, City, Area, Zipcode
FROM dbo.Zipcodes WHERE City = @City ORDER BY Zipcode;
```

## 驗收標準

- [x] 規格段所有欄位有 DTO 型別 + 範例
- [x] 舊系統對照表已逐行對到舊 code line ref
- [x] 對應的 `legacy-coverage/new-signup-form.md` 行（9/10/11/12/21）已勾選 `✅ 已實作`
- [x] 含 handler 單元測試（`ZipcodeHandlersTests` 5 case）
- [x] 實機 dev DB smoke：cities=22、台北市 areas=12（中正區/100…）
- [ ] 通過 [code-review](../../workflows/code-review.md)
- [ ] 通過 [qa-testing](../../workflows/qa-testing.md)

## 風險與未解問題

- `/zipcodes/lookup?zipcode=` 反查尚未實作（新增報名表單不需要；如未來有「輸入郵遞區號自動帶城市/區域」需求再補）
- `IsDisplay` 欄位目前不過濾（對齊舊）；若業務確認某些區域應隱藏，再加 `WHERE IsDisplay = 1`

## 參考

- 舊 Form：`reference/old/Ceremony/NewSignupForm.cs:406-460, 662-677`
- 後端：`backend/src/Ceremony.Application/Zipcodes/`、`backend/src/Ceremony.Infrastructure/Repositories/ZipcodeRepository.cs`、`backend/src/Ceremony.Api/Controllers/ZipcodesController.cs`
- Legacy coverage：[../legacy-coverage/new-signup-form.md](../legacy-coverage/new-signup-form.md)
