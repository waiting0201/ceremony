---
title: POST /api/v1/reports/batch/jobs（含 job lifecycle 共 4 個 endpoint）
purpose: 把批次列印改成背景 job，讓 UI 能顯示真實的「第 i / 共 N 筆」進度並中途取消
status: shipped
endpoint: post-reports-batch-jobs
http_method: POST
route: /api/v1/reports/batch/jobs
legacy_form: SignupForm.cs
legacy_lines: 447-653, 1698-1722
related_agents:
  - backend-engineer
  - frontend-architect
  - qa-test-engineer
related_docs:
  - ../../design/api-design.md
  - ../../design/backend-design.md
  - ../../design/frontend-design.md
  - ../../design/visual-design.md
  - ../../design/performance.md
  - ../../design/security.md
  - ./post-reports-batch.md
  - ../legacy-coverage/signup-form.md
  - ../printing-reports.md
keywords: [批次列印, 進度, progress, overlay, 取消, cancel, job, 輪詢, polling, SSE, PDF]
last_updated: 2026-07-31 (編號區間改為只需填一端；驗證仍共用 BatchReportHandler.ResolveAsync，錯誤碼不變)
---

> **偏離命名規則的說明**：`api-endpoints/README.md` 的規則是「一個 endpoint 一份檔」，但這 4 個
> endpoint 是同一個 job 的生命週期（建立 → 查進度 → 取檔 → 取消），拆開會讓狀態機敘述散在四處。
> 比照 `PUT/DELETE /admins` 併入母 blueprint 的既有先例，這裡用一份涵蓋全部。

## 規格

### 背景與取捨

舊的 `POST /reports/batch`（見 [post-reports-batch.md](./post-reports-batch.md)）是一次阻塞式請求：
後端在 `foreach` 裡逐筆渲染再合併，過程中不回報任何進度。前端唯一的回饋是一個 boolean，
把按鈕文字換成「列印中…」。大批次（實測 508 筆需 ~7 秒、19018 筆會跑數分鐘）時畫面等同凍結，
使用者無從得知還要多久、也無法中止。

**設計原則：驗證與 DB 查詢留在同步的 POST，只有 render + merge 進背景。**
好處是 `VALIDATION_INVALID` / `BATCH_NO_SIGNUPS` 的 HTTP 狀態碼與繁中訊息與同步版**完全相同**
（照舊走 `ExceptionMiddleware`），前端錯誤處理零改動；且 `total` 在 POST 回應時就是確定值，
overlay 不需要 indeterminate 過渡態。DB 查詢是單次 indexed query（ms 級），不會卡住請求。

**為什麼是輪詢而不是 SSE**：`Program.cs` 是純 JWT Bearer 驗證，瀏覽器的 `EventSource`
無法帶 `Authorization` header；唯一常見繞法（token 進 query string）會被 `UseSerilogRequestLogging()`
把 JWT 寫進 log，是安全問題。詳見 [security.md](../../design/security.md)。
本系統是 localhost 回圈、單一使用者，250ms 輪詢的成本可忽略，且比「渲染完一筆」（實測約 6.5ms）
還密，加上 CSS transition 後視覺上與 push 不可區分。**job 資源模型與 SSE 完全相容**，
日後若要改推送，只需新增一個 `GET .../events` action 並改前端一個方法，job store / 取消 / 下載都不動。

### Route + Method

| # | Endpoint | 用途 |
|---|---|---|
| 1 | `POST /api/v1/reports/batch/jobs` | 建立 job，立刻回 jobId 與總筆數 |
| 2 | `GET /api/v1/reports/batch/jobs/{jobId}` | 查進度（前端每 250ms 輪詢） |
| 3 | `GET /api/v1/reports/batch/jobs/{jobId}/file` | 取成品 PDF，取走即釋放（one-shot） |
| 4 | `DELETE /api/v1/reports/batch/jobs/{jobId}` | 取消，冪等 |

全部繼承 `ReportsController` 的 `[Authorize]`。

### Request DTO

`POST /batch/jobs` 的 body 就是既有的 `BatchReportRequest`，**欄位完全不變**：

```jsonc
{
  "reportType": "datacard",   // 必填；datacard|receipt|tablet|text|worship|worshipcard（trim + 小寫）
  "numberStart": 1,           // 編號區間模式：與 numberEnd 至少填一個
  "numberEnd": 400,           // 只給一端時另一端補同值＝只印那一筆（2026-07-31）
  "year": 115,                // 以下皆可選，跟隨呼叫端的搜尋篩選
  "yearGte": false,
  "ceremonyCategoryId": null,
  "signupType": 1,
  "signupIds": null           // 勾選模式；有值時優先於編號區間
}
```

### Response DTO

