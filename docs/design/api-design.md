---
title: API Design
purpose: 法會報名系統重構版的 REST API 契約：路徑、方法、DTO、錯誤碼
applicable_when: 要新增/修改 API endpoint、要設計 DTO、要處理錯誤碼、要對齊前後端契約
related_agents:
  - backend-engineer
  - system-analyst
related_docs:
  - backend-design.md
  - database-design.md
  - frontend-design.md
  - security.md
keywords: [api, REST, endpoint, contract, DTO, error, OpenAPI]
last_updated: 2026-07-03 (新增 GET /reports/tablet/sample dev-only 端點：5 亡者+5 陽上固定樣本，免 signupId；POST /reports/batch 加 signupIds[] 精準勾選列印；加 reports 三個 endpoint 的 dev-only debugOverlay 參數；註記既有 Reports/Print 表格與 Controller 實際落差)
---

## 通則

| 項目 | 規範 |
|---|---|
| Base path | `/api/v1` |
| 認證 | `Authorization: Bearer <JWT>`（除 `/auth/login`） |
| 序列化 | JSON，camelCase 屬性名 |
| 時間格式 | ISO 8601 UTC（前端轉本地） |
| 民國年欄位 | int（不用日期型） |
| GUID | string（標準 UUID 格式） |
| 分頁 | `?page=1&pageSize=50`；回傳含 `total`、`page`、`pageSize`、`items` |
| 排序 | `?sort=year:desc,number:asc` |
| 篩選 | query string 多參數 |
| OpenAPI | Swagger UI 暴露於 `/swagger`（dev only） |

## 實作規範（CRITICAL — 對應 [CLAUDE.md](../../CLAUDE.md) 規則 10）

每個 endpoint 必須走以下流程：

1. **開工前**：到 [../blueprints/api-endpoints/](../blueprints/api-endpoints/) 建一份 `<verb>-<resource>.md`（複製 `_template.md`），填完「舊系統對照」段才能開 code
2. **實作時**：Controller / Service method 必須有 XML doc comment 含 `Legacy: <Form>.cs:line-line` + `Blueprint: ...` + `Coverage: ...`，詳見 [conventions.md](../conventions.md) API 實作約定段
3. **實作後**：到 [../blueprints/legacy-coverage/](../blueprints/legacy-coverage/) 對應 `<form>.md` 把實作的行勾為 ✅

完整索引：
- forward：[../blueprints/api-endpoints/README.md](../blueprints/api-endpoints/README.md)
- reverse：[../blueprints/legacy-coverage/README.md](../blueprints/legacy-coverage/README.md)

## 統一錯誤回應

```json
{
  "errorCode": "SIGNUP_NUMBER_CONFLICT",
  "message": "編號重複，請重新確認！",
  "details": { "year": 115, "ceremonyId": "...", "number": 42 },
  "traceId": "00-...-01"
}
```

HTTP status 映射：

| Status | 用途 |
|---|---|
| 200 | 查詢/更新成功 |
| 201 | 建立成功（含 `Location` header） |
| 204 | 刪除成功 / 無內容 |
| 400 | 驗證失敗（FluentValidation） |
| 401 | 未認證 |
| 403 | 已認證但無權限 |
| 404 | 找不到資源 |
| 409 | 業務衝突（編號重複、刪除受限） |
| 422 | 業務規則違反 |
| 500 | 未預期例外（含 traceId） |

### 業務錯誤碼（舊 MessageBox 文字 → 錯誤碼）

