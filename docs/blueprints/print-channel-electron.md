---
title: Electron 列印通道
purpose: 「按列印」的送印通道——指定印表機與份數、送印前預覽；紙張/邊界/縮放刻意交回驅動（2026-08-01 起）
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
keywords: [列印, 印表機, 紙張, pageSize, 縮放, scaleFactor, silent print, plugins, PDF viewer, X-Report-Page-Size, print-settings.json, 送印基準, 印表機內容, 診斷紀錄, print-options, DEVMODE]
last_updated: 2026-08-01 (**決策 2 推翻改寫**：預設送印基準改回「什麼都不指定」，並把它攤成 scale/orientation/paper 三個獨立軸供使用者選（預設全 'driver'）——因為無法在開發機證明 driver 等價於改版前、現場印表機各不相同，使用者要能自救；v2.3.7 的「實際大小」= scale:'actual'+paper:'report' 仍可選回來。改回的理由是 printing-reports-positions.md 的座標全是在該基準下驗收的，v2.3.7 改 1:1 導致三種報表同時失準；決策 1 對話框欄位改為印表機/份數/三軸、補 silent:false 在分段列印會每段跳；決策 4 註記 X-Report-Page-Size 只在 paper:'report' 時參與送印，其餘僅供 log 與紙張選項文字；新增決策 8 診斷區「用 PDF 檢視器列印」（＝改版前路徑，有原生「內容」按鈕）＋「開啟診斷紀錄」、決策 9 送印診斷紀錄、決策 10 print-settings v1→v2 就地遷移；新增 Phase 2 printui.dll 印表機內容段；待驗證改為以 V/D/A 三路對照組為主）；先前 2026-07-31 (同日稍晚：大量列印改前端分段後，決策 3「main 自己抓 PDF」的 printReport / printBatchJob 整條移除，printPdfBuffer 成為唯一送印通道；資料流圖改為 plan-first 分流；預覽門檻只剩 PREVIEW_MAX_BYTES。同日先前追加決策 6「預覽內建在列印對話框」含大檔門檻表與 one-shot/TTL 分析、決策 7「送印錯誤用 UserFacingError」含 blob 錯誤 body 解析；資料流圖依取檔者重畫；移除 ceremony:printReport IPC；待驗證補 3 項)
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

- `webContents.print({ silent: false })` 走 `PrintViewManagerBase::ScriptedPrint` → `PrintingContextWin::AskUserForSettings` → 原生 `PrintDlgEx`。建立 `PRINTDLGEX` 時 `hDevMode` / `hDevNames` 為 null → Windows 用**系統預設印表機 + 驅動預設 DEVMODE** 當初值；使用者按確定後回傳的 DEVMODE 會**覆寫**先前設定。JS 傳入的 `deviceName` 在這條路徑上沒有注入點。
- Electron build 不含 Chrome 的 print preview WebUI（`enable_print_preview = false`），所以沒有第三條路。
- 官方型別註解也只對 `silent: true` 保證「Electron will pick … the default settings for printing」。
- 而且大量列印分段（>200 筆）在 `silent:false` 下會**每段跳一次** PrintDlgEx——DEVMODE 無法跨 `print()` 呼叫保留，沒有「跳一次沿用」的選項。

→ 自己畫對話框（印表機／份數／列印方式三軸），按下去用 `silent: true` 送出。
代價：外觀不是 Windows 原生，**沒有「印表機內容／進階」按鈕** → 由決策 8 的診斷區補回。

### 2. 送印基準 =「什麼都不指定」（2026-08-01 改寫）

> **這條決策在 2026-08-01 被推翻並改寫過。** 原本是「預設縮放 = 100% 實際大小」，
> 送印時帶 `pageSize`（微米）+ `margins:'none'` + `scaleFactor:100`，並提供 `fit` 切換。
> 那個決策造成 v2.3.7 的客訴，見下方「為什麼推翻」。

**預設基準是「什麼都不指定」**（`frontend/electron/print-options.ts`）：

```ts
{ silent: true, printBackground: true, copies, deviceName? }   // 三軸皆 driver 時就這些
```

紙張、邊界、縮放、方向一律**不傳**，全部交回印表機驅動的 DEVMODE。

對話框把它攤成**三個獨立的軸**，預設全部是「印表機預設」：

| 軸 | 選項 | 送出的 print options |
|---|---|---|
| 列印方式 `scale` | `driver`（預設）／`actual`／`fit` | 無／`margins:'none'`+`scaleFactor:100`／`margins:'printableArea'` |
| 方向 `orientation` | `driver`（預設）／`portrait`／`landscape` | 無／`landscape:false`／`landscape:true` |
| 紙張 `paper` | `driver`（預設）／`report` | 無／`pageSize`（微米，來自 header） |

v2.3.7 的「實際大小」= `{ scale:'actual', paper:'report' }`，使用者仍可手動選回來。

**為什麼仍然給選**（2026-08-01 使用者定案）：我們無法在 macOS 上證明 `driver` 等價於改版前，
而現場的印表機、驅動、自訂紙張各不相同。把三個軸攤開來，任何一台機器需要別的組合時使用者能
自救——而不是把全部風險押在一個沒驗證過的假設上。每種報表各自記住自己的選擇。

三個軸刻意**互相獨立**（不是一個「模式」下拉）：`landscape` 與 `pageSize` 對驅動的作用不同，
綁在一起會做出使用者選不到的組合（例如「紙張用驅動的，但方向要橫」）。

#### 為什麼推翻（客訴 2026-08-01）

**客訴**：「可以選印表機，但無法進去印表機裡面的設定；直接列印格式也不對，
跟之前我們調好的位置都跑掉了。」資料卡、薦牌、文牒**全部**都不對。

「之前」的基準是 `9264a23` 以前的路徑：`window.open('blob:…pdf')` → Chromium 內建 PDF 檢視器
→ 使用者按檢視器工具列的列印鈕 → 落到原生 PrintDlgEx → 驅動當前紙張 + PDF plugin 的
fit-to-printable-area 縮放。

**而 [printing-reports-positions.md](printing-reports-positions.md) 那套 ±0.05cm 的座標，
全部是在那個基準下實機驗收的**——座標表沒有記錄它的驗收前提，所以送印路徑一換整份就作廢。
其中 `margins:'none'` 最可能是主要位移來源：它把版面推到實體紙緣，而印表機有 0.3–0.5cm
不可列印邊界，與 [../gotchas.md](../gotchas.md)「不可列印邊界整欄吃掉 Left<0.5cm 的欄位」
是同一個機制、同一個量級。

諷刺的是本檔原本的「待驗證」與 gotchas 都寫過這個風險（「切到 100% 有機會讓已驗收座標再次跑掉
→ 上線前必須做對照組」），那個對照組沒做就上線了。

#### 這不是可證明的等價——也是「方向」軸存在的原因

Electron 33 的 `.d.ts` 為 `landscape`（預設 false）與 `color`（預設 true）標了**明文預設值**，
這暗示它們是無條件寫入 job settings 的，省略 ≠ 不送。若成立，方向仍會被強制成直向，
文牒（36.5×26.2 橫向）可能還是不對。**這點只能由現場的 D vs V 對照判定**（見「驗證」段），
而「方向」軸就是判定為真時的現場自救手段。

#### 舊設定一律重設，但使用者可以再選回來

遷移（決策 10）把已落地的 `scaleMode:'actual'` / `'fit'` 全部重設為 `driver`——先回到基準線，
因為無法分辨「刻意選的」與「印歪了亂試」。重設之後三個下拉都還在，需要的人自己調回去。

#### 1:1 對位不是被放棄，是被延後

日後真要做「PDF 頁面 = 實體紙張、100% 對位」，前提是現場每台機器都建好正確的自訂紙張 form，
而且**必須連同重新實機驗收 positions 全部座標一起做**。單獨改送印基準＝再製造一次本客訴。

### 3. PDF 由主行程自己抓，不走 IPC 傳 buffer

批次 PDF 可達數百 MB（實測 19018 筆會爆 2GB），structured clone 會在 main 再複製一份 → renderer + main 雙份記憶體。改用 `net.request` 串流落檔（`api-stream.ts`，與 `download.ts` 的 `.bak` 另存共用）。

`net.request` 是主行程 HTTP client，不是瀏覽器 fetch → **不受 CORS 限制**，一定讀得到 `X-Report-Page-Size`。

例外：報表預覽頁的 blob 已在 renderer 手上（且 job 已被取檔消耗），該條路徑才走 `printPdfBuffer(Uint8Array)`。

### 4. 紙張尺寸 single source of truth 在後端

> 2026-08-01 起 `X-Report-Page-Size` **不再參與送印**（紙張交回驅動），但整條傳遞鏈保留：
> 它現在的用途是寫進診斷紀錄（決策 9）——「印歪時驅動當初用的是哪張紙」的第一個線索，
> 以及對話框的唯讀紙張顯示。後端的權威表與一致性測試不變。

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

### 8. 診斷區：「用 PDF 檢視器列印」＋「開啟診斷紀錄」（2026-08-01 追加）

決策 1 明列的代價（沒有「印表機內容」按鈕）在 2026-08-01 變成客訴的一半：使用者要調
自訂紙張尺寸、紙匣與進紙方式。

**做法**：`openPdfInViewerWindow()` 把 PDF 開在一個**可見**的 `BrowserWindow({plugins:true})`，
然後什麼都不做——使用者按檢視器工具列的列印鈕，就會落到 Windows 原生 PrintDlgEx，
**那裡有「內容」按鈕**。這就是 `9264a23` 以前那條路，逐位元。

刻意**不**加 `#toolbar=0`：那顆工具列列印鈕正是本功能的重點（對話框裡的預覽 iframe 才需要藏它，
避免使用者誤按而繞過通道）。temp 檔在 `closed` 事件才刪。

一個 ~30 行的函式同時是三件事：
1. 現場對照組的**基準線產生器**——不必為了比對而回裝舊版
2. 新基準萬一在某台機器仍不對的**逃生門**
3. 在「印表機內容」按鈕（Phase 2）做出來之前，唯一能進驅動設定的路

**不做 `silent:false` 當選項**：大量列印分段會每段跳一次、隱藏視窗當 owner 會讓對話框躲到
主視窗後面（看起來像「按了沒反應」）、份數會被 DEVMODE 覆蓋。檢視器那條路涵蓋同樣的需求
且沒有這些問題。

### 9. 送印診斷紀錄（2026-08-01 追加）

2026-08-01 那輪客訴查不出症狀細節，根因是**現場什麼證據都留不下來**：packaged 版沒有 console，
`printResolved` 那行「未取得 X-Report-Page-Size」的 `console.warn` 等於不存在。

`%APPDATA%/Ceremony/logs/print-YYYYMMDD.log`，JSON Lines，每次送印一行：
`reportType` / `deviceName` / `pageSizeSource`（header|fallback|none）/ `pageSizeMicrons` /
**`options`（真正丟給 `webContents.print` 的完整物件）** / `bytes` / `attempts` / `durationMs` /
`result`。遷移發生時另記一行。

- 刻意跟 `print-settings.json` 同一個目錄樹（`appData/Ceremony`），不用 `app.getPath('logs')`
  ——那會落到另一個以 productName 命名的目錄，現場找不到。
- 對話框診斷區的「開啟診斷紀錄」→ `shell.showItemInFolder()`，使用者直接把檔案傳回來。
- 輪替：>1MB 改名 `.1`；掛在既有 `sweepTempDir()` 的啟動／離開兩次掃描上刪 7 天前的。
- **隱私**：不得出現 signupId、姓名、堂號、報表內容、token、temp 完整路徑。
  `deviceName` 是有意識的例外——診斷必需，且它可能含人名（「王小明的印表機」）；
  檔案純本機，只有使用者主動送出時才外流。見 [../design/security.md](../design/security.md)。

### 10. `print-settings.json` v1 → v2 就地遷移（2026-08-01 追加）

v1 的 `scaleMode: 'actual'|'fit'` 換成 `scale` / `orientation` / `paper` 三個獨立軸，
**舊值一律丟棄、重設為 `driver`**。

列印對話框的「記住」**預設是勾的**，所以現場的設定檔幾乎都已經落地 `scaleMode:'actual'`
——那正是位置跑掉的來源。而 `readPrintSettings()` 原本直接回傳 raw `byReportType`、完全不
sanitize，所以**只改程式的預設值對這些機器完全無效**。

→ 遷移做在 **read 端**（`print-settings-migrate.ts`，純函式可測）：`version<2` 時保留
`deviceName` / `copies`（那是使用者明確的選擇），`scaleMode` 直接丟棄。寫回是 best-effort，
失敗就下次再遷移一次——維持「設定檔壞掉不阻斷列印」的既有語意。

`'fit'` 也一起重設：無法分辨「刻意選 fit」與「印歪了亂試」，而這次改版的目的就是回到基準線。

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
      → PrintDialogService.ask({ previewBlob, onDiagnose, ... })   ← 內建 PDF 預覽 iframe
           │      Electron 額外帶 listPrinters + getPrintSettings 的預設值（三軸預設 driver）
           │      瀏覽器走 mode:'preview-only'（無印表機欄位、無診斷區，主鈕＝在新分頁開啟）
           │      分段模式只在第 1 段問一次，其餘段沿用
           │
           ├─ 非 Electron → openPdfInNewTab(blob)
           │
           ├─ 診斷區（Electron）
           │    ├─ 'viewer' → ceremony:openPdfInViewer → 可見的 PDF 檢視器視窗
           │    │               → 使用者按工具列列印鈕 → 原生 PrintDlgEx（有「內容」）
           │    └─ 'log'    → ceremony:openPrintLogFolder → shell.showItemInFolder
           │
           └─ Electron → ceremony:printPdfBuffer(type, bytes, choice, pageSizeHeader)
                            ← 唯一送印通道；每次最多一段（≈27 MB）
                          → main 寫 temp 檔 → printResolved

     printResolved → readPrintSettings（就地 v1→v2 遷移）
       → resolvePageSize（只有 paper:'report' 會用到；其餘僅寫 log）
       → buildPrintOptions({ copies, deviceName, scale, orientation, paper, pageSize })
              ← 三軸皆 driver（預設）時不產生任何格式選項
       → printPdfFile：隱藏視窗（plugins:true）載入 file://x.pdf
           → did-finish-load → 等 PDF 子 frame 掛上 → +250ms
           → webContents.print(options)   // 預設就只有這四個 key
           → callback 才關窗 + 刪 temp（提早關窗會殺掉列印）
       → logPrintEvent（options / pageSizeSource / result / durationMs）
```

## 檔案

| 層 | 檔案 | 職責 |
|---|---|---|
| Domain | `Ceremony.Domain/Reports/ReportPageSizes.cs` | 6 種紙張尺寸權威表 + 微米換算 |
| Api | `Controllers/ReportsController.cs` | `AppendPageSize()` 掛 `X-Report-Page-Size` |
| Api | `Program.cs` | `WithExposedHeaders` 加 `X-Report-Page-Size` |
| Electron | `electron/paper.ts` | fallback 尺寸表 + header 解析（純函式，可測） |
| Electron | `electron/api-stream.ts` | `streamApiToFile`（`download.ts` 共用）；**負責補齊目的目錄** |
| Electron | `electron/print-options.ts` | **送印基準**（純函式，可測）——要加任何 key 先讀該檔註解 |
| Electron | `electron/print-settings-migrate.ts` | 設定資料模型 + v1→v2 遷移（純函式，可測） |
| Electron | `electron/print-config.ts` | `print-settings.json` 讀寫（read 端就地遷移） |
| Electron | `electron/print-log.ts` | 送印診斷紀錄（寫入 / 輪替 / 過期清理） |
| Electron | `electron/print.ts` | `printPdfBuffer` / `openPdfInViewerWindow` / `listPrinters` / `sweepTempDir` |
| Electron | `electron/main.ts` | `plugins: true`、`setWindowOpenHandler`、6 個列印 IPC、temp/log 清理 |
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

- **靜默直印（完全不跳對話框）**：使用者要能看到印表機與預覽再按確定。設定已記住，第二次之後只是按 Enter。
- **`silent:false` 系統列印對話框**：大量列印分段會每段跳一次、隱藏視窗當 owner 會讓對話框躲到主視窗後面、份數被 DEVMODE 覆蓋。需求由決策 8 的檢視器路徑滿足。
- **依名稱指定驅動自訂 form**：Electron 的 `pageSize` 只有 `{width, height}`、沒有 `vendor_id`，做不到。現在不傳 `pageSize` 了，這個限制轉為「使用者自己在驅動裡選好紙」——與舊系統相同。
- **雲端列印 / HTML 印表機驅動**：沿用 [printing-reports.md](printing-reports.md) 的既有範圍決定。

## Phase 2（尚未實作）

**對話框加「印表機內容…」按鈕**，主行程 `spawn('rundll32.exe', ['printui.dll,PrintUIEntry', …])`：
`/e /n "<name>"` 開列印喜好設定（紙張／紙匣／方向）、`/p /n` 開印表機內容、`/s /t1` 開列印伺服器
內容的「表單」分頁（建自訂紙張，文牒 36.5×26.2 需要，且需本機管理員權限）。

要點：**不用 `shell:true`**（`printui.dll,PrintUIEntry` 含逗號會被 cmd 斷開；且名稱來自 renderer，
走 shell 就是命令注入）；main 端先 `listPrinters()` 做白名單比對；非 win32 不渲染按鈕；
`await once(child,'exit')` 期間 disable 列印鈕；關窗後重新 `listPrinters()`。

**這是唯一能「設一次、之後每次送印都吃到」的入口**：Chromium 的
`PrintingContextWin::UseDefaultSettings()` 取的正是驅動的每使用者預設 DEVMODE，而新基準
不覆寫任何東西 → 完整繼承。等 Phase 1 的現場對照驗過再做。

## 待驗證（Windows 實機，macOS 開發環境驗不了）

### 2026-08-01 送印基準回退——**上線前必做的對照組**

同一份 PDF × 3 條路徑各印一張，印完立刻在背面寫代號，疊起來對光比：

| 代號 | 路徑 | 意義 |
|---|---|---|
| **V** | 診斷區「用 PDF 檢視器列印」→ 工具列列印鈕 → 原生對話框直接確定 | **基準線＝改版前行為** |
| **D** | 新版直接列印 | 要證明 D ≡ V |
| **A** | v2.3.7 直接列印 | 確認客訴可重現、量出偏移量 |

前置：含 2 位往者的固定測試報名；三種報表（資料卡／薦牌／文牒）；至少 2 台印表機
（A4 雷射 + 薦牌用窄長機）；**用真正的預印紙，不要用白紙**（白紙看不出對位）。

量測（每張兩個錨點，量到 0.05cm）：資料卡＝固定欄位左緣／上緣到紙緣；薦牌＝編號「郵」字左緣到
紙左緣（[../gotchas.md](../gotchas.md) 那條的受害欄位）；文牒＝往者最左欄左緣到預印字欄右緣（應為 0.5cm）。

通過標準：
1. **D 與 V 兩個錨點差 ≤ 0.05cm**，3 報表 × 2 印表機全數成立
2. A 與 V 明顯不同（預期差 ≈ 不可列印邊界 0.3–0.5cm）→ 根因確認
3. **文牒的 D 沒被轉成直向、沒被裁切** → `landscape` 強制寫入的假設排除；若被轉了 → 必須補方向控制並重跑此表
4. 大量列印（>200 筆）跑完全程只跳一次自建對話框、分段面板行為與 v2.3.7 相同

證據（缺一不算通過——上一輪的失敗模式就是沒有實體對照組就上線）：9 張實體樣張拍照
（背面代號可見）、錨點量測表、整份 `%APPDATA%/Ceremony/logs/print-*.log`（可核對「使用者以為
印的是 D，log 裡的 options 是不是真的沒有格式選項」）、印表機型號 + 驅動版本 + Windows 版本。

### 其他

1. `plugins: true` 隱藏視窗 + `silent: true` 能否穩定印出；若印白紙 → 改成畫面外顯示（`setPosition(-20000,-20000)` + `showInactive()`）而非 `show: false`。
2. 資料卡與文牒的**實體紙張真實尺寸**要量一張現場的預印紙（見上方尺寸差異表）。注意：現在不指定 `pageSize`，這件事變成「驅動裡要選對紙」而不是「程式要傳對值」。
3. **`#toolbar=0` 是否真的必要**：Chromium 內建 PDF viewer 的工具列列印鈕在 Electron（`enable_print_preview=false`）內按下去的實際行為未定義。決策 8 的檢視器視窗正是靠那顆鈕運作——若它在實機上不出現，決策 8 整條要改走 Phase 2 的 `printui.dll`。**這是 Phase 1 最關鍵的單一未知數。**
4. **`PREVIEW_MAX_BYTES = 64 MB` 的門檻要不要調**：量 200 筆左右的批次「取檔 → IPC 傳 bytes → 寫 temp」的實際延遲與記憶體峰值。
5. 診斷紀錄的輪替與清理在長期使用下的實際檔案大小。
