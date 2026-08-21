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
last_updated: 2026-08-21 (新增 **POST /signups/{id}/move-number**「移動插入至…」：把既有一筆移到同群組內的指定編號、中間區段自動 ±1 讓位。起因是現場回報「在此前插入」不適用於移位——用插入做移位只能「先插新的再刪舊的」，中間必留一個空號。三項定案：超出群組現有 MIN..MAX **擋下不 clamp**〔照字面搬會留空號，與需求相反〕、**完全不寫 SignupLog**〔同 insert-shift 順移列的取捨，代價是歷程看不出被移過〕、**只做單筆**。新錯誤碼 `VALIDATION_NUMBER_RANGE`（走 VALIDATION_ 前綴自動對到 400，不需動 middleware）。先前 2026-08-06 (`GET /reports/tablet` 新增 **`debugGrid`** query：薦牌現場對位校正版〔同一筆真實資料 + 1cm 刻度格線，檔名帶 `-calibration`〕。**刻意不做 Development 阻擋**——與 `debugOverlay`／`tablet/sample` 兩個 dev-only 工具相反，它要在客戶的 Windows 機器與實體牌位座上量測，擋在 Development 等於不存在。起因：客訴「四位往生者壓到預印的靈位」，但依現行座標算 3+ 位矩陣下緣最遠只到 13.1946、離量測的「靈」上緣 13.462 還有 0.27cm 餘裕 → 量測基準或送印路徑有問題，需要現場刻度反推。新增 blueprint get-reports-tablet.md。先前 2026-08-02 (**移除 POST /reports/batch 與 POST /reports/batch/plan**：前者是前端早已停用的同步版、後者唯一用途是大量列印分段，而分段已隨列印通道改版整條廢止〔改為合併成一份 PDF + 原生列印對話框的「頁面範圍」續印〕；batch/jobs 成為批次列印唯一入口，/file 改以 DeleteOnClose 串流暫存檔回應。見 blueprints/print-channel-electron.md。先前 2026-07-31 (新增 POST /reports/batch/plan：只解析不渲染、回依 Number 升冪的 {id, number} 清單，供前端切段做大量列印〔單一大 PDF 會爆 PdfSharp 2GB；驗證與錯誤碼與 batch/jobs 共用 ResolveAsync 故逐字相同〕。同日先前錯誤碼表廢除 BELIEVER_MAIL_ADDRESS_REQUIRED〔地址自 2026-07-21 起非必填，2026-07-31 前端信眾表單的 required 一併移除，既有地址可整段清空〕；同日先前 GET /signups 編號由單值 number 改為 numberStart/numberEnd 區間，只給一端＝只查那一筆；POST /reports/batch(+jobs) 編號區間同步改為只需填一端；順手修正 Signups 表誤植的 /signups/search + page/pageSize/sort（實際為 GET /signups 不分頁）。先前 2026-07-31 所有回 PDF 的 endpoint 新增 X-Report-Page-Size response header（微米），供 Electron 列印通道指定 pageSize；CORS WithExposedHeaders 同步加入。先前 2026-07-28 (批次列印改 job 模型：新增 POST /reports/batch/jobs + GET 進度 + GET /file + DELETE 取消 4 支，讓 UI 顯示真實 i/N 百分比並可取消；驗證與查詢仍留在 POST 故錯誤碼/訊息不變；記錄「為何用輪詢而非 SSE」（EventSource 帶不了 Authorization，token 進 query 會被 Serilog 記錄）；新增 3 個 BATCH_JOB_* 錯誤碼；CORS 補 WithExposedHeaders 修正前端讀不到 Content-Disposition/X-Signup-Count 的既有 bug；舊 POST /reports/batch 後端保留、前端停用。先前 2026-07-27 GET /believers 加 searchKey 單一關鍵字參數（14 欄 OR，對齊舊 NewSignupForm txtQ），供新增報名信眾搜尋補「從未報名過的信眾」；順手修正 Believers 表該列寫成 /believers/search?...&page=&pageSize= 的舊路徑，實際為 GET /believers 不分頁；先前 2026-07-18 worship/worshipcard 解鎖：移除 signupType=4 限制與 422 WORSHIP_ONLY_TYPE_4，單筆/批次皆選什麼印什麼，對齊舊系統（客訴右鍵選項被鎖）；先前 2026-07-04 新增 GET /reports/worshipcard 普桌資料卡端點：全新報表、限 signupType=4、支援 dev-only debugOverlay，batch 白名單同步加入；先前：GET /reports/tablet/sample dev-only 端點；POST /reports/batch 加 signupIds[] 精準勾選列印；reports 三個 endpoint 的 dev-only debugOverlay 參數；註記既有 Reports/Print 表格與 Controller 實際落差)))))
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
| ~~BELIEVER_MAIL_ADDRESS_REQUIRED~~ | ~~請輸入寄件地址~~ | – | **已廢除**（2026-07-21 地址改非必填）：空地址照常寫入，不再回 400 |
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
| VALIDATION_NUMBER_RANGE | 目標編號超出範圍（目前 {min}–{max}） | 400 | 移動插入至…（`POST /signups/{id}/move-number`）目標不在該群組現有編號範圍內。用 `VALIDATION_` 前綴才會被 `ExceptionMiddleware` 對到 400（fallback 是 409） |
| PRINT_RANGE_INVALID | 編號錯誤 | 400 | 批次列印起 > 迄，或起迄皆空（只填一端＝只印那一筆，不算錯）。搜尋的編號起迄填反時前端用同一句訊息擋在送出前 |
| CATEGORY_HAS_DEPENDENCIES | 已有報名或還有下層法會，無法刪除 | 409 | 刪除法會分類 |
| SEARCH_NO_CRITERIA | 請輸入搜尋條件 | 400 | 信眾搜尋未填 |
| SEARCH_NO_RESULTS | 無資料，請重新搜尋！ | 200（空清單） | – |
| BATCH_NO_SIGNUPS | 查無符合條件的報名資料 | 404 | 批次列印查無資料 |
| BATCH_JOB_NOT_FOUND | 批次列印工作不存在或已逾期 | 404 | job 未知／TTL 逾期／已取走／已取消／**非本人**（不洩漏是否存在） |
| BATCH_JOB_NOT_READY | 批次列印尚未完成 | 409 | 仍在渲染時取 `/file` |
| BATCH_JOB_LIMIT | 批次列印工作過多，請稍後再試 | 409 | 同一使用者已有 2 個 running job |

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
| GET | `/believers?name=&phone=&hallName=&livingName=&deadName=&searchKey=` | 搜尋（至少一個非空條件，否則 400 `VALIDATION_REQUIRED`；對應舊 BelieverForm 規則）。`searchKey`（2026-07-27 加）為單一關鍵字 OR 比對姓名/電話/陽上 1-6/往生 1-6 共 14 欄，對齊舊 `NewSignupForm` txtQ，供新增報名的信眾搜尋補「從未報名過的信眾」；見 [get-believers.md](../blueprints/api-endpoints/get-believers.md) |
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
| GET | `/signups` | 主搜尋；query 對應舊 PredicateBuilder（year, isScope, ceremonyCategoryId, signupType, **numberStart, numberEnd**, isFixedNumber, searchKey, scopeName, scopeLivingName, scopeDeadName, scopePhone, scopeRemark）。編號為區間，只給一端 → 另一端補同值＝只查那一筆。不分頁。Blueprint: [get-signups.md](../blueprints/api-endpoints/get-signups.md) |
| GET | `/signups/{id}` | 單筆 + 完整 nav |
| POST | `/signups` | 新增（atomic：含 believer create/update + signup_log） |
| POST | `/signups/insert-shift` | 插入報名於指定編號（`customNumber`），同群組 Number ≥ 該編號的既有報名 +1 順移（單一交易 + `sp_getapplock` + `UPDLOCK/HOLDLOCK`）。刻意不做編號重複檢查。列表右鍵「在此前插入」。新版增強，legacy 無。Blueprint: [post-signups-insert-shift.md](../blueprints/api-endpoints/post-signups-insert-shift.md) |
| POST | `/signups/{id}/move-number` | 把既有一筆移到同群組內的 `targetNumber`，中間區段自動 ±1 讓位（上移 `[to, from-1]` +1、下移 `(from, to]` -1；總筆數不變、**不留空號**）。單一交易 + `sp_getapplock`（與 insert-shift／預繳載入共用 resource）+ `UPDLOCK/HOLDLOCK`。目標超出該群組現有 MIN..MAX → 400 擋下（不 clamp）。**不寫任何 SignupLog**。列表右鍵「移動插入至…」。新版增強，legacy 無。Blueprint: [post-signups-move-number.md](../blueprints/api-endpoints/post-signups-move-number.md) |
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
| POST | `/reports/worship` | body: `{signupIds[]}` → application/pdf（不限 signupType，2026-07-18 解鎖） |
| GET | `/reports/worshipcard` | `?signupId=` → application/pdf（普桌資料卡，A5 橫預印卡紙套印；不限 signupType（2026-07-18 解鎖）；支援 dev-only `?debugOverlay=true`）。2026-07-04 新增（全新報表，直接以實際 GET 簽章記載）。Blueprint: [get-reports-worshipcard.md](../blueprints/api-endpoints/get-reports-worshipcard.md) |
| POST | `/reports/batch/jobs` | body: `{reportType, numberStart?, numberEnd?, signupIds?[], year?, yearGte?, ceremonyCategoryId?, signupType?}` → **202** `{jobId, total, fileName, reportType}`。批次列印**唯一**入口（`signupIds` 有值時精準印該幾筆，優先於 `numberStart`/`numberEnd` 編號區間；區間只給一端 → 另一端補同值＝只印那一筆；`signupIds` 空且兩端皆缺才回 400 `編號錯誤`）。驗證與 DB 查詢仍同步執行，只有 render+merge 進背景 |
| GET | `/reports/batch/jobs/{jobId}` | → `{jobId, status, total, completed, fileName, errorCode?, message?}`，`status ∈ running/completed/failed/canceled`。前端每 250ms 輪詢 |
| GET | `/reports/batch/jobs/{jobId}/file` | → application/pdf + `Content-Disposition` + `X-Signup-Count`。**one-shot**：取走即釋放 job。成品是伺服器上的暫存檔，以 `FileOptions.DeleteOnClose` 串流回應（送完或客戶端斷線都會刪） |
| DELETE | `/reports/batch/jobs/{jobId}` | → 204。取消渲染，冪等（未知 id 也回 204，避開「剛好完成」競態） |