| errorCode | message (verbatim) | HTTP | 觸發 |
|---|---|---|---|
| AUTH_EMPTY_USERNAME | 請輸入帳號！ | 400 | 登入 |
| AUTH_EMPTY_PASSWORD | 請輸入密碼！ | 400 | 登入 |
| AUTH_INVALID_CREDENTIALS | 帳號或密碼錯誤！ | 401 | 登入 |
| ADMIN_DUPLICATE_USERNAME | 帳號重複，請重新確認！ | 409 | 新增/編輯 admin |
| ADMIN_PASSWORD_MISMATCH | 確認密碼輸入錯誤 | 400 | 編輯 admin |
| BELIEVER_NAME_REQUIRED | 請輸入姓名 | 400 | 新增/編輯信眾 |
| BELIEVER_MAIL_ADDRESS_REQUIRED | 請輸入寄件地址 | 400 | 新增信眾 |
| BELIEVER_PHONE_FORMAT | 聯絡電話格式錯誤，請重新確認！ | 400 | 信眾/報名 |
| BELIEVER_HAS_SIGNUPS | {name} 已有報名資料，不能刪除！ | 409 | 刪除信眾 |
| SIGNUP_YEAR_FORMAT | 年份格式錯誤，請重新確認！ | 400 | 報名表單 |
| SIGNUP_YEAR_PAST | 請勿輸入今年以前的年份 | 400 | 新增報名 |
| SIGNUP_NUMBER_FORMAT | 編號格式錯誤，請重新確認！ | 400 | 報名表單 |
| SIGNUP_NUMBER_CONFLICT | {year} {ceremony} {type} 編號重複，請重新確認！ | 409 | 新增/編輯報名 |
| SIGNUP_FEE_FORMAT | 費用格式錯誤，請重新確認！ | 400 | 報名表單 |
| SIGNUP_PREPAY_YEAR_FORMAT | 預繳年份格式錯誤，請重新確認！ | 400 | 預繳 |
| SIGNUP_PREPAY_YEAR_TOO_EARLY | 預繳年份需大於{currentYear}，請重新確認！ | 400 | 預繳 |
| SIGNUP_KEEP_NUMBER_EMPTY | 請輸入編號 | 400 | cbKeepNumber 勾但空 |
| PRINT_RANGE_INVALID | 編號錯誤 | 400 | 批次列印起 > 迄 |
| CATEGORY_HAS_DEPENDENCIES | 已有報名或還有下層法會，無法刪除 | 409 | 刪除法會分類 |
| SEARCH_NO_CRITERIA | 請輸入搜尋條件 | 400 | 信眾搜尋未填 |
| SEARCH_NO_RESULTS | 無資料，請重新搜尋！ | 200（空清單） | – |

## Endpoint 清單

### Auth

| Method | Path | Body | Response | 說明 |
|---|---|---|---|---|
| POST | `/auth/login` | `{username, password}` | `{accessToken, refreshToken, user}` | |
| POST | `/auth/refresh` | `{refreshToken}` | `{accessToken, refreshToken}` | |
| POST | `/auth/logout` | – | 204 | revoke refresh token |
| POST | `/auth/change-password` | `{oldPassword, newPassword}` | 204 | 強制首次登入用 |

### Admins

| Method | Path | 說明 |
|---|---|---|
| GET | `/admins?includeDisabled=false` | 清單 |
| GET | `/admins/{id}` | 單筆 |
| POST | `/admins` | 新增（body: name, username, password） |
| PUT | `/admins/{id}` | 更新（不含 username） |
| PATCH | `/admins/{id}/password` | 重設密碼 |
| DELETE | `/admins/{id}` | 軟刪除（is_enabled=false） |

### Believers

| Method | Path | 說明 |
|---|---|---|
| GET | `/believers/search?name=&phone=&hallName=&livingName=&deadName=&page=&pageSize=` | 搜尋（至少一個欄位；對應舊 BelieverForm 規則） |
| GET | `/believers/{id}` | 單筆 + nav data |
| GET | `/believers/{id}/signups?year=` | 信眾的報名紀錄（含預繳查詢用） |
| POST | `/believers` | 新增 |
| PUT | `/believers/{id}` | 更新 |
| DELETE | `/believers/{id}` | 刪除（受 BELIEVER_HAS_SIGNUPS 限制） |

### Zipcodes

