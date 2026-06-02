---
title: POST /api/v1/reports/batch
purpose: 按編號範圍 + 篩選條件批次列印同一類報表，合併成單一 PDF 回傳
status: shipped
endpoint: post-reports-batch
http_method: POST
route: /api/v1/reports/batch
legacy_form: SignupForm.cs
legacy_lines: 447-653,1698-1722
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../../design/api-design.md
  - ../printing-reports.md
  - ../printing-reports-positions.md
  - ../legacy-coverage/signup-form.md
keywords: [reports, batch, print, pdf, merge, range]
last_updated: 2026-05-27
---

## 規格

### Route + Method

`POST /api/v1/reports/batch`

### Request DTO

```jsonc
{
  "reportType": "datacard",       // "datacard" | "receipt" | "tablet" | "text" | "worship"（必填）
  "numberStart": 1,                // int >= 1（必填）
  "numberEnd": 50,                 // int >= numberStart（必填）
  "year": 115,                     // int (民國年)，可選
  "yearGte": false,                // bool，true → year >= Y，false → year == Y；對齊舊 cbIsScope
  "ceremonyCategoryId": "<guid>",  // 可選
  "signupType": 1                  // 1..5；可選
}
```

### Response

- **200 OK** + `Content-Type: application/pdf`
- `Content-Disposition: attachment; filename="batch-<reportType>-<numberStart>-<numberEnd>.pdf"`
- Body：合併後的 PDF binary
- Header `X-Signup-Count: <int>`（合併幾份 signup）

### 錯誤碼

| HTTP | errorCode | message (verbatim) | 觸發條件 |
|---|---|---|---|
| 400 | `VALIDATION_INVALID` | `編號錯誤` | `numberEnd < numberStart`（對齊 SignupForm.cs:454） |
| 400 | `VALIDATION_INVALID` | `報表類型錯誤` | reportType 不在 5 種白名單 |
| 401 | `AUTH_REQUIRED` | – | 無 JWT |
| 404 | `BATCH_NO_SIGNUPS` | `查無符合條件的報名資料` | 篩選後無任何 signup |

## 舊系統對照（規則 A — forward）

### 對應 Form / 事件

| 舊方法/事件 | 舊 code line | 行為摘要 |
|---|---|---|
| `SignupForm.btnPrint_Click` | `SignupForm.cs:447-653` | 從 nudStart/nudEnd + 4 個篩選器組查詢 + switch(printtype) 1..5 對應 5 種報表 + 個別 ViewModel 組合 + Print<Type> 渲染 |
| `SignupForm.CombinePDFs` | `SignupForm.cs:1698-1722` | PdfSharp 開啟每張 PDF 用 `PdfDocumentOpenMode.Import` 逐頁 `AddPage` 合併為單一 byte[] |

### 驗證規則對照

| 欄位 | 舊驗證 (line) | 新驗證 | 差異 |
|---|---|---|---|
| `numberEnd >= numberStart` | `SignupForm.cs:452-456` MessageBox「編號錯誤」| `BatchReportHandler` 預先檢查 → 400 `編號錯誤` | verbatim |
| `year` + `cbIsScope` | `SignupForm.cs:463-472` 雙分支 `>= Y` vs `== Y` | `yearGte` 布林參數對齊 | 等價 |
| `ceremonyCategoryId` 篩選 | `SignupForm.cs:473` | optional GUID | 等價 |
| `signupType` 篩選 | `SignupForm.cs:474` | optional 1..5 | 等價 |

### 業務邏輯區塊

1. **編號範圍 + 條件查詢**（舊：`SignupForm.cs:462-474`）
   - 舊：用 `IQueryable<SignupView>` 串 LINQ Where + OrderBy(Number)
   - 新：`SignupRepository.SearchByNumberRangeAsync` 用 Dapper + DynamicParameters 動態組 WHERE；走既有 `dbo.SignupView`；ORDER BY Number