**批次列印 job 版（2026-07-28）**：讓 UI 能顯示真實的「第 i / 共 N 筆」百分比進度並中途取消。
Blueprint: [post-reports-batch-jobs.md](../blueprints/api-endpoints/post-reports-batch-jobs.md)。

- **為何驗證留在 POST**：`VALIDATION_INVALID` / `BATCH_NO_SIGNUPS` 的狀態碼與繁中訊息因此與同步版
  **完全相同**（照舊走 `ExceptionMiddleware`），前端錯誤處理零改動；且 `total` 在建立時就是確定值，
  進度條不需要 indeterminate 過渡態。
- **為何是輪詢而非 SSE**：`Program.cs` 是純 JWT Bearer，瀏覽器 `EventSource` 無法帶 `Authorization`
  header；唯一繞法（token 進 query string）會被 `UseSerilogRequestLogging()` 把 JWT 寫進 log
  （見 [security.md](security.md)）。本系統是 localhost 回圈、單一使用者，250ms 輪詢成本可忽略，
  且比「渲染完一筆」（實測約 6.5ms）還密。job 資源模型與 SSE 完全相容，日後可無痛改推送。
- **CORS**：`Content-Disposition`、`X-Signup-Count`、`X-Report-Page-Size` 不在 CORS safelist，已在
  `Program.cs` 加 `WithExposedHeaders`（前兩者是修正一個既有 bug：先前前端讀不到，檔名一直退回 fallback）。