| Method | Path | 說明 | 狀態 |
|---|---|---|---|
| GET | `/zipcodes/cities` | 縣市清單（distinct City，ORDER BY City；對齊舊 LoadCity，未過濾 IsDisplay） | ✅ |
| GET | `/zipcodes?city={city}` | 該縣市的鄉鎮區（item: `{zipcodeId, city, area, zipcode}`，ORDER BY Zipcode）；`city` 空回空陣列 | ✅ |
| GET | `/zipcodes/lookup?zipcode={code}` | 反查 | ⏳ 尚未需要（新增報名表單由區域 item 直接帶 zipcode，不需反查） |

> 城市/區域連動下拉資料源；新增報名表單 [signup-edit-form](../../frontend/src/app/features/signups/signup-edit-form.component.ts) 使用。Blueprint：[get-zipcodes.md](../blueprints/api-endpoints/get-zipcodes.md)。後端唯讀 `ZipcodeRepository`（Dapper），`[Authorize]`。

### Ceremony Categories

| Method | Path | 說明 |
|---|---|---|
| GET | `/categories/tree` | 樹狀結構（兩層） |
| GET | `/categories?parentId=` | 平面查詢（null = 根） |
| POST | `/categories` | 新增 |
| PUT | `/categories/{id}` | 更新 title / sort |
| DELETE | `/categories/{id}` | 刪除（受 CATEGORY_HAS_DEPENDENCIES 限制） |

### Signups

| Method | Path | 說明 |
|---|---|---|
| GET | `/signups/search` | 主搜尋；query 對應舊 PredicateBuilder（year, isScope, ceremonyId, signupType, number, isFixedNumber, key, scopeName, scopeLivingName, scopeDeadName, scopePhone, page, pageSize, sort） |
| GET | `/signups/{id}` | 單筆 + 完整 nav |
| POST | `/signups` | 新增（atomic：含 believer create/update + signup_log） |
| PUT | `/signups/{id}` | 編輯（atomic） |
| DELETE | `/signups/{id}` | 刪除 |
| GET | `/signups/{id}/logs` | 歷程（Createdate DESC） |
| GET | `/signups/{id}/believer-fill-context?year=` | NewSignupForm 自動帶入：含「今年以前最新報名」 |
| GET | `/signups/duplicates?year=&ceremonyCategoryId=&believerId=&excludeSignupId=` | 重複報名警示：某信眾在同一 (year, ceremonyCategoryId) 既有報名（**忽略 signupType**）→ `{items:[{signupId, signupType, numberTitle, number, name}], total}`。查無回空。新版增強，legacy 無此檢查；僅警示不阻擋。Blueprint: [get-signup-duplicates.md](../blueprints/api-endpoints/get-signup-duplicates.md) |
| POST | `/signups/check-number-conflict` | `{year, ceremonyId, signupType, number, excludeSignupId?}` |
| POST | `/signups/next-number` | `{year, ceremonyId, signupType}` → `{next}`（對應 Library.GetSignupNumber） |
| POST | `/signups/export-excel` | body: 同 search query → 回傳 .xlsx 串流 |

### Prepay (載入預繳)

| Method | Path | 說明 |
|---|---|---|
| POST | `/prepay/load` | body: `{sourceYear, sourceCeremonyId, targetYear, targetCeremonyId, believerCategory}`（believerCategory: 1..6 對應六種 case）→ 回傳建立筆數摘要 |
| GET | `/prepay?believerId={id}&year={y}` | 某信眾今年(含)以前最新報名的預繳資訊（新增報名選信眾時自動帶入預繳年/法會）→ `{prepayYear, prepayCeremonyCategoryId, prepayCeremonyCategoryTitle}`，查無回三欄 null。對齊 `NewSignupForm.BelieverSelected:1102-1115`。Blueprint: [get-prepay-believer-latest.md](../blueprints/api-endpoints/get-prepay-believer-latest.md) |
| GET | `/prepay/preview` | 同上 query → 不寫入，僅預覽預期建立的清單（規劃中） |

