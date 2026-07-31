---
title: Electron 列印通道
purpose: 讓「按列印」在每台機器都印出同樣結果——紙張、邊界、縮放由程式指定，不再交給 PDF 檢視器與驅動
status: implemented
applicable_when: 要修改列印流程、要加新報表的送印路徑、實機列印對不準要查根因時
related_agents:
  - frontend-architect
  - backend-engineer
related_docs:
  - printing-reports.md
  - printing-reports-positions.md
  - ../design/infrastructure.md
  - ../design/security.md
  - ../design/frontend-design.md
  - ../design/api-design.md
  - ../gotchas.md
keywords: [列印, 印表機, 紙張, pageSize, 縮放, scaleFactor, silent print, plugins, PDF viewer, X-Report-Page-Size, print-settings.json]
last_updated: 2026-07-31 (同日稍晚：大量列印改前端分段後，決策 3「main 自己抓 PDF」的 printReport / printBatchJob 整條移除，printPdfBuffer 成為唯一送印通道；資料流圖改為 plan-first 分流；預覽門檻只剩 PREVIEW_MAX_BYTES。同日先前追加決策 6「預覽內建在列印對話框」含大檔門檻表與 one-shot/TTL 分析、決策 7「送印錯誤用 UserFacingError」含 blob 錯誤 body 解析；資料流圖依取檔者重畫；移除 ceremony:printReport IPC；待驗證補 3 項)
---

## 背景與動機

**客訴（2026-07-31）**：「舊系統按列印資料卡不用手動調列印設定；新系統有的印表機可以、有的要手動調、有的卻讀不到印表機。」

三個症狀是同一個根因的三種表現：**新系統把列印整個外包出去了**。

`frontend/src/app/shared/util/pdf.ts` 只做 `window.open(blobUrl, '_blank', 'noopener')` 就結束，之後紙張、縮放、印表機全由使用者機器上碰巧開啟這個 PDF 的軟體決定。

| 症狀 | 真因 |
|---|---|
| 有的印表機可以 | 該機驅動剛好有 A5 橫式或舊系統留下的「資料卡」自訂紙張，檢視器自動選中 |
| 有的要手動調 | 檢視器預設 A4 +「符合頁面大小」，21×14.8cm 被縮到 A4 → 使用者要手動改紙張與「實際大小」 |
| 有的讀不到印表機 | `main.ts` 的 `webPreferences` 沒有 `plugins: true`（Electron 預設 false）→ Chromium 內建 PDF viewer 未啟用 → `window.open(blob:)` 變成下載、`<iframe src="blob:...pdf">` 是空白，使用者看到的就是「叫不出印表機」 |

### 舊系統為什麼「不用調」——不是設定得準，是它自動拉滿

`reference/old/Ceremony/SignupForm.cs:1737-1762`：

```csharp
Rectangle adjustedRect = new Rectangle(0, 0, ev.PageBounds.Width, ev.PageBounds.Height);
ev.Graphics.FillRectangle(Brushes.White, adjustedRect);
ev.Graphics.DrawImage(pageImage, adjustedRect);   // 非等比拉伸到實體頁面
```

RDLC 先 render 成**點陣圖**，再 `DrawImage` 到 `ev.PageBounds`（整張紙，不是可列印區）。驅動裡是什麼紙都無所謂，內容一律拉滿——這才是「舊系統不用調設定」的真相。

它另外會依**紙張名稱**去驅動裡撈自訂 form（`:1768-1781` 比對 `ps.PaperName == "資料卡"`），並把 `PaperSize` / `Margins(0,0,0,0)` 塞進 `PrintDocument.DefaultPageSettings` 與 `PrinterSettings.DefaultPageSettings` 才跳 `PrintDialog`。

⚠️ **舊寫死的 fallback 尺寸與新系統對不上**（舊系統靠拉伸吃掉了，1:1 送印後會變成真實裁切／位移）：

| 報表 | 舊 `PaperSize`（1/100 吋） | 換算 | 新 QuestPDF 頁面 | 差 |
|---|---|---|---|---|
| 資料卡 | 794 × 560 | 201.7 × 142.2mm | 210 × 148mm | −8.3 / −5.8mm |
| 收據 | 827 × 1170 | 210.1 × 297.2mm | 210 × 297mm | 一致 |
| 薦牌 | 453 × 1000 | 115.1 × 254.0mm | 115 × 255mm | +1.0mm（2026-07-05 實測修正） |
| 文牒 | 1370 × 990 | 348.0 × 251.5mm | 365 × 262mm | +17 / +10.5mm |
| 普桌 | 827 × 1165 | 210.1 × 295.9mm | 210 × 296mm | 一致 |