- **`X-Report-Page-Size`**：所有回 PDF 的 endpoint（6 個單筆 + `tablet/sample` +
  `GET /batch/jobs/{id}/file`）都會掛，值為**微米整數** `<寬>x<高>`（如資料卡 `210000x148000`）。
  權威值在 `Ceremony.Domain.Reports.ReportPageSizes`（`ReportPageSizeConsistencyTests` 鎖住它與各 renderer 的
  `PageWidthCm/PageHeightCm` 一致）——**這就是舊系統 `DeviceInfo` 的位置：紙張尺寸在產 PDF 那一刻定案**。
  2026-08-02 起 header **不參與送印**（送印全由 Windows 原生對話框接手），它現在的用途是寫進
  Electron 的診斷紀錄——印歪時第一個要對的就是「PDF 的頁面尺寸」與「驅動裡選的紙」。
  見 [print-channel-electron.md](../blueprints/print-channel-electron.md)。

每個單筆 endpoint 支援：
- `?format=pdf|preview`（preview 走相同格式但加 watermark「預覽」）
- `?variant=auto|tabletOne|tabletOneOne|...` 強制指定模板變體（auto 走 server 端邏輯）

> ⚠️ **本表與目前 [ReportsController](../../backend/src/Ceremony.Api/Controllers/ReportsController.cs) 實際行為部分落差**（既有落差，非本次任務範圍）：5 個單筆 endpoint 實際是 `GET` + `[FromQuery] signupId`（單筆），不是 `POST` + `body: {signupIds[]}`；`format=preview` / `variant=` 這兩個 query 參數在現有 Controller 中也未實作。**批次列印列已於 2026-08-02 更新為 `POST /reports/batch/jobs` 的實際簽章**（同步版 `POST /reports/batch` 與 `POST /reports/batch/plan` 已移除），其餘 5 個單筆 endpoint 落差維持原狀。

