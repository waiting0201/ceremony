---
title: POST /api/v1/reports/batch/plan
purpose: 解析批次列印範圍，回傳要印的報名清單（不渲染），供前端切成固定大小的段逐段送印
status: shipped
endpoint: post-reports-batch-plan
http_method: POST
route: /api/v1/reports/batch/plan
legacy_form: N/A (新需求)
legacy_lines: N/A
related_agents:
  - backend-engineer
  - frontend-architect
related_docs:
  - ../../design/api-design.md
  - ../../design/performance.md
  - ../chunked-batch-printing.md
  - ../print-channel-electron.md
  - post-reports-batch-jobs.md
keywords: [批次列印, 分段, chunk, plan, 大量列印, signupIds, 續印]
last_updated: 2026-07-31
---

## 規格

### Route + Method

`POST` `/api/v1/reports/batch/plan`（`[Authorize]`）

### Request DTO

與 `POST /reports/batch/jobs` **完全相同**的 `BatchReportRequest` —— 兩者共用
`BatchReportHandler.ResolveAsync`，所以驗證、錯誤碼、選到哪些筆都保證一致。

```jsonc
{
  "reportType": "datacard",   // 必填，6 種之一
  "numberStart": 1,           // 編號區間；只填一端 = 只印那一筆
  "numberEnd": 5000,
  "year": 113,
  "yearGte": false,
  "ceremonyCategoryId": null,
  "signupType": null,
  "signupIds": null           // 有值時優先於編號區間
}
```

### Response DTO

```jsonc
// 200
{
  "reportType": "datacard",
  "fileName": "batch-datacard-1-5000.pdf",
  "total": 5000,
  "items": [                          // 依 Number 升冪，順序即分段順序
    { "id": "2f1c…", "number": 1 },
    { "id": "9a03…", "number": 2 }
    // …
  ]
}
```

`number` 可為 `null`（尚未配號）。前端用它顯示「第 7 段：編號 1201–1400」——
卡紙時使用者要能把段對上手裡那疊紙；整段都沒配號時退回顯示筆數。

5000 筆的回應約 180 KB JSON，屬可接受範圍（同機部署，走 localhost）。

### 錯誤碼

| HTTP | errorCode | message | 觸發條件 |
|---|---|---|---|
| 400 | `VALIDATION_INVALID` | 編號錯誤 | 起迄皆空或迄 < 起 |
| 400 | `VALIDATION_INVALID` | 報表類型錯誤 | reportType 不在 6 種內 |
| 404 | `BATCH_NO_SIGNUPS` | 查無符合條件的報名資料 | 查詢結果 0 筆 |
| 401 | – | – | 無 token |

**與 `batch/jobs` 逐字相同**——這是刻意的：前端先打 plan 再打 jobs，若兩者驗證有差，
使用者會看到「plan 說有 500 筆，jobs 卻說編號錯誤」這種自相矛盾的狀態。

## 為什麼需要這個 endpoint

大量列印不能做成單一 PDF（實測 799 筆 = 107 MB，19018 筆直接爆 PdfSharp 的 2 GB
`MemoryStream`），而且幾千頁的單一 spooler job 中途卡紙就得整批重印。

解法是前端切段逐段送印，但這需要**在建任何 job 之前**就知道「要印哪些、切幾段」：

- `batch/jobs` 的回應雖然有 `total`，但那時 job 已經建了（整批渲染已經開始）。
- 前端不能自己用 `GET /signups` 重查一次——那是另一組查詢語意，算出來的筆數有機會
  與批次條件不同，變成「畫面說 500 筆、實際印出 498 筆」。

所以由後端複用 `ResolveAsync` 回一份權威清單，前端只負責切。詳見
[chunked-batch-printing.md](../chunked-batch-printing.md)。

## 舊系統對照（規則 A — forward）

**N/A (新需求)**。舊系統 `SignupForm.cs:447-653` 的批次列印是同步迴圈：逐筆 render 成點陣圖
後直接 `PrintDocument` 送印，沒有「先產生完整 PDF 再送印」這一步，因此不存在單一大 PDF
的記憶體上限問題，也就沒有分段的概念。

新版改用「後端產 PDF → 前端送印」的架構後才出現這個限制，分段是新架構的補救，
不是舊行為的移植。舊系統同樣沒有「印到一半可續印」的能力（那正是本次要解決的客訴）。

## 資料存取

### 相關資料表

| Table | 用途 | 索引 | 注意 |
|---|---|---|---|
| `dbo.SignupView` | 查要印的報名 | – | 唯讀 |

### 預期 SQL

與 `batch/jobs` 完全相同（同一個 `ResolveAsync`）：`SearchByNumberRangeAsync` 或
`SearchByIdsAsync`，兩者都 `ORDER BY Number`。

⚠️ **`SearchByIdsAsync` 的 `WHERE SignupID IN @Ids` 經 Dapper 展開成 N 個參數，
SQL Server 上限 2100**。這是段大小不能太大的硬理由之一——前端切 200 筆一段，
每段的 job 都遠低於上限。分段之前，勾選 3000 筆列印會直接炸在這裡。

## 驗收標準

- [x] 規格段所有欄位有 DTO 型別 + 範例
- [x] 錯誤碼與 `batch/jobs` 逐字相同（共用 ResolveAsync）
- [x] 舊系統對照已說明為何是 N/A
- [x] 整合測試：401、正常回傳且 `ORDER BY Number`、與 job 版共用驗證
      （`ReportsEndpointsTests.POST_batch_plan_*`，3 個 case）
- [x] 前端 `ReportApi.createBatchPlan` + `PrintService.printBatch` 已接上