→ 現場若已有舊的「資料卡」「文牒」自訂 form，**尺寸對新系統是錯的，要重建**。見 [../design/infrastructure.md](../design/infrastructure.md)「現場印表機自訂紙張設定」。

## 決策

### 1. 自建 app 內列印對話框 + `silent: true`（不是系統對話框）

使用者原本要的是「跳系統對話框但預設值已帶好」，**用純 Electron 在 Windows 上做不到**：

- `webContents.print({ silent: false })` 走 `PrintViewManagerBase::ScriptedPrint` → `PrintingContextWin::AskUserForSettings` → 原生 `PrintDlgEx`。建立 `PRINTDLGEX` 時 `hDevMode` / `hDevNames` 為 null → Windows 用**系統預設印表機 + 驅動預設 DEVMODE** 當初值；使用者按確定後回傳的 DEVMODE 會**覆寫**先前設定。JS 傳入的 `pageSize` / `deviceName` 在這條路徑上沒有注入點。
- Electron build 不含 Chrome 的 print preview WebUI（`enable_print_preview = false`），所以沒有第三條路。
- 官方型別註解也只對 `silent: true` 保證「Electron will pick … the default settings for printing」。

→ 自己畫對話框（選印表機／份數／縮放，紙張唯讀顯示），按下去用 `silent: true` 把完整設定送出。功能等價，且參數 100% 受控。代價：外觀不是 Windows 原生，沒有「印表機內容／進階」按鈕。

### 2. 預設縮放 = 100% 實際大小

PDF 頁面尺寸就是實體紙張尺寸，1:1 才對得上 [printing-reports-positions.md](printing-reports-positions.md) 那套 ±0.05cm 的座標。

仍提供「符合紙張」切換（等價於舊系統的拉伸行為）作為退路，每種報表可各自記住。

| 模式 | 送出的 print options |
|---|---|
| `actual`（預設） | `pageSize`（微米，來自 header）+ `margins.marginType: 'none'` + `scaleFactor: 100` |
| `fit` | 不給 `pageSize`（用驅動預設紙張）+ `margins.marginType: 'printableArea'`，由 Chromium 縮到符合 |

`fit` 不是「等比縮到 pageSize」——Electron 沒有暴露 `fitToPage`，所以只能靠不指定紙張讓 Chromium 走預設縮放。

### 3. PDF 由主行程自己抓，不走 IPC 傳 buffer

批次 PDF 可達數百 MB（實測 19018 筆會爆 2GB），structured clone 會在 main 再複製一份 → renderer + main 雙份記憶體。改用 `net.request` 串流落檔（`api-stream.ts`，與 `download.ts` 的 `.bak` 另存共用）。

`net.request` 是主行程 HTTP client，不是瀏覽器 fetch → **不受 CORS 限制**，一定讀得到 `X-Report-Page-Size`。

例外：報表預覽頁的 blob 已在 renderer 手上（且 job 已被取檔消耗），該條路徑才走 `printPdfBuffer(Uint8Array)`。

### 4. 紙張尺寸 single source of truth 在後端

6 種尺寸原本散在各 renderer 的 `private const`，而且**漂移過**（薦牌 25.4 → 25.5）。純 TS 常數表不同步的失敗模式是**靜默印歪**，不會報錯。

- 權威：`backend/src/Ceremony.Domain/Reports/ReportPageSizes.cs`
- 鎖住：`ReportPageSizeConsistencyTests` 斷言表 == 每個 renderer 的 `internal const PageWidthCm/PageHeightCm`（各 renderer 的 `page.Size(...)` 一律改走這兩個 const）
- 傳遞：7 個回 PDF 的 endpoint 掛 `X-Report-Page-Size`（微米，如 `210000x148000`）
- Fallback：`frontend/electron/paper.ts` 的 `REPORT_PAGE_MICRONS`，header 缺失時使用並 log 警告

### 5. 列印設定另存 `print-settings.json`，不放 `config.json`

`main.ts` 的 bootstrap 每次啟動都用 `default-config.json` 種子覆寫 `config.json`（只保留 `jwtKey`），塞進去會被吃掉。而且印表機是**每台機器**的屬性，與「連線權威由出廠種子決定」的語意衝突。

### 6. 預覽內建在列印對話框，不另開視窗（2026-07-31 追加）

**客訴**：「列印會出現操作失敗請稍後再試，而且也沒有預覽列印。」

`silent: true` 是決策 1 的必然代價——按下去就直接進 spooler，使用者印壞了才知道。舊系統有 `PrintPreviewDialog`，新系統只剩 `/reports/preview` 那頁，而它要手貼 signupId GUID，實務上等於沒有。