```jsonc
// 1) POST /batch/jobs → 202 Accepted
{
  "jobId": "f690c225-d89e-4938-9eb0-2f7bf4d02d6f",
  "total": 799,                            // 已確定的總筆數
  "fileName": "batch-datacard-1-400.pdf",
  "reportType": "datacard"
}

// 2) GET /batch/jobs/{jobId} → 200
{
  "jobId": "...",
  "status": "running",                     // running | completed | failed | canceled
  "total": 799,
  "completed": 348,                        // 已渲染完成筆數
  "fileName": "batch-datacard-1-400.pdf",
  "errorCode": null,                       // status=failed 時才有
  "message": null
}

// 3) GET /batch/jobs/{jobId}/file → 200 application/pdf
//    + Content-Disposition: attachment; filename=...
//    + X-Signup-Count: 799
//    + X-Report-Page-Size: 210000x148000   ← 微米；供 Electron 送印指定 pageSize（2026-07-31 新增）
//    （兩個 header 都需要 CORS WithExposedHeaders，見 infrastructure.md）

// 4) DELETE /batch/jobs/{jobId} → 204 No Content
```

### 錯誤碼

`ExceptionMiddleware.MapStatus` **不需修改**，新錯誤碼全部命中既有的 fallback 規則。

| HTTP | errorCode | message (verbatim) | 觸發條件 | MapStatus 命中規則 |
|---|---|---|---|---|
| 400 | `VALIDATION_INVALID` | `編號錯誤` | 缺編號區間或 end < start（POST 階段） | `StartsWith("VALIDATION_")` |
| 400 | `VALIDATION_INVALID` | `報表類型錯誤` | reportType 不在白名單（POST 階段） | 同上 |
| 404 | `BATCH_NO_SIGNUPS` | `查無符合條件的報名資料` | 查無資料（POST 階段，job 不會被建立） | 既有明列 |
| 404 | `BATCH_JOB_NOT_FOUND` | `批次列印工作不存在或已逾期` | 未知 id / TTL 逾期 / 已取走 / 已取消 / **非本人** | `EndsWith("_NOT_FOUND")` |
| 409 | `BATCH_JOB_NOT_READY` | `批次列印尚未完成` | 仍在渲染時呼叫 `/file` | `_ => 409` |
| 409 | `BATCH_JOB_LIMIT` | `批次列印工作過多，請稍後再試` | 同一使用者已有 2 個 running job | `_ => 409` |
| 401 | – | – | 無 / 失效 JWT | controller `[Authorize]` |

失敗的 job 其錯誤碼與訊息放在 status 回應的 JSON 裡（背景例外不經過 `ExceptionMiddleware`），
前端讀 `message` 顯示，與同步版的 UX 一致。渲染時發生非預期例外一律記為
`INTERNAL_ERROR` / `未預期的伺服器錯誤`，並在後端 log 留完整 stack trace。

## 舊系統對照（規則 A — forward）

### 對應 Form / 事件

| 舊方法/事件 | 舊 code line ref | 行為摘要 |
|---|---|---|
| `SignupForm.btnPrint_Click` | `SignupForm.cs:447-653` | 依編號區間 + 篩選逐筆產生報表 |
| `SignupForm.CombinePDFs` | `SignupForm.cs:1698-1722` | PdfSharp 合併成單一 PDF |

### 驗證規則對照

| 欄位 | 舊驗證 (line) | 新驗證 | 差異 / 取捨 |
|---|---|---|---|
| 編號區間 | `SignupForm.cs:447-460`（end < start → MessageBox「編號錯誤」） | `BatchReportHandler.ResolveAsync` 丟 `VALIDATION_INVALID`／`編號錯誤` | 等價，訊息 verbatim |
| 報表類型 | 舊系統由選單決定，無字串驗證 | 白名單 6 種，否則 `報表類型錯誤` | 新增（API 需防非法輸入） |
| 查無資料 | `SignupForm.cs:470` MessageBox「查無符合條件的報名資料」 | `BATCH_NO_SIGNUPS` | 等價，訊息 verbatim |

### 業務邏輯區塊

1. **選取模式判斷 / 查詢 / 檔名**（舊：`SignupForm.cs:447-500`）
   - 舊行為：SignupIds（勾選）優先於編號區間；普桌不另限 SignupType，跟隨呼叫端篩選。
   - 新實作：原封不動搬到 `BatchReportHandler.ResolveAsync`，回 `BatchReportPlan`。
   - 差異：無。這段是同步執行的，錯誤語意與時序都不變。

2. **逐筆渲染 + 合併**（舊：`SignupForm.cs:500-653` + `:1698-1722`）
   - 舊行為：`foreach` 渲染 → `CombinePDFs`。全程阻塞 UI thread，無進度、無法中止。
   - 新實作：`BatchReportComposer.Render`，迴圈內每筆之前 `ct.ThrowIfCancellationRequested()`、
     每筆之後 `onRendered(i+1)` 回報。
   - 差異：**進度回報與取消是新版加值，舊系統無對應**。渲染順序與內容完全相同。

