---
title: Electron 列印通道
purpose: 「按列印」之後的路徑——開列印預覽視窗，送印本身交給 Windows 原生列印對話框（對齊舊系統）
status: implemented
applicable_when: 要修改列印流程、要加新報表的送印路徑、實機列印對不準要查根因時
related_agents:
  - frontend-architect
  - backend-engineer
related_docs:
  - printing-reports.md
  - printing-reports-positions.md
  - chunked-batch-printing.md
  - ../design/infrastructure.md
  - ../design/security.md
  - ../design/frontend-design.md
  - ../design/api-design.md
  - ../design/performance.md
  - ../gotchas.md
keywords: [列印, 印表機, 紙張, PrintDlgEx, 原生對話框, 預覽視窗, PDF viewer, plugins, DeviceInfo, ReportPageSizes, 頁面範圍, 診斷紀錄, 串流取檔, streamApiToFile]
last_updated: 2026-08-02 (**全面改寫**：自建列印對話框 + silent:true 整條移除，改為「開 PDF 檢視器視窗 → 使用者按工具列列印鈕 → Windows 原生 PrintDlgEx」，逐位元對齊舊系統；列印設定（印表機/份數/三軸/記住）整組刪除；大量列印取消分段改回單一合併 PDF，後端合併改串流落檔；PDF 改由主行程 streamApiToFile 取檔，不再經 renderer；順帶修掉「列印完卡在列印中」的 UI 卡死。先前版本見 git 歷史 cc3ac5d 及更早)
---

## 背景與動機

**兩輪客訴，同一個根因**：

| 時間 | 客訴 | 當時的解法 |
|---|---|---|
| 2026-07-31 | 「舊系統按列印不用調設定；新系統有的可以、有的要手動調、有的讀不到印表機」 | 自建列印對話框 + `silent:true` 指定印表機／份數／縮放 |
| 2026-08-01 | 「可以選印表機，但**無法進去印表機裡面的設定**；格式也不對，之前調好的位置都跑掉了」 | 把送印基準攤成 scale/orientation/paper 三個軸讓現場自救 |
| 2026-08-02 | （承上）位置對不對只能靠現場土法對照，且列印完畫面會卡在「列印中」 | **本次：整條拆掉，回歸舊系統形狀** |

根因是**新系統把送印參數攬在自己手上**。自建對話框沒有「印表機內容」按鈕（進不去驅動的紙匣、
進紙方式、自訂紙張），而我們在 macOS 上也無法證明自己組出來的 `webContents.print` 選項
等價於改版前的行為——v2.3.8 那三個軸，本質上是在補一個不該存在的抽象層。

### 舊系統怎麼做的

`reference/old/Ceremony/SignupForm.cs`：

```
LocalReport.Render(…, deviceInfo, …)   → 產出內容
PrintPreviewDialog                      → 預覽視窗（使用者按工具列列印鈕）
PrintDialog                             → Windows 原生（印表機/份數/紙張/方向/頁面範圍）
printDocument.Print()
```

程式只負責產內容與預覽，**送印參數一個都不記**。

### 關鍵結構：排版與紙張尺寸是兩層

舊系統的 RDLC **只是排版**，真正的紙張尺寸由 `lr.Render` 當下的 `DeviceInfo` 決定
（`SignupForm.cs:976` 的註解原文就是「這裡是決定列印的紙張大小，RDLC只是用來排版的」）：

| 舊系統 | 新系統 |
|---|---|
| RDLC 版面元素座標 | QuestPDF 各 renderer 的排版 |
| **DeviceInfo `PageWidth`/`PageHeight`** | **`ReportPageSizes.cs` → `page.Size()`（PDF 的 MediaBox）** |
| `Margins(0,0,0,0)` | QuestPDF 頁面無邊界 |

→ **紙張尺寸在產 PDF 那一刻就定案**，送印端不該再有任何紙張參數。
這就是本次把 `paper:'report'` 軸、`electron/paper.ts` fallback 表、
`X-Report-Page-Size` 的送印用途整組拆掉的理由：它們都是在送印端**二次**指定紙張，
舊系統沒有這個東西。

## 決策

### 1. 開預覽視窗，送印交給 Windows 原生對話框

```
按「列印」
  → 主行程把 PDF 弄到本機暫存檔
  → BrowserWindow({ plugins: true }) 載入該檔 ← Chromium 內建 PDF 檢視器（＝ PrintPreviewDialog）
  → 使用者按工具列的 🖨
  → Electron build 不含 print preview WebUI（enable_print_preview=false）
  → Windows 原生 PrintDlgEx ← **有「內容」按鈕、有「頁面範圍」**
```