所以預覽做進既有的列印對話框（左 iframe、右設定），而不是另開視窗：
- 另開 `BrowserWindow` 顯示 PDF 沒辦法在同一個視窗放「列印/取消」按鈕（`webContents.print` 會把工具列一起印進去），還要處理兩個視窗的狀態同步。
- 主視窗已有 `plugins: true`，renderer 的 `<iframe src="blob:…pdf">` 直接能渲染，與 `/reports/preview` 用的是同一套機制。
- iframe src 接 `#toolbar=0`：Chromium 內建 PDF viewer 的工具列自帶列印鈕，按下去會**繞過整條通道**（紙張 / 縮放全失效）。

**代價**：預覽需要 bytes 在 renderer，所以 PDF 改由 renderer 取檔、再經 IPC 把 bytes 送回 main，
違反了決策 3「大檔不經 IPC」的原則。

這個矛盾在同日稍晚被
[大量列印分段](chunked-batch-printing.md) 一併解掉：批次切成 200 筆一段之後，
**每次送印最多一段（≈27 MB），IPC 傳 bytes 已無成本問題**，`printPdfBuffer` 因此成為
唯一的送印通道，決策 3 的「main 自己抓」路徑（`printReport` / `printBatchJob`）整條移除。

現在的預覽規則只剩一條保險絲：blob > 64 MB（`PREVIEW_MAX_BYTES`）時略過 iframe 渲染，
顯示「檔案較大，略過預覽」，bytes 仍在手上照樣送印。

**附帶好處**：每段在 job `completed` 當下就取檔，job 立刻被消耗 →
對話框開多久都不怕後端 10 分鐘 TTL。

### 7. 送印錯誤用 `UserFacingError`，不用原生 `Error`（2026-07-31 追加）

「操作失敗，請稍後再試」的真正來源是 `toMessage()` —— 這份三行函式在 13 個 feature 各複製了一份，只認 `ApiError`，其餘一律蓋掉。列印通道丟的是原生 `Error`，於是主行程回的每一句話都被抹掉：ENOENT、「列印逾時（印表機無回應）」、「尚未連線」、sidecar 的「找不到報名」。

- `toMessage` 收斂到 `core/errors/to-message.ts`，同時認 `ApiError` 與新的 `UserFacingError` marker class。
- 不改成無條件透出 `Error.message`：TypeError / ChunkLoadError 丟到 UI 只會製造客訴。要透出就明確標記。
- 不偽造 `new ApiError(0, 'PRINT_FAILED', …)`：status / errorCode 是假的，會汙染日後依 errorCode 分支的程式。

同時修掉 blob endpoint 的錯誤訊息：`responseType:'blob'` 時 `HttpErrorResponse.error` 是 `Blob`，`ApiError.fromHttp` 的 `errorCode` 判斷失效 → 中文訊息永遠出不來。新增 `ApiError.fromHttpAsync`（`Blob → text → JSON`），interceptor 只有在 `err.error instanceof Blob` 時才走非同步分支。

## 資料流

```
UI（右鍵選單 / 批次區 / 新增後列印 / 報表預覽頁）
  → PrintService（core/print）          ← 唯一入口
      │
      ├─ 單筆：ReportApi.single() 取 blob + pageSizeHeader
      │
      └─ 批次：POST /reports/batch/plan 取清單 → 依 total 分流
             ├─ ≤ 200 筆：BatchPrintService 單一 job（ProgressOverlay）
             └─ > 200 筆：ChunkedPrintService 逐段（分段面板，可暫停／單段重印）
                          見 chunked-batch-printing.md
      │
      → PrintDialogService.ask({ previewBlob, ... })   ← 內建 PDF 預覽 iframe
           │      Electron 額外帶 listPrinters + getPrintSettings 的預設值
           │      瀏覽器走 mode:'preview-only'（無印表機欄位，主鈕＝在新分頁開啟）
           │      分段模式只在第 1 段問一次，其餘段沿用
           │
           ├─ 非 Electron → openPdfInNewTab(blob)
           │
           └─ Electron → ceremony:printPdfBuffer(type, bytes, choice, pageSizeHeader)
                            ← 唯一送印通道；每次最多一段（≈27 MB）
                          → main 寫 temp 檔 → printResolved

     printResolved → resolvePageSize（header 優先，fallback 表次之）
       → printPdfFile：隱藏視窗（plugins:true）載入 file://x.pdf
           → did-finish-load → 等 PDF 子 frame 掛上 → +250ms
           → webContents.print({ silent:true, deviceName, pageSize, margins, scaleFactor })
           → callback 才關窗 + 刪 temp（提早關窗會殺掉列印）
```

## 檔案