**`debugOverlay`（dev-only，2026-07-03 新增）**：`datacard` / `tablet` / `text` / `worshipcard`（2026-07-04 加入）四個 GET endpoint 額外支援 `?debugOverlay=true`，會在產出的 PDF 疊上 `reference/template/` 對應的實體樣板照片，供開發人員檢視列印位置是否對齊。**僅 `ASPNETCORE_ENVIRONMENT=Development` 可用，其他環境回 404**。詳見 [printing-reports.md](../blueprints/printing-reports.md)「開發用列印位置檢視工具」。

**`GET /reports/tablet/sample`（dev-only，2026-07-03 新增）**：免 `signupId`，固定回傳「5 位亡者 + 5 位陽上」的薦牌樣本 PDF（`TabletTemplate.Base` fallback 變體），可搭配 `?debugOverlay=true` 疊樣板照片。同樣僅 `Development` 環境可用，其他環境回 404。

**`GET /reports/tablet?debugGrid=true`（全環境可用，2026-08-06 新增）**：薦牌**現場對位校正版**——同一筆真實資料再疊 1cm 刻度格線，檔名帶 `-calibration` 尾綴。**刻意不做 `Development` 阻擋**（與上面兩項相反）：它是要在客戶的 Windows 機器、實體薦牌紙、實體牌位座上使用的量測工具，擋在 Development 等於這個工具不存在。用途與量測步驟見 [printing-reports.md](../blueprints/printing-reports.md)「現場對位校正版」；Blueprint: [get-reports-tablet.md](../blueprints/api-endpoints/get-reports-tablet.md)。

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