程式碼是 `frontend/electron/print.ts` 的 `showViewerWindow()`，約 30 行。
這條路本來就存在（v2.3.8 的診斷區「用 PDF 檢視器列印」），本次只是把它從逃生門升格為主要路徑。

**為什麼不用 `webContents.print({ silent:false })` 直接跳原生對話框**（少一次點擊）：
隱藏視窗當 owner 會讓對話框躲到主視窗後面（看起來像「按了沒反應」），而且 Electron 在
Windows 的實際行為我們驗不了。檢視器那條路是**改版前使用者已經在走的路**，風險最低。

**代價**：多一次點擊（開視窗 → 按工具列列印鈕）。舊系統也是這樣。

### 2. 送印基準 = 完全不參與

`buildPrintOptions`、`print-options.ts`、三個軸、`print-settings.json`、v1→v2 遷移，
**全部刪除**。程式不再有任何「送印選項」的概念。

連帶的好處是**驗收前提自動成立**：`printing-reports-positions.md` 那套 ±0.05cm 的座標，
當初就是在「驅動 DEVMODE + Chromium PDF plugin 的 fit-to-printable-area」下實機驗收的
（v2.3.6 以前的 `window.open('blob:…pdf')` 路徑）。現在的路徑逐位元就是它——
不需要再證明「D ≡ V」，因為 D 就是 V。

⚠️ **與舊系統唯一的結構性差異**：舊系統 `DrawImage(pageImage, ev.PageBounds)` 是
**非等比拉滿整張紙**，所以驅動裡是什麼紙都無所謂；Chromium PDF 檢視器是
**fit-to-printable-area 等比縮放置中**。驅動選 A4 印 21×14.8 的資料卡，舊系統會拉滿、
新系統會縮小置中 → 位置跑掉。
**所以現場每台印表機必須依 `ReportPageSizes` 建正確的自訂紙張 form**，
見 [../design/infrastructure.md](../design/infrastructure.md)。

⚠️ 舊系統留下的自訂 form **尺寸對新系統是錯的**（它靠拉伸吃掉了差異）：

| 報表 | 舊 `PaperSize`（1/100 吋） | 換算 | 新 `ReportPageSizes` | 差 |
|---|---|---|---|---|
| 資料卡 | 794 × 560 | 201.7 × 142.2mm | 210 × 148mm | −8.3 / −5.8mm |
| 收據 | 827 × 1170 | 210.1 × 297.2mm | 210 × 297mm | 一致 |
| 薦牌 | 453 × 1000 | 115.1 × 254.0mm | 115 × 255mm | +1.0mm |
| 文牒 | 1370 × 990 | 348.0 × 251.5mm | 365 × 262mm | +17 / +10.5mm |
| 普桌 | 827 × 1165 | 210.1 × 295.9mm | 210 × 296mm | 一致 |

### 3. PDF 由主行程串流取檔，不經 renderer

`ceremony:openReportInViewer(reportType, apiPath, token)` → `streamApiToFile()`
（`electron/api-stream.ts`，備份下載共用）→ `net.request` 串流落檔 → 開視窗。

- **renderer 完全不碰 PDF bytes**。取消分段之後單一批次可達數百 MB，走 IPC 就是
  renderer + main 各一份 structured clone。
- `net.request` 是主行程 HTTP client，不受 CORS 限制。
- 例外：報表預覽頁的 blob 已在 renderer 手上（job 已被取檔消耗），走
  `ceremony:openPdfInViewer(type, bytes)`。

### 4. 大量列印取消分段，合併成一份

見 [chunked-batch-printing.md](chunked-batch-printing.md)（已標記 superseded）。

分段當初解的是**卡紙續印**（「印到一半卡紙就要整批重印」），不是記憶體。
改走原生對話框後這件事自然被補回來了：**原生列印對話框有「頁面範圍」**——
卡在第 3000 張時預覽視窗還開著，再按一次列印鈕填 `3000-` 即可。
比分段面板更接近舊系統，也更直覺。

刪除：`ChunkedPrintService`、`chunked-print.types.ts`、`shared/batch-print-panel/`、
`POST /reports/batch/plan`。

### 5. 後端合併改串流落檔

取消分段後又變回「一次合併全部」，而原本的 `IPdfMerger.Merge(IReadOnlyList<byte[]>) → byte[]`
有 **2 GB 硬上限**（19018 筆實測丟 `Stream was too long`），加上 `.ToArray()` 會把成品整份再複製。