### Reports / Print

| Method | Path | 說明 |
|---|---|---|
| POST | `/reports/datacard` | body: `{signupIds[]}` → application/pdf |
| POST | `/reports/receipt` | body: `{signupIds[]}` → application/pdf |
| POST | `/reports/tablet` | body: `{signupIds[]}` → application/pdf（合併） |
| POST | `/reports/text` | body: `{signupIds[]}` → application/pdf（含垂直地址 PNG） |
| POST | `/reports/worship` | body: `{signupIds[]}` → application/pdf（限 signupType=4） |
| POST | `/reports/batch` | body: `{reportType, numberStart?, numberEnd?, signupIds?[], year?, yearGte?, ceremonyCategoryId?, signupType?}` → 統一入口（`signupIds` 有值時精準印該幾筆，優先於 `numberStart`/`numberEnd` 編號區間；兩者皆缺回 400 `編號錯誤`） |

每個 endpoint 支援：
- `?format=pdf|preview`（preview 走相同格式但加 watermark「預覽」）
- `?variant=auto|tabletOne|tabletOneOne|...` 強制指定模板變體（auto 走 server 端邏輯）

> ⚠️ **本表與目前 [ReportsController](../../backend/src/Ceremony.Api/Controllers/ReportsController.cs) 實際行為部分落差**（既有落差，非本次任務範圍）：5 個單筆 endpoint 實際是 `GET` + `[FromQuery] signupId`（單筆），不是 `POST` + `body: {signupIds[]}`；`format=preview` / `variant=` 這兩個 query 參數在現有 Controller 中也未實作。**`/reports/batch` 已於 2026-07-03 補上 `signupIds[]`**（見上一列的實際簽章），此列的落差已消除，其餘 5 個單筆 endpoint 落差維持原狀。

**`debugOverlay`（dev-only，2026-07-03 新增）**：`datacard` / `tablet` / `text` 三個 GET endpoint 額外支援 `?debugOverlay=true`，會在產出的 PDF 疊上 `reference/template/` 對應的實體樣板照片，供開發人員檢視列印位置是否對齊。**僅 `ASPNETCORE_ENVIRONMENT=Development` 可用，其他環境回 404**。詳見 [printing-reports.md](../blueprints/printing-reports.md)「開發用列印位置檢視工具」。

**`GET /reports/tablet/sample`（dev-only，2026-07-03 新增）**：免 `signupId`，固定回傳「5 位亡者 + 5 位陽上」的薦牌樣本 PDF（`TabletTemplate.Base` fallback 變體），可搭配 `?debugOverlay=true` 疊樣板照片。同樣僅 `Development` 環境可用，其他環境回 404。

### Backup

