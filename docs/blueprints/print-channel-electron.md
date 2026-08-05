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
keywords: [列印, 印表機, 紙張, 自訂表單, PaperSizes, DEVMODE, SetPrinter, PrintDlgEx, 原生對話框, 預覽視窗, PDF viewer, plugins, DeviceInfo, ReportPageSizes, PrinterFormMatcher, 頁面範圍, 診斷紀錄, 串流取檔, streamApiToFile]
last_updated: 2026-08-05 (新增**決策 9a：DEVMODE 的旗標與值必須同進退**——客訴「選了印表機卻跳『您的印表機已發生未預期的設定問題 0x80010105』〔＝RPC_E_SERVERFAULT〕」的根因就在決策 9 自己：`WritePaperSize` 清了 `DM_PAPERWIDTH`/`DM_PAPERLENGTH` 旗標卻沒把欄位歸零、`WriteSnapshot` 還原時整包蓋回 `dmFields`，兩處都留下自相矛盾的 DEVMODE，v4 驅動做 DEVMODE⇄PrintTicket 轉換時丟例外。規則抽成平台中立的 `DevModePaperFields`（ForFormSelection / ForRestore / AlreadySelected）＋ merge 後的 `NormalizeUnusedPaperFields`，回歸鎖 `DevModePaperFieldsTests`；`AlreadySelected` 順帶補上「DM_PAPERSIZE 有設」的條件。元教訓：Windows-only 專案裡任何不需要 Win32 handle 的邏輯都該住在 Domain——本決策拆了「比對」卻沒拆「位元運算」，於是這段在 macOS 與 CI 上完全隱形。另把 `CEREMONY_PRINTFORM_EXE` 指向不存在路徑寫成受文件保證的現場止血開關。先前 2026-08-04 (新增**決策 9：開視窗前把驅動的紙張預選成該報表的自訂表單**——客訴「舊系統送出列印會自動找到印表機的設定，新系統不行」，根因是決策 2 把舊系統唯一會主動設定的那格〔SignupForm.cs:1770-1787 用中文表單名比對 PrinterSettings.PaperSizes〕也一起劃到界線外；注入點在 Win32 的 SetPrinter Level 9〔每使用者預設 DEVMODE ＝ PrintDlgEx 初值來源，且不需 admin〕，落點是獨立的 Ceremony.PrintForm.exe；表單名 SSoT 收進 ReportPageSizes.FormName、比對與 ±0.5mm 容差在 PrinterFormMatcher；尺寸不符仍選它但標題與診斷紀錄帶 ⚠；refcount + journal 還原副作用；helper 失敗一律不影響列印成敗。決策 2 標題與「不做什麼」的「依名稱指定驅動自訂 form」條已改寫／刪除。先前 2026-08-02 (**全面改寫**：自建列印對話框 + silent:true 整條移除，改為「開 PDF 檢視器視窗 → 使用者按工具列列印鈕 → Windows 原生 PrintDlgEx」，逐位元對齊舊系統；列印設定（印表機/份數/三軸/記住）整組刪除；大量列印取消分段改回單一合併 PDF，後端合併改串流落檔；PDF 改由主行程 streamApiToFile 取檔，不再經 renderer；順帶修掉「列印完卡在列印中」的 UI 卡死。先前版本見 git 歷史 cc3ac5d 及更早))
---

## 背景與動機

**兩輪客訴，同一個根因**：

| 時間 | 客訴 | 當時的解法 |
|---|---|---|
| 2026-07-31 | 「舊系統按列印不用調設定；新系統有的可以、有的要手動調、有的讀不到印表機」 | 自建列印對話框 + `silent:true` 指定印表機／份數／縮放 |
| 2026-08-01 | 「可以選印表機，但**無法進去印表機裡面的設定**；格式也不對，之前調好的位置都跑掉了」 | 把送印基準攤成 scale/orientation/paper 三個軸讓現場自救 |
| 2026-08-02 | （承上）位置對不對只能靠現場土法對照，且列印完畫面會卡在「列印中」 | 整條拆掉，回歸舊系統形狀 |
| 2026-08-04 | 「舊系統送出列印會自動找到印表機的設定，新系統不行」（對話框有跳、列印 OK，只是紙張每次都要手動選） | **本次：補回舊系統的表單名比對（決策 9）** |

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

程式只負責產內容與預覽，**送印參數一個都不記**——除了一件事：跳 `PrintDialog` 之前
用中文表單名去驅動的紙張清單撈同名表單（`SignupForm.cs:1770-1787`）。
2026-08-02 那版漏掉了這一格，2026-08-04 由決策 9 補回。

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

### 2. 送印基準 = 不參與「這一次列印」的參數，但預先把驅動調到對的紙

> **2026-08-04 修訂**（原標題為「送印基準 = 完全不參與」）。原本的論證仍然成立，
> 但當時把「依名稱指定驅動自訂 form」也一起劃到界線外，那是錯的——見決策 9。

`buildPrintOptions`、`print-options.ts`、三個軸、`print-settings.json`、v1→v2 遷移，
**全部刪除**。程式不再有任何「送印選項」的概念：不呼叫 `webContents.print`、
不傳 `pageSize`／`scaleFactor`／`margins`／`deviceName`，**這一次列印**的參數一個都不由我們決定。

連帶的好處是**驗收前提自動成立**：`printing-reports-positions.md` 那套 ±0.05cm 的座標，
當初就是在「驅動 DEVMODE + Chromium PDF plugin 的 fit-to-printable-area」下實機驗收的
（v2.3.6 以前的 `window.open('blob:…pdf')` 路徑）。現在的路徑逐位元就是它——
不需要再證明「D ≡ V」，因為 D 就是 V。

**界線在哪**：我們動的是**驅動的每使用者預設 DEVMODE**（＝ Windows 原生的「列印喜好設定」，
PrintDlgEx 開啟時的初值來源），不是 Electron/Chromium 的送印參數。使用者仍可在對話框裡改掉，
決定權沒有被拿走。這與 v2.3.7 的 `pageSize:{width,height}` 是**相反**的東西：
那個是驅動不認得的 Custom 尺寸，這個是驅動自己的表單 ID。詳見決策 9。

⚠️ **與舊系統唯一的結構性差異**：舊系統 `DrawImage(pageImage, ev.PageBounds)` 是
**非等比拉滿整張紙**，所以驅動裡是什麼紙都無所謂；Chromium PDF 檢視器是
**fit-to-printable-area 等比縮放置中**。驅動選 A4 印 21×14.8 的資料卡，舊系統會拉滿、
新系統會縮小置中 → 位置跑掉。
**所以現場每台印表機必須依 `ReportPageSizes` 建正確的自訂紙張 form**（名稱與尺寸都要對，
決策 9 會依名稱去選它），見 [../design/infrastructure.md](../design/infrastructure.md)。

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

**2026-08-04 追加欄位**（決策 9）：`formTarget`（想要哪張紙）、`formResult`、`formKind`、
`formMismatchMm`、`formMs`、`printerVirtual`、`printerHash`；另有兩種獨立行
`form-restore-recovered` / `form-restore-failed`（還原成功不寫，避免每次列印變兩行）。

⚠️ 印表機**原始名稱**仍然不寫。現場名稱常是 `\\PC-王小明\HP LaserJet 1020`，
等於同時洩漏使用者姓名與內網主機名——比隱私名單上任何一項都嚴重。
helper 會回傳原始名稱（還原時需要指名同一台），但那條路只到還原 journal；
`print-form-core.ts` 的 `logFields()` 是**白名單**，`electron-print-form.spec.ts` 有測試鎖住它。

### 9. 開視窗前把驅動的紙張預選成該報表的自訂表單（2026-08-04）

**客訴**：「使用者在印表機設定好，舊系統送出列印會自動找到印表機的設定，新系統不行。」
現場症狀是原生對話框有正常跳出、列印也 OK，只是**紙張停在 A4／驅動預設，每次都要手動改**。

**根因是決策 2 劃錯了一條界線。** 舊系統除了「不記任何送印偏好」之外，還做了一件事——
在跳 `PrintDialog` 之前用中文表單名比對驅動的紙張清單（`SignupForm.cs:1770-1787`，
註解原文「取得印表機尺寸設定」）：

```csharp
foreach (PaperSize ps in printDialog.PrinterSettings.PaperSizes)
    if (ps.PaperName == paperSize.PaperName) { pss = ps; break; }
printDialog.Document.DefaultPageSettings.PaperSize = pss != null ? pss : paperSize;
```

命中的 `pss` 帶著驅動自己的 `RawKind`（＝ DEVMODE 的 `dmPaperSize` 表單 ID），驅動因此自動套用
該表單綁定的尺寸與紙匣。**一台印表機只有一個預設紙張，六種報表最多一種會對**——
少了這段比對，其餘五種每次都得手動選。原本「不做什麼」寫著「依名稱指定驅動自訂 form」，
那條就是本次客訴的來源，已刪除。

**注入點**：JS 層沒有辦法（`silent:false` 帶不進設定、`silent:true` 的 `pageSize` 只能給
驅動不認得的 Custom 尺寸，兩條都已實證，見 [../gotchas.md](../gotchas.md)）。
唯一的注入點在 Win32：**`SetPrinter` Level 9 的每使用者預設 DEVMODE 正是 PrintDlgEx 的初值來源**，
而且只需 `PRINTER_ACCESS_USE`（Level 8 的全域預設才要系統管理員）。

```
按列印 → 取檔成功
  → Ceremony.PrintForm.exe apply <reportType>     ← best-effort，3s 逾時
       PrinterSettings.PaperSizes 依中文名比對（＝舊系統那段，逐行相同）
       → OpenPrinter → DocumentProperties(讀) → 改 dmPaperSize → DocumentProperties(驗證)
       → SetPrinter(Level 9)
  → 開檢視器視窗（標題依結果可能帶 ⚠ 警告）
  → 使用者按 🖨 → PrintDlgEx 初值已是對的紙
  → 視窗 closed → 還原
```

**落點是獨立 exe 而不是 sidecar**，三個理由：
(a) `System.Drawing.Common` 的 API 全標 `[SupportedOSPlatform("windows")]`，塞進 `Ceremony.Api`
就得把 TFM 改成 `net10.0-windows`，`Ceremony.Api.IntegrationTests` 跟著改 → macOS 開發機再也跑不了整合測試；
(b) `DocumentProperties` 是驅動程式碼，網路印表機離線時會卡住整條執行緒且不吃 CancellationToken，
獨立子行程才砍得掉；
(c)「改這台電腦的印表機預設值」是桌面端的機器狀態變更，不該躲在 HTTP endpoint 後面。

**尺寸不符時選它，但絕不安靜。** 現場很可能還留著舊系統建的同名表單（尺寸是錯的）。
拒選只會讓客訴原封不動；而選了錯尺寸的同名表單一定比 A4 好（資料卡等比縮 ~3.9% vs 整份下移數公分）。
代價是可見度：`formResult:"mismatch"` + `formMismatchMm` 進診斷紀錄，**檢視器視窗標題帶 ⚠ 警告**。
`not-found`（驅動裡根本沒這張紙）同樣警告——那正是客訴的狀態。
容差 **±0.5mm**：下界是 1/100 吋的量化步階 0.254mm 的兩倍，上界必須 <1.0mm 才抓得到舊薦牌表單的 −1.0mm。
`PrinterFormMatcherTests` 用五張真實舊表單當 fixture 鎖住這個值。

**副作用必須還原。** 每使用者預設 DEVMODE 是整個使用者工作階段共用的（Word/Excel 開新文件也會吃到），
舊系統沒有這個副作用。所以：refcount（最後一個檢視器視窗關掉才還原，否則會弄掉另一個視窗的紙）
+ `%APPDATA%/Ceremony/print-form-restore.json` journal（app 崩潰時留下，下次啟動由
`recoverPendingFormRestore()` 撿回來）。還原只寫回四個純量（kind/fields/w/h）而不是整包 DEVMODE blob
——blob 會過期，使用者中途改過的驅動設定會被整包蓋掉。

**不可退步**：`applyReportForm()` 的任何結果都不得影響 `PrintResult.ok`
（`PrintService.report()` 會把 `ok:false` 丟成使用者看得到的紅字）。非 Windows 直接短路、
exe 缺檔回 `helper-missing`、3s/3.5s 雙層逾時、**helper 的 exit code 一律是 0**（成敗只看 stdout 的
`result`，用 exit code 表達失敗只會誘導呼叫端寫出「非 0 就當錯誤」的分支）。

**表單名的 SSoT 在 `ReportPageSizes.FormName`**，與尺寸相鄰：名字用來比對、尺寸用來驗證，
分開放就會有第二個真相來源。`ReportPageSizeConsistencyTests` 斷言每種報表都有非空且互異的表單名
——新增第 7 種報表卻忘了給名字會是 build 失敗，不是現場印在 A4 上。

#### 9a. DEVMODE 的旗標與值必須同進退（2026-08-05 修正，客訴 `0x80010105`）

**客訴**：「選了印表機，卻出現**您的印表機已發生未預期的設定問題．0x80010105**。」

`0x80010105 = RPC_E_SERVERFAULT`。Windows 的列印 UI 讀每使用者預設 DEVMODE、轉成 PrintTicket、
開驅動設定頁時失敗就顯示這句——**而寫那份 DEVMODE 的正是本決策**。上面那段
`SetPrinter` Level 9 的初版留下了一份自相矛盾的 DEVMODE：

| 位置 | 初版做的事 | 問題 |
|---|---|---|
| `WritePaperSize` | 清 `DM_PAPERWIDTH`/`DM_PAPERLENGTH` **旗標**，不動欄位 | 旗標說「未使用」、結構裡卻躺著上一張紙的寬高 |
| `WriteSnapshot`（還原） | 把快照的 `dmFields` **整包 32 位元**蓋回去 | 使用者在檢視器開著時按「內容」改的雙面／方向，旗標被清掉、值還在 |

**不變式**：`dmFields` 的某個旗標沒設 ⇒ 對應欄位「未使用」⇒ 唯一合法的值是 0。
規則收進平台中立的 `Ceremony.Domain.Reports.DevModePaperFields`：

- `ForFormSelection(current)` — 選表單後的 `dmFields`；呼叫端**同時**把 `dmPaperWidth`/`dmPaperLength` 寫 0
- `ForRestore(current, snapshot)` — 只換三個紙張位元，**其餘位元保留現況**
- `AlreadySelected(kind, fields, wanted)` — 順帶補上「`DM_PAPERSIZE` 有設」這個條件（旗標沒設時
  `dmPaperSize` 只是被驅動忽略的殘值，比對相等會誤判成「不必寫入」而讓該修的機器修不到）

`DocumentProperties` merge 之後另加一道 `NormalizeUnusedPaperFields`：驅動的正規化結果也要守同一條
規則。**刻意選「歸零」而不是 `driver-rejected`**——旗標沒設的欄位歸零在定義上是安全的，
而整個放棄預選等於把 2026-08-04 那則客訴原封不動退回去。

**為什麼上線兩天才發現**：`Ceremony.PrintForm` 是 `net10.0-windows` 的 exe，macOS 開發機連跑都跑不了，
CI 也不印東西。本決策當初拆出 `PrinterFormMatcher` 的理由（「純邏輯要平台中立、測得到」）完全正確，
只是**只拆了「比對」沒拆「位元運算」**。→ **Windows-only 專案裡任何不需要 Win32 handle 的邏輯都應該
住在 Domain**；`Ceremony.PrintForm` 剩下的東西應該只有 P/Invoke 與 buffer 生命週期。
回歸鎖 `DevModePaperFieldsTests`，其中一條斷言 `ForFormSelection` 的輸出必然被 `AlreadySelected`
判為 true——兩個函式是同一條規則的兩面，任一邊被單獨改動就會炸（無窮寫入或永遠不寫）。

**現場止血手段**（不必出新版）：環境變數 `CEREMONY_PRINTFORM_EXE` 指到不存在的路徑 →
`helper-missing` → 整段跳過，列印本身完全不受影響。這是「不可退步」那條的自然結果，
但它現在是**被文件保證的行為**，見 [../design/infrastructure.md](../design/infrastructure.md) 列印排障段。

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
| Domain | `Ceremony.Domain/Reports/PrinterFormMatcher.cs` | 報表 → 驅動表單的比對與 ±0.5mm 容差（平台中立、可測） |
| PrintForm | `Ceremony.PrintForm/`（Windows-only exe） | `PrinterSettings.PaperSizes` 名稱比對 + `SetPrinter` Level 9 + 還原 |
| Electron | `electron/api-stream.ts` | `streamApiToFile`（備份下載共用） |
| Electron | `electron/print.ts` | `openReportInViewer` / `openPdfInViewer` / `sweepTempDir` |
| Electron | `electron/print-form.ts` | 子行程呼叫、refcount、還原 journal（best-effort） |
| Electron | `electron/print-form-core.ts` | 純函式：輸出解析 / 視窗標題 / log 欄位白名單 |
| Electron | `electron/print-log.ts` | 診斷紀錄（寫入 / 輪替 / 過期清理） |
| Electron | `electron/main.ts` | `plugins: true`、2 個列印 IPC、temp/log 清理、開機還原紙張 |
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
- **記住印表機／份數**：原生對話框自己會記（那是 Windows 的每使用者 DEVMODE），
  我們再記一份只會有兩個真相。
- **批次套用到所有已安裝印表機**（決策 9）：副作用面積 × N、還原風險 × N，
  收益只有「使用者懶得改預設印表機」。舊系統也只認預設印表機。
- **Level 8（per-machine）預設**（決策 9）：需要系統管理員，而寺方使用者多半不是；
  Level 9 才是「列印喜好設定」那個 UI 寫入的位置。
- **現場可設定的表單名對照表**（決策 9）：名稱是契約，寫死在 `ReportPageSizes.FormName`。
  多一層可設定就多一個「設錯了但沒人知道」的面。
- **找不到同名表單時退回相近尺寸**（決策 9）：資料卡與普桌資料卡尺寸完全相同、名稱不同，
  靜默代換＝印在別種報表的紙上而沒有任何訊號。找不到就是 `not-found` + 標題警告。
- **後端 append-mode 合併 / 換 PDF library**：見決策 5 的限制說明，等真的撞到再說。

## 待驗證（Windows 實機，macOS 開發環境驗不了）

> 現場已回報「原生對話框有跳、列印也 OK」（2026-08-04 客訴），所以下面第 1 項的
> (a)(b)(c)(d) 實務上已經過了；第 0 項是決策 9 的阻斷性前提，仍未驗。

### ⚠️ 阻斷性 0：PrintDlgEx 是否取每使用者預設 DEVMODE 當初值（決策 9 的前提）

**零程式碼實驗，2 分鐘**：控制台 → 裝置和印表機 → 預設印表機 → 右鍵**列印喜好設定** →
紙張改成已建好的「資料卡」→ 確定（這個 UI 底層做的事就是 `SetPrinter` Level 9，與 helper 等價）
→ 開 app 列印資料卡 → 檢視器按 🖨 → 看對話框的「紙張」是不是已經是「資料卡」。

**不通過 → 決策 9 作廢**（helper 白寫），改走 `printui.dll` 路線
（`rundll32 printui.dll,PrintUIEntry /e /n "<name>"` 幫使用者開列印喜好設定；
不用 `shell:true`，名稱先與印表機清單做白名單比對），並把否證寫進 gotchas。

### ⚠️ 阻斷性 1：檢視器工具列的列印鈕

Electron build 不含 print preview WebUI，Chromium PDF viewer 工具列列印鈕按下去的實際行為。

1. 任一報名 → 右鍵「列印資料卡」→ 檢視器視窗出現
2. 按工具列的 🖨
3. 檢查：(a) 跳出 Windows 原生列印對話框 (b) 有「內容(R)…」按鈕
   (c) 有「頁面範圍」欄位 (d) 按確定真的印出來

### 決策 9（紙張預選）

1. 六種正確表單都建好後，六種報表各印一次，PrintDlgEx 開啟時紙張＝對應中文表單
   （含**連續切換不同報表**，驗證 Chromium 沒有快取住第一次的 DEVMODE）
2. 只留舊尺寸「資料卡」(201.7×142.2) → 仍選中、視窗標題出現 ⚠、
   log `formResult:"mismatch"` 且 `formMismatchMm ≈ -8.32x-5.76`
3. 刪掉「文牒」表單 → log `not-found`、標題出現 ⚠、視窗照常開、仍可正常列印
4. 預設印表機設為 Microsoft Print to PDF → `not-found` + `printerVirtual:true`，不崩不報錯
5. 關閉檢視器視窗後，「列印喜好設定」的紙張回到原值
6. 開著視窗時用工作管理員強制結束 app → 重開 → 紙張已還原、
   log 有 `form-restore-recovered`、journal 檔已刪
7. 同時開資料卡 + 文牒兩個視窗 → 關掉一個，另一個紙張仍在；兩個都關才還原
8. 在 PrintDlgEx 裡改選另一台印表機 → 紙張回到那台的預設（已知限制，舊系統相同），不出錯
9. **非管理員帳號**登入執行 → 全部照常（Level 9 不需 admin 是選型的關鍵前提）
10. 網路印表機離線／關機時列印 → helper 3 秒內放棄、視窗照常開、log `helper-timeout`

### 其他

1. **實體對位**：用真正的預印紙印資料卡／薦牌／文牒各一張，量兩個錨點到 0.05cm，
   與 v2.3.6 的樣張比對 → 應完全一致（同一條路徑；決策 9 只改紙張預選，沒動送印路徑）。
2. **頁面範圍續印**：同一個檢視器視窗再按列印，填 `600-1200`，確認只印後半。
3. **5000 筆**：後端記憶體峰值（工作管理員看 `Ceremony.Api`）、渲染時間、
   `%TEMP%/ceremony-batch/` 有無殘留。
4. **卡住 bug 回歸**：連續列印 5 筆，每次列印完立刻能再列印下一筆，
   批次按鈕不會停在「產生中…」，全程不需切換選單。
5. 關閉檢視器視窗後 `%TEMP%/ceremony-print/` 的檔案有被刪。
6. 檢視器視窗 `parent: mainWindow` 之下，原生列印對話框不會躲到主視窗後面。