- `IPdfMerger.Merge(IReadOnlyList<string> srcPaths, string destPath)` — `PdfReader.Open(path)` +
  `Save(FileStream)`；單筆批次直接 `File.Copy`（成品與單筆端點逐位元相同）
- `BatchReportComposer.Render(…, workDir, outputPath, …)` — 逐筆落檔到 `workDir`，
  合併到 `outputPath`，`finally` 一律刪 `workDir`
- `BatchPrintJob.PdfPath`（原 `byte[] Pdf`）；`BatchPrintJobService` 管
  `%TEMP%/ceremony-batch/{jobId}.pdf` 的生命週期，並在啟動時掃掉 1 小時前的殘檔
- `GET batch/jobs/{id}/file` 用 `FileOptions.DeleteOnClose` 串流回應——回應送完（或客戶端斷線）
  檔案自動消失，不需另排清理

⚠️ **這不是常數峰值**：PdfSharp 的 `AddPage` 會把來源頁面複製進目標 document 的物件表，
峰值仍與總頁數相關。實際解掉的是 (a) 2 GB 硬上限 (b) `ToArray()` 的整份複製
(c) job 不再常駐 `byte[]`。5000 筆（≈670 MB）峰值從 ~2 GB 降到 ~700 MB。
真正的常數峰值要換 PDF library 或 append-mode 合併，不在本次範圍。

### 6. 列印設定整組移除

印表機、份數、列印方式／方向／紙張三軸、「記住這台印表機」全部刪除，
`print-settings.json` 不再讀寫（現場的舊檔留著無害）。

理由是決策 2 的必然結果：這些欄位在原生對話框裡都有，而且那裡的值才是真的會生效的值。
留著只會有兩個真相來源。

### 7. 診斷紀錄保留，內容改寫

`%APPDATA%/Ceremony/logs/print-YYYYMMDD.log`，JSON Lines，每次**開預覽視窗**一行：
`reportType` / `via`（stream|buffer）/ `bytes` / `pageSizeHeader` / `signupCount` / `result`。

不再有 `options` / `deviceName`（我們不決定任何送印參數，也不再查印表機清單）。
**印歪時的第一個線索變成 `pageSizeHeader`**（PDF 的實際頁面尺寸）對不對得上驅動裡選的紙。

入口從已刪除的列印對話框搬到**報表預覽頁工具列的「診斷紀錄」按鈕**（`/reports/preview`，
只在桌面版顯示）→ `shell.showItemInFolder()`。

隱私規則不變：不得出現 signupId、姓名、堂號、報表內容、token、temp 完整路徑。
`deviceName` 隨著印表機清單一起消失，這條反而更乾淨了。見 [../design/security.md](../design/security.md)。

### 8. 順帶修掉「列印完卡在列印中」

**現場回報（2026-08-02）**：列印完成後畫面卡在「列印中」，無法再選下一個列印，
要跳到其他功能再回來才恢復。

根因：`signup-list-page.ts` 的 `printing` signal 沒解除——`finally` 一定會 `set(false)`，
所以真正卡住的是 `await bridge.printPdfBuffer(...)` → `webContents.print(options, callback)`
的 **callback 沒回來**（紙已印出＝job 已進 spooler，這在 Windows 上是已知會發生的），
唯一的出口是 10 分鐘的 `PRINT_TIMEOUT_MS`，逾時後還會誤報「列印逾時（印表機無回應）」。

本次改版讓它由架構消失：`openReportInViewer` 在視窗 `loadFile` 完成就 resolve，
UI 不再與 spooler 的 callback 綁在一起。

## 資料流

```
UI（右鍵選單 / 批次區 / 新增後列印 / 報表預覽頁）
  → PrintService（core/print）          ← 唯一入口
      │
      ├─ 非 Electron → 自己抓 blob → openPdfInNewTab（dev 環境）
      │
      ├─ 單筆（Electron）
      │     ceremony:openReportInViewer('/reports/{type}?signupId=…')
      │
      ├─ 批次（Electron）
      │     POST /reports/batch/jobs → ProgressOverlay 輪詢 250ms（只等渲染，不取檔）
      │     → ceremony:openReportInViewer('/reports/batch/jobs/{id}/file')
      │
      └─ 報表預覽頁（blob 已在手上）
            ceremony:openPdfInViewer(type, bytes)

  main：streamApiToFile / 寫 temp 檔
        → BrowserWindow({ plugins:true, parent: mainWindow }).loadFile(pdf)
        → logPrintEvent
        → 使用者按檢視器工具列的 🖨 → Windows 原生 PrintDlgEx
        → 視窗 closed → 刪 temp 檔
```

## 檔案