3. **進度回報 / 取消**（舊：N/A，新需求）
   - 舊系統無此能力（WinForms 直接凍結）。
   - 新實作：`BatchPrintJobService` 背景 `Task.Run` + `ConcurrentDictionary` job store。

### 邊界 case

| 場景 | 舊 code 行為 | 新版行為 | 對應測試 |
|---|---|---|---|
| end < start | MessageBox「編號錯誤」 | 400，POST 階段就擋 | `POST_batch_job_invalid_range_returns_400_same_as_sync_version` |
| 查無資料 | MessageBox | 404，**job 不會被建立** | `POST_batch_job_no_signups_returns_404_before_job_is_created` |
| 取消到一半 | N/A | 最多多跑當下這一筆即停，不合併 | `Cancellation_stops_rendering_and_never_merges` |
| 重複取檔 | N/A | 第二次 404（one-shot） | `Batch_job_happy_path_reports_progress_then_serves_pdf_once` |
| 渲染中取檔 | N/A | 409 `BATCH_JOB_NOT_READY` | `TakeFile_while_running_throws_NOT_READY` |
| 他人 job | N/A | 404（不洩漏是否存在） | `Other_owner_cannot_see_the_job` |
| App 關閉 | 隨程序結束 | `Dispose()` 取消所有 running job | `Dispose_cancels_running_jobs` |

## 業務規則

- 沿用 [post-reports-batch.md](./post-reports-batch.md) 的全部業務規則（選取模式、普桌不限型別等），
  本 blueprint 只加上「進度／取消」這層，不改變任何列印內容。

## 資料存取

無新增資料表。job 狀態存在**記憶體**（singleton `ConcurrentDictionary`）。

安全性依據：本系統是 Electron + .NET sidecar 同一個 exe，一台 client 一個 API process、
只服務一個使用者，無反向代理也無多實例（見 [infrastructure.md](../../design/infrastructure.md)），
與既有 `MemoryJwtBlacklist` 是同一組取捨。不用 `IMemoryCache` 是因為這裡需要列舉
（TTL sweep、per-owner 計數）與可變的 job 物件。

**記憶體釋放三層**：
1. `/file` 取走即移除（主力；三個前端呼叫點都只取一次）
2. TTL 10 分鐘 sweep（在 `Start` 與每個 job 收尾時各掃一次，不需要 `BackgroundService`/Timer）
3. 硬上限保留 4 個 job，超過淘汰最舊的已結束 job

查詢沿用既有的 `ISignupRepository.SearchByIdsAsync` / `SearchByNumberRangeAsync`。

## 驗收標準

- [x] 規格段所有欄位有 DTO 型別 + 驗證 + 範例
- [x] 舊系統對照表已逐行對到舊 code line ref
- [x] 錯誤碼與舊 MessageBox 文字 verbatim（沿用同步版）
- [x] `legacy-coverage/signup-form.md` rows 16/33 已更新新版對應
- [x] 單元測試：`BatchReportComposerTests`（4）+ `BatchPrintJobServiceTests`（11）
- [x] 整合測試：`ReportsEndpointsTests` 新增 7 個 job 案例
- [x] 前端測試：`batch-print.service.spec.ts`（5）
- [x] 端對端實測：508 筆 datacard，進度 0%→100% 真實遞增、100% 時顯示「合併 PDF…」、
      完成後 overlay 消失並開新分頁；取消於 40/799 立即生效且不產生 PDF

## 風險與未解問題

- **合併 PDF 的 2 GB 上限**：實測 799 筆 datacard 合併後為 107 MB，
  19018 筆時 PdfSharp 在 `MemoryStream` 觸發 `System.IO.IOException: Stream was too long`。
  這是**既有限制**（舊的同步 endpoint 一樣會爆），job 化只是讓它變成可見的 `INTERNAL_ERROR`。
  目前**不加**每 job 筆數上限（維持與同步版相同行為）；若之後要處理，
  方向是分段輸出多個 PDF 或改用檔案串流合併。見 [performance.md](../../design/performance.md)。
- 同一使用者上限 2 個並行 job 是保守值；單機單使用者情境下實務上不會碰到。

## 參考

- 舊 Form：`reference/old/Ceremony/SignupForm.cs:447-653`、`:1698-1722`
- 同步版 blueprint：[post-reports-batch.md](./post-reports-batch.md)
- Legacy coverage：[../legacy-coverage/signup-form.md](../legacy-coverage/signup-form.md)
- 列印總覽：[../printing-reports.md](../printing-reports.md)