| 層 | 檔案 | 職責 |
|---|---|---|
| Domain | `Ceremony.Domain/Reports/ReportPageSizes.cs` | 6 種紙張尺寸權威表 + 微米換算 |
| Api | `Controllers/ReportsController.cs` | `AppendPageSize()` 掛 `X-Report-Page-Size` |
| Api | `Program.cs` | `WithExposedHeaders` 加 `X-Report-Page-Size` |
| Electron | `electron/paper.ts` | fallback 尺寸表 + header 解析（純函式，可測） |
| Electron | `electron/api-stream.ts` | `streamApiToFile`（`download.ts` 共用）；**負責補齊目的目錄** |
| Electron | `electron/print-config.ts` | `print-settings.json` 讀寫 |
| Electron | `electron/print.ts` | `printPdfBuffer` / `printPdfFile` / `listPrinters` / `sweepTempDir` |
| Electron | `electron/main.ts` | `plugins: true`、`setWindowOpenHandler`、4 個列印 IPC、temp 清理 |
| Renderer | `core/print/print.service.ts` | 唯一列印入口；plan-first 分流、`PREVIEW_MAX_BYTES` |
| Renderer | `shared/print-dialog/` | 自建列印對話框（含 PDF 預覽；object URL 生命週期在 service） |
| Renderer | `core/reports/batch-print.service.ts` | 單一 job 的進度 overlay 流程（≤ `SEGMENT_SIZE`） |
| Renderer | `core/reports/chunked-print.service.ts` | 大量列印分段狀態機（見 [chunked-batch-printing.md](chunked-batch-printing.md)） |
| Renderer | `core/errors/to-message.ts` | `toMessage` 單一實作 + `UserFacingError` marker |
| Renderer | `core/http/api-error.ts` | `fromHttpAsync`：blob 錯誤 body 的 JSON 解析 |

**已移除**：`ceremony:printReport` 與 `ceremony:printBatchJob` 兩條 IPC，以及
`electron/print.ts` 的 `printReport` / `printBatchJob`。分段之後每次送印最多一段（≈27 MB），
「main 自己去 sidecar 串流取檔」不再有存在理由，留著就是永遠不會被執行的死碼。
`electron/api-stream.ts` 保留——備份下載仍在用。

## 不做什麼

- **靜默直印（完全不跳對話框）**：使用者要能看到印表機與紙張再按確定。設定已記住，第二次之後只是按 Enter。
- **依名稱指定驅動自訂 form**：Electron 的 `pageSize` 只有 `{width, height}`、沒有 `vendor_id`，做不到。只能靠尺寸命中，所以現場仍需建自訂紙張。
- **雲端列印 / HTML 印表機驅動**：沿用 [printing-reports.md](printing-reports.md) 的既有範圍決定。

## 待驗證（Windows 實機，macOS 開發環境驗不了）

1. `plugins: true` 隱藏視窗 + `silent: true` 能否穩定印出；若印白紙 → 改成畫面外顯示（`setPosition(-20000,-20000)` + `showInactive()`）而非 `show: false`。
2. 自訂 `pageSize`（微米）在「驅動有建 form」與「沒建 form」兩台機器分別印出**用尺量**。若某些機器（特別是薦牌用的窄長紙印表機）完全不吃自訂尺寸 → 退路是 bundle 一支 .NET WinForms helper，1:1 移植舊系統的「依名稱查 form → PrintDialog → 送印」。
3. 橫式尺寸（文牒 365×262mm）Chromium 會不會自己轉直向 → 決定要不要補 `landscape`。
4. **對照組**：同一份 PDF 用「現行檢視器路徑 / 新通道 actual / 新通道 fit」各印一張疊起來比。歷次對位客訴是在檢視器的預設縮放下驗收的，改 1:1 有機會讓已驗收的座標再次跑掉——這是唯一能判定的方法。
5. 資料卡與文牒的**實體紙張真實尺寸**要量一張現場的預印紙（見上方尺寸差異表），決定是重建驅動 form 還是改 renderer 常數（後者要重跑對位驗收）。
6. **`#toolbar=0` 是否真的必要**：Chromium 內建 PDF viewer 的工具列列印鈕在 Electron（`enable_print_preview=false`）內按下去的實際行為未定義。若實機上該鈕本來就不出現或無作用，可以拿掉以換回捲軸與頁碼顯示。
7. **`PREVIEW_MAX_SIGNUPS = 200` 的門檻要不要調**：量 200 筆左右的批次「取檔 → IPC 傳 bytes → 寫 temp」的實際延遲與記憶體峰值。太慢就調小、綽綽有餘就調大（使用者當然希望愈多筆愈能預覽）。
8. **大批次（≥ 5000 筆）走 `printBatchJob` 路徑的記憶體曲線**：確認略過預覽的分支真的沒把 PDF 帶進 renderer。