| 層 | 檔案 | 職責 |
|---|---|---|
| Domain | `Ceremony.Domain/Reports/ReportPageSizes.cs` | **紙張尺寸唯一權威**（＝舊系統的 DeviceInfo）+ 微米換算 |
| Application | `Reports/IPdfMerger.cs` / `BatchReportComposer.cs` | 路徑版合併 + 逐筆落檔 + workDir 生命週期 |
| Application | `Reports/BatchPrintJob(Service).cs` | 成品檔路徑、TTL/上限刪檔、啟動殘檔掃描 |
| Infrastructure | `Reporting/PdfSharpMerger.cs` | `PdfReader.Open(path)` → `Save(FileStream)` |
| Api | `Controllers/ReportsController.cs` | `AppendPageSize()`；`/file` 用 `DeleteOnClose` 串流 |
| Electron | `electron/api-stream.ts` | `streamApiToFile`（備份下載共用） |
| Electron | `electron/print.ts` | `openReportInViewer` / `openPdfInViewer` / `sweepTempDir` |
| Electron | `electron/print-log.ts` | 診斷紀錄（寫入 / 輪替 / 過期清理） |
| Electron | `electron/main.ts` | `plugins: true`、2 個列印 IPC、temp/log 清理 |
| Renderer | `core/print/print.service.ts` | 唯一列印入口（約 110 行，無對話框、無設定） |
| Renderer | `core/reports/batch-print.service.ts` | `render()`（只等渲染）/ `run()`（連成品取回） |
| Renderer | `shared/progress-overlay/` | 批次渲染進度 |
| Renderer | `core/errors/to-message.ts` | `toMessage` + `UserFacingError` marker |

**本次刪除**：
`electron/print-options.ts`、`print-config.ts`、`print-settings-migrate.ts`、`paper.ts`；
IPC `ceremony:listPrinters` / `getPrintSettings` / `savePrintSetting` / `printPdfBuffer`；
`shared/print-dialog/`、`shared/batch-print-panel/`、`core/reports/chunked-print.*`；
後端 `POST /reports/batch`、`POST /reports/batch/plan`、`BatchReportHandler.HandleAsync`。

## 不做什麼

- **`silent:false` 直接跳原生對話框**：隱藏視窗當 owner 會讓對話框躲到主視窗後面；
  檢視器那條路涵蓋同樣需求且是使用者已經走過的路。見決策 1。
- **靜默直印**：使用者要能先看到預覽。
- **依名稱指定驅動自訂 form**：我們不傳任何紙張參數了，這件事完全交給使用者在驅動裡選。
- **記住印表機／份數**：原生對話框自己會記（那是 Windows 的每使用者 DEVMODE），
  我們再記一份只會有兩個真相。
- **後端 append-mode 合併 / 換 PDF library**：見決策 5 的限制說明，等真的撞到再說。

## 待驗證（Windows 實機，macOS 開發環境驗不了）

### ⚠️ 阻斷性：檢視器工具列的列印鈕

**這是整個方案的單一致命未知數**。Electron build 不含 print preview WebUI，
Chromium PDF viewer 工具列列印鈕按下去的實際行為未經實機確認。

1. 任一報名 → 右鍵「列印資料卡」→ 檢視器視窗出現
2. 按工具列的 🖨
3. 檢查：(a) 跳出 Windows 原生列印對話框 (b) 有「內容(R)…」按鈕
   (c) 有「頁面範圍」欄位 (d) 按確定真的印出來

**不通過 → 本方案作廢**，改走 `printui.dll` 路線（`rundll32 printui.dll,PrintUIEntry /e /n "<name>"`
開列印喜好設定；不用 `shell:true`，名稱先與印表機清單做白名單比對）。

### 其他

1. **實體對位**：用真正的預印紙印資料卡／薦牌／文牒各一張，量兩個錨點到 0.05cm，
   與 v2.3.6 的樣張比對 → 應完全一致（同一條路徑）。
   前提是驅動裡已建好對的自訂紙張。
2. **頁面範圍續印**：同一個檢視器視窗再按列印，填 `600-1200`，確認只印後半。
3. **5000 筆**：後端記憶體峰值（工作管理員看 `Ceremony.Api`）、渲染時間、
   `%TEMP%/ceremony-batch/` 有無殘留。
4. **卡住 bug 回歸**：連續列印 5 筆，每次列印完立刻能再列印下一筆，
   批次按鈕不會停在「產生中…」，全程不需切換選單。
5. 關閉檢視器視窗後 `%TEMP%/ceremony-print/` 的檔案有被刪。
6. 檢視器視窗 `parent: mainWindow` 之下，原生列印對話框不會躲到主視窗後面。
