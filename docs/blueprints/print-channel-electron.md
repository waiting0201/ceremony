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
last_updated: 2026-07-31
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

## 資料流

```
UI（右鍵選單 / 批次區 / 新增後列印 / 報表預覽頁）
  → PrintService（core/print）          ← isElectron() 分流
      │
      ├─ 非 Electron（ng serve / 測試）→ openPdfInNewTab(blob)   ※ 行為完全不變
      │
      └─ Electron
           ├─ 批次：BatchPrintService.run(req, { takeFile: false })
           │        （進度 overlay + 取消仍在前端；/file 是 one-shot 不能先取）
           ├─ PrintDialogService.ask(...)  ← listPrinters + getPrintSettings 帶入預設
           └─ window.ceremony.printReport / printBatchJob / printPdfBuffer
                 → main: streamApiToFile → 讀 X-Report-Page-Size
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
| Electron | `electron/api-stream.ts` | `streamApiToFile`（`download.ts` 共用） |
| Electron | `electron/print-config.ts` | `print-settings.json` 讀寫 |
| Electron | `electron/print.ts` | `printReport` / `printBatchJob` / `printPdfBuffer` / `printPdfFile` / `listPrinters` / `sweepTempDir` |
| Electron | `electron/main.ts` | `plugins: true`、`setWindowOpenHandler`、6 個 IPC、temp 清理 |
| Renderer | `core/print/print.service.ts` | 唯一列印入口，Electron / 瀏覽器分流 |
| Renderer | `shared/print-dialog/` | 自建列印對話框 |
| Renderer | `core/reports/batch-print.service.ts` | 新增 `takeFile` 選項（預設 `true`，行為不變） |

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