2. **5 種報表 ViewModel 組合**（舊：`SignupForm.cs:480-647`）
   - 舊：5 case 各自 foreach signups 組 ViewModel
   - 新：對每筆 signup 呼叫對應的 `Generate<Type>Handler.BuildModel`（reuse 既有 SignupReportContext 邏輯：避4、HallName split、Address join、Phone）
   - 共用 `SignupReportContext.Extract / SplitHallName / AddressOf` helper

3. **PDF 合併**（舊：`SignupForm.cs:1698-1722` PdfSharp）
   - 舊：`new PdfDocument(ms)` + `PdfReader.Open(..., Import)` + 逐頁 AddPage
   - 新：`IPdfMerger.Merge(IReadOnlyList<byte[]>)` 用 PdfSharp 6.x（同套件，移植行為）

### 邊界 case

| 場景 | 舊 code 行為 (line) | 新版行為 | 對應測試 |
|---|---|---|---|
| `numberEnd < numberStart` | `:454` MessageBox 並 return | 400 `編號錯誤` | `BatchReportHandlerTests.Invalid_range` + Integration `400_when_range_inverted` |
| reportType=worship + 含 non-type-4 signup | 舊：直接 render，可能對位錯亂 | **僅 render SignupType=4 的列**（防呆，比舊系統嚴格）；若全部被過濾 → 404 `BATCH_NO_SIGNUPS` | `BatchReportHandlerTests.Worship_skips_non_type_4` |
| 篩選後 0 筆 | 舊：產空 PDF 並嘗試列印（崩潰風險）| 404 `BATCH_NO_SIGNUPS` 「查無符合條件的報名資料」| `400_when_no_signups_match` |
| Print Format（PDF / 預覽列印）| 舊有 `CustomDialogForm` 對話 | **故意捨棄**：API 統一回 PDF byte，預覽由前端 PDF.js 處理 | – |

## 業務規則

- 對應 [printing-reports-positions.md](../printing-reports-positions.md)：各報表座標規範
- 對應 [glossary.md](../glossary.md)：「資料卡」「收據」「薦牌」「文牒」「普桌」

## 資料存取

### 相關資料表

| Table / View | 用途 | 注意 |
|---|---|---|
| `dbo.SignupView` | 已 join Believer/Category/Admin 的讀取 view | 等同單筆列印 |

### 預期 SQL

```sql
SELECT * FROM dbo.SignupView
WHERE Number >= @start AND Number <= @end
  /* AND Year = @year  -- yearGte=false */
  /* AND Year >= @year -- yearGte=true */
  /* AND CeremonyCategoryID = @ceremonyId */
  /* AND SignupType = @signupType */
  /* AND SignupType = 4 -- 當 reportType=worship 時強制加 */
ORDER BY Number
```

### Repository 方法（舊系統參照）

| 舊 method | line | 行為 |
|---|---|---|
| `signupviewService.Get().Where(...).OrderBy(...)` | `SignupForm.cs:462-474` | LINQ 串接 |

## 驗收標準

- [x] 規格段所有欄位有 DTO 型別 + 驗證
- [x] 舊系統對照表已逐行對到舊 code line ref
- [x] 錯誤碼與舊 MessageBox 文字 verbatim「編號錯誤」
- [x] 對應的 `legacy-coverage/signup-form.md` rows 16, 33 勾選為 `✅ 已實作`
- [x] 含舊系統行為對照測試（XUnit + 真實 DB integration）
- [ ] 客戶實機列印驗收（同單筆列印，待印表機環境）

## 風險與未解問題

- 大範圍批次（500+ signups）效能：目前 sequential render；後續視需求改 parallel 或 stream
- Worship 自動過濾 non-type-4：與舊系統行為不同（舊系統會崩潰），新版選擇防呆；若有客戶投訴可改為 422 嚴格驗證

## 參考

- 舊 Form：`reference/old/Ceremony/SignupForm.cs:447-653, 1698-1722`
- Legacy coverage：[../legacy-coverage/signup-form.md](../legacy-coverage/signup-form.md) rows 16, 33
- 單筆列印 5 endpoint 已 shipped（datacard / receipt / tablet / text / worship）