| Method | Path | 說明 |
|---|---|---|
| POST | `/backup` | ✅ **已實作**。Request：`{ customFileName?, clearLog? }`（皆選填，`clearLog` 預設 false）。Response：`{ fileName, fullPath, sizeBytes, startedAt, completedAt, logCleared, logBackupFileName?, logClearError? }`；`[Authorize]`。對齊舊 [MainForm.cs:95-113](../../reference/old/Ceremony/MainForm.cs#L95-L113)：檔名 `{yyyyMMddHHmmssffffff}.bak`（6 位微秒、無前綴）；SQL flags `WITH NOFORMAT, NOINIT, NAME = N'Ceremony-Full Database Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10`；DB 名稱由開啟的連線動態取得（`conn.Database`）；目錄 best-effort 建立；`Backup:Directory` 未設 → 500 `BACKUP_NOT_CONFIGURED`。**`clearLog=true`（新功能，非舊系統行為）**：完整備份後依 recovery model 安全清交易紀錄檔 — FULL/BULK_LOGGED 走 `BACKUP LOG`（產 `.trn` 於同目錄）+ `DBCC SHRINKFILE`、SIMPLE 走 `CHECKPOINT`+`SHRINKFILE`；清 log 失敗**不影響備份成功**（回 `logCleared=false`+`logClearError`） |
| GET | `/backup/{fileName}/download` | ✅ **已實作（新功能，非舊系統行為）**。串流下載指定備份檔（`.bak`/`.trn`）供 client 端「另存新檔」；`[Authorize]`、回 `application/octet-stream`（`enableRangeProcessing`）。**用途**：sidecar 模式下 `.bak` 由 DB 主機端 SQL Server 寫，瀏覽器無法選本機路徑 → Electron 殼以原生對話框接收此串流寫到 client 任意位置（見 [electron-packaging.md](../blueprints/electron-packaging.md)）。**檔名 traversal 防護**（`SqlBackupService.IsValidBackupFileName`）：僅允許 `^[0-9A-Za-z._-]+\.(bak\|trn)$` 且不含 `..`；不合法 → 400 `VALIDATION_BACKUP_FILENAME`；找不到 / API process 讀不到該目錄 → 404 `BACKUP_FILE_NOT_FOUND`。**限制**：API process 須讀得到 `Backup:Directory`（prod sidecar 建議 UNC 共用；dev docker MSSQL 容器內路徑 API 讀不到 → 404，屬已知限制）。Blueprint：[get-backup-download.md](../blueprints/api-endpoints/get-backup-download.md) |
| GET | `/backups` | ❌ **尚未實作**（列出既有備份檔）；前端目前不需要 |

## DTO 範例

### CreateSignupRequest

```json
{
  "year": 115,
  "ceremonyCategoryId": "...",
  "signupType": 1,
  "believerId": "...",
  "newBeliever": {
    "name": "王小明",
    "phone": "0912345678",
    "...": "..."
  },
  "fee": 1000,
  "number": null,
  "keepNumber": false,
  "name": "王小明",
  "phone": "0912345678",
  "livingNames": ["陽上1", "陽上2", null, null, null, null],
  "deadNames": [null, null, null, null, null, null],
  "mail": { "zipcodeId": 100, "address": "信義路一段" },
  "text": { "zipcodeId": null, "address": null },
  "remark": "",
  "prepay": { "year": 116, "ceremonyCategoryId": "..." }
}
```

> `believerId` 與 `newBeliever` 互斥：兩者擇一。後端會在 transaction 內處理建立或關聯。

### Search query → response

```http
GET /signups/search?year=115&isScope=true&ceremonyId=...&signupType=-1&key=王&scopeName=true&scopeLivingName=true&page=1&pageSize=50&sort=year:desc,number:asc
```

```json
{
  "page": 1, "pageSize": 50, "total": 237,
  "items": [
    { "id": "...", "year": 115, "ceremonyTitle": "中元", "numberTitle": "No", "number": 42,
      "name": "王小明", "phone": "0912345678",
      "livingNames": ["...", "...", null, null, null, null],
      "deadNames": [null, null, null, null, null, null],
      "mailCity": "台北市", "mailZone": "信義區", "mailAddress": "信義路一段",
      "textCity": null, "textZone": null, "textAddress": null,
      "prepayYear": 116, "prepayCeremonyTitle": "春季",
      "adminName": "tim", "createdAt": "2026-04-01T..."
    }
  ]
}
```

## OpenAPI / Swagger

- 每個 endpoint 加 XML doc comment（被 Swashbuckle 取用）
- `Produces<TResponse>(StatusCode)` annotation 明確列舉
- DTO 屬性加 `[Required]` / `[Range]` 等讓 Swagger 顯示完整契約
- 錯誤碼以 enum 列出在 OpenAPI components

## 版本策略

- 路徑前綴 `v1`；breaking change 升 `v2`
- 過渡期同時供應，至少維持 6 個月

## 速率限制 / Throttling

- 單機桌面 app 無 DoS 風險，但仍設：
  - `/auth/login` 每 IP 每分鐘 10 次（防爆破）
  - `/backup` 每 admin 每小時 5 次（避免誤觸大量備份）
- 其他 endpoint 不限
