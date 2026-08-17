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
keywords: [列印, 印表機, 紙張, 自訂表單, PaperSizes, DEVMODE, SetPrinter, PrintDlgEx, 原生對話框, 預覽視窗, PDF viewer, plugins, DeviceInfo, ReportPageSizes, PrinterFormMatcher, PrinterFormPolicy, PrinterContactPolicy, 印表機黑名單, 卡死, 虛擬印表機, 還原 journal, 頁面範圍, 診斷紀錄, 串流取檔, streamApiToFile, 焦點, focus, z-order, 視窗跳到後面, window-focus]
last_updated: 2026-08-11 (**決策 9d 補記：「自動選紙」開關原本活不過重開機**〔現場紀錄連著三行 `form-preselect-toggled {enabled:false}`＋三次 `skipped-disabled`。根因在 bootstrap 的種子覆寫是整包 assign，`printFormPreselect` 每次開機被清掉 → 改走 `config-merge.ts` 的 `mergeConfig`；教訓：止血鍵必須活得比故障久〕。同日先前 (**新增決策 9e：排障鈕不得掛在「要先產出預覽」的工具列裡**〔使用者裝完 v2.4.6 回報「工具列沒看到」。三顆復位鍵（診斷紀錄／印表機設定／自動選紙）在 `@if (previewUrl())` 之內，而它們要救的故障情境正好產不出預覽 ⇒ 獨立成頁面上方的 `.trouble-bar`。可轉移教訓：把逃生門裝在只有一切正常時才打得開的房間裡，等於沒有逃生門〕。先前 2026-08-10 (決策 9d **追加「印表機設定」按鈕**〔客戶回報**找不到 `%APPDATA%` 底下的檔案**〕：排障 ④ 做成工具列按鈕 `rundll32 printui.dll,PrintUIEntry /e /n "<預設印表機>"`——**覆寫壞掉 DEVMODE 的是 Windows 自己的 UI 不是我們**〔要修它就得先讀它，而讀就是一次接觸〕，順便完成排障 ① 的分流〔連那個視窗都開不起來 ⇒ 與本程式無關〕；同時記下**「不要把復位動作放進安裝程式」**的否決理由〔提權帳號的 `$APPDATA` 可能是別的 profile、一台多使用者只清得到一個、真正要復位的不是檔案、`print-form-restore.json` 刪掉比留著糟〕。同日先前 (新增**決策 10：關掉預覽後把焦點交還主視窗**——客訴「同時開著其他應用程式時，列印完把預覽關掉，系統畫面會自動跳到後面」。根因是**沒有人負責把焦點接回來**：Windows 關窗時焦點交給 z-order 的下一個視窗，`parent`（owner）只保證壓在上面、**不保證關掉後焦點回到 owner**，中間又隔了一個 modal 的 PrintDlgEx 再轉一次焦點。處置：`electron/window-focus.ts` 的 `returnFocusOnClose`，掛在 `print.ts` 檢視器視窗與 `main.ts` 的 `did-create-window` 兩個開窗點；**刻意只在「預覽關閉當下自己持有焦點 ＋ 主視窗未 destroy ＋ 未最小化」三者同時成立時才搶**（搶過頭會在使用者刻意切走時把畫面拉回來，比原本的 bug 更煩人）；實作是「`close` 記焦點狀態、`closed` 才動作」（前者晚了問不到、後者早了會被系統的焦點轉移蓋掉）。回歸鎖 `electron-window-focus.spec.ts` 5 條。**Windows 實機待複驗**。同日先前 (新增**決策 9d：出過一次事就永遠不碰那台驅動**——客訴 **KYOCERA PA2000 在 v2.4.5（決策 9c）之後症狀變成「按下檢視器列印鈕後整個程式卡死、選不了別台印表機也關不掉預覽，只能重啟」**。9c 是 fail-closed 的 ⇒ 那台機器上我們**早就不寫入**了，所以剩下能怪的是**接觸本身**：9c 新增的 `PTOpenProvider` / `PTConvertDevModeToPrintTicket` 是 v4 驅動的 COM 呼叫〔設定模組多半跑在 PrintIsolationHost.exe〕，而 Electron 端的 3 秒逾時是 `SIGKILL`——**把 COM client 殺在半路**，之後 PrintDlgEx 問同一個 provider 就可能永遠等不到，而它是 modal 的、owner 視窗會被 disable〔＝現場看到的卡死〕；再乘上「每次列印都問一次」。處置三件：(1) **失敗印表機黑名單** `print-form-printers.json`〔`skipped-printticket-reject`/`unavailable`/`driver-rejected`/`error` → 記 printerHash，之後在**任何驅動呼叫之前**結束；`helper-timeout`/`helper-error` 判斷不出是哪台 → 整台停用。判定＝ TS 的 `blockScope` ＋ C# 的 `PrinterContactPolicy`，兩邊都有測試；`not-found`/`mismatch`/`skipped-virtual` **刻意不記帳**〕；(2) **逾時不再 kill**〔execFile 拿掉 timeout/killSignal；配套是 helper 自己守 `--budget-ms` → `skipped-over-budget`，加 `onLate` 補還原 journal，加 `skipped-helper-busy` 不疊第二個呼叫〕；(3) **使用者按得到的開關**〔報表預覽頁「自動選紙：開/關」→ `config.json` 的 `printFormPreselect`；按回「開」同時清黑名單。原本只有 `CEREMONY_PRINTFORM_EXE` 環境變數，寺方按不到〕。可轉移教訓：**fail-closed 的預檢只保證「沒把它寫壞」，不保證「沒把它弄卡」——一個會叫醒第三方行程的檢查本身就是一次接觸**。回歸鎖 `PrinterContactPolicyTests` + `electron-print-form.spec.ts` 的 `blockScope`/`applyArgs`/四條標題。**Windows 實機待複驗**。先前 2026-08-08 (新增**決策 9c：寫入前先自己轉一次 PrintTicket，轉不過就不寫**——客訴 **KYOCERA PA2000 在 v2.4.2（決策 9a）之後仍噴 `0x80010105`**；9a 修的旗標一致性是真 bug 也真的修好了，但**不是唯一的失敗方式**〔`DevModePaperFields` 只保證「沒違反已知的不變式」，驅動吃不吃始終是猜的；v4 驅動的使用者預設本體是 PrintTicket，DEVMODE 只是要即時轉換的相容介面〕。處置：merge 之後、`SetPrinter` 之前自己呼一次 `prntvpt.dll` 的 `PTConvertDevModeToPrintTicket`〔＝列印 UI 開設定頁走的同一條轉換〕，過了才寫；判定收進平台中立的 `PrintTicketPreflight`，新增兩個 `formResult`〔`skipped-printticket-reject` / `skipped-printticket-unavailable`〕，**刻意 fail-closed**〔檢查跑不起來也不寫，理由同 9b〕，還原路徑刻意不預檢。回歸鎖 `PrintTicketPreflightTests` + `electron-print-form.spec.ts` 兩條。另新增〈**待評估：回到 WinForms PrintDialog（＝舊系統的形狀）**〉：查證舊系統 `SignupForm.cs:1764-1798` 後確認 2026-08-02 放棄程式送印的理由〔沒有「印表機內容」按鈕〕**在這個方案上不成立**——當時是三選一漏了一個〔舊系統用的是系統的 `PrintDialog`，hDevMode 非 null ⇒ 機制上不會噴〕；入場費是整份座標表再次作廢＋向量變點陣＋必須取代不能並存，**只評估未動工**。**Windows 實機待複驗**。先前 2026-08-06 (新增**決策 9b：只有「確定會變好」才動那份共用狀態**——起點是使用者的觀察「這問題在舊系統不會出現」，成立而且是結構性的〔舊系統寫的是 process-local 的 PrintDocument.DefaultPageSettings、值來自驅動給的現成物件；我們寫的是全域持久化的每使用者預設 DEVMODE、位元自己組 → 失敗代價差一個數量級，而這是 v2.3.9 改走 Chromium 檢視器後「沒有 PrintDocument 可綁」的連帶代價〕。既然代價不對稱門檻就要不對稱：判斷抽成平台中立的 `PrinterFormPolicy.Decide`，**只有 Exact + 實體印表機才寫入**——`SizeMismatch` 改成不寫〔**推翻 2026-08-04 的「仍選它」**：原本的比較漏了第三個選項，不寫 ≠ 停在 A4 而是停在使用者目前的紙且標題請他手動選，最壞是多按幾下不是印壞；且尺寸不符幾乎都代表現場表單建錯，靜默套一張已知不對的紙只會讓它更晚被發現〕、虛擬印表機從「靠 NotFound 間接擋住」改成明確 `skipped-virtual`。順帶修掉一個還原洩漏：寫 journal 的條件從 `viewerCount === 0` 改成「journal 是否已存在」〔舊條件下「第一個視窗 not-found ＋第二個視窗寫入成功」會寫進 DEVMODE 卻不留還原紀錄，那張紙就永遠留在使用者的 Word/Excel〕，連帶得到「已有 journal 就完全不呼叫 helper」〔`skipped-viewer-open`，不會換掉前一個視窗正在等按列印的紙〕。回歸鎖 `PrinterFormPolicyTests`〔核心斷言：掃過所有 FormMatch × 虛擬與否，會寫入的恰好只有一格〕。**Windows 實機待複驗**。先前 2026-08-05 (新增**決策 9a：DEVMODE 的旗標與值必須同進退**——客訴「選了印表機卻跳『您的印表機已發生未預期的設定問題 0x80010105』〔＝RPC_E_SERVERFAULT〕」的根因就在決策 9 自己：`WritePaperSize` 清了 `DM_PAPERWIDTH`/`DM_PAPERLENGTH` 旗標卻沒把欄位歸零、`WriteSnapshot` 還原時整包蓋回 `dmFields`，兩處都留下自相矛盾的 DEVMODE，v4 驅動做 DEVMODE⇄PrintTicket 轉換時丟例外。規則抽成平台中立的 `DevModePaperFields`（ForFormSelection / ForRestore / AlreadySelected）＋ merge 後的 `NormalizeUnusedPaperFields`，回歸鎖 `DevModePaperFieldsTests`；`AlreadySelected` 順帶補上「DM_PAPERSIZE 有設」的條件。元教訓：Windows-only 專案裡任何不需要 Win32 handle 的邏輯都該住在 Domain——本決策拆了「比對」卻沒拆「位元運算」，於是這段在 macOS 與 CI 上完全隱形。另把 `CEREMONY_PRINTFORM_EXE` 指向不存在路徑寫成受文件保證的現場止血開關。先前 2026-08-04 (新增**決策 9：開視窗前把驅動的紙張預選成該報表的自訂表單**——客訴「舊系統送出列印會自動找到印表機的設定，新系統不行」，根因是決策 2 把舊系統唯一會主動設定的那格〔SignupForm.cs:1770-1787 用中文表單名比對 PrinterSettings.PaperSizes〕也一起劃到界線外；注入點在 Win32 的 SetPrinter Level 9〔每使用者預設 DEVMODE ＝ PrintDlgEx 初值來源，且不需 admin〕，落點是獨立的 Ceremony.PrintForm.exe；表單名 SSoT 收進 ReportPageSizes.FormName、比對與 ±0.5mm 容差在 PrinterFormMatcher；尺寸不符仍選它但標題與診斷紀錄帶 ⚠；refcount + journal 還原副作用；helper 失敗一律不影響列印成敗。決策 2 標題與「不做什麼」的「依名稱指定驅動自訂 form」條已改寫／刪除。先前 2026-08-02 (**全面改寫**：自建列印對話框 + silent:true 整條移除，改為「開 PDF 檢視器視窗 → 使用者按工具列列印鈕 → Windows 原生 PrintDlgEx」，逐位元對齊舊系統；列印設定（印表機/份數/三軸/記住）整組刪除；大量列印取消分段改回單一合併 PDF，後端合併改串流落檔；PDF 改由主行程 streamApiToFile 取檔，不再經 renderer；順帶修掉「列印完卡在列印中」的 UI 卡死。先前版本見 git 歷史 cc3ac5d 及更早)))))))))
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

入口從已刪除的列印對話框搬到**報表預覽頁的「診斷紀錄」按鈕**（`/reports/preview`，
只在桌面版顯示）→ `shell.showItemInFolder()`。2026-08-11 起與另外兩顆排障鈕一起搬到
**頁面上方的列印排障列**，不再需要先產出預覽（見決策 9e）。

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

#### 9b. 只有「確定會變好」才動那份共用狀態（2026-08-06 縮小爆炸半徑）

**起點是使用者的一句觀察**：「這問題在舊系統不會出現。」——完全正確，而且**不是運氣，是結構**：

| | 舊系統 | 本決策（9／9a） |
|---|---|---|
| 寫到哪 | 自己那份 `PrintDocument.DefaultPageSettings` | 驅動的**每使用者預設 DEVMODE**（`SetPrinter` Level 9） |
| 生命期 | process-local，程式關掉就沒了 | 持久化，留在系統裡 |
| 影響範圍 | 只有這次列印 | 全域，外溢到 Word/Excel |
| 值哪來 | 驅動給的現成 `PaperSize` 物件 | 我們自己組 `dmFields` 位元 |
| 寫壞的後果 | 這次印不對，重開就好 | `0x80010105`；還原沒跑到就一直錯 |

舊系統的做法沒有失敗模式可言；我們的做法有一整類。原因不是誰比較粗心，是 v2.3.9 把送印改成
「Chromium PDF 檢視器 → 原生 PrintDlgEx」之後，手上**沒有 `PrintDocument` 可以綁**，
唯一的注入點就只剩那份共用的 DEVMODE。這是那次決策的連帶代價，當時沒被算進去。

**既然代價不對稱，門檻就必須不對稱**：只有「確定會讓列印變好」才動它。判斷收進平台中立的
`Ceremony.Domain.Reports.PrinterFormPolicy.Decide(match, isVirtualPrinter)`：

| 情況 | 舊行為 | 現行為 | 理由 |
|---|---|---|---|
| `Exact` + 實體印表機 | 寫入 | **寫入**（唯一入口） | 這正是決策 9 要解的客訴 |
| `SizeMismatch` | 寫入 + 標題警告 | **不寫**，標題警告 | 見下 |
| `NotFound` | 不寫 | 不寫 | 本來就沒東西可選 |
| 虛擬印表機 | 靠 `NotFound` **間接**擋住 | **明確跳過**（`skipped-virtual`） | 「虛擬印表機不會有我們的表單」是推論不是保證，賭錯就改掉使用者的 PDF 輸出設定 |

**`SizeMismatch` 改成不寫，推翻 2026-08-04 的決定。** 原本的理由是「選錯尺寸的同名表單頂多等比縮幾 %，
停在 A4 則整份位移數公分完全不能用」——那個比較**漏了第三個選項**：不寫入 ≠ 停在 A4，而是停在
使用者目前的預設紙，且檢視器標題會請他手動選（他仍然選得到那張表單）。所以最壞情況是多按幾下，
不是印壞。用「多按幾下」換掉「動全域 DEVMODE」這個高代價動作，在尺寸本來就已經錯了的前提下划算；
而且 `SizeMismatch` 幾乎都代表現場表單建錯（多半是舊系統留下的），靜默替他套一張已知不對的紙
只會讓那件事更晚被發現。標題文案同步改成「**未自動選用**…請手動選紙」——只說「請重建」會讓使用者
以為這次還是選好了。

**順帶修掉一個還原洩漏**：`applyReportForm` 原本用 `viewerCount === 0` 當寫 journal 的條件，
於是「第一個視窗 `not-found`（沒寫入）＋第二個視窗寫入成功」的組合會**寫進 DEVMODE 卻不留還原紀錄**，
那張紙就永遠留在使用者的 Word/Excel 上。判準改成 **journal 是否已存在**——journal 才是
「共用狀態被動過」的憑證，`viewerCount` 只是視窗數。連帶得到一個更好的行為：已有 journal 時
**完全不呼叫 helper**（`skipped-viewer-open`），不會把前一個視窗正在等使用者按列印的紙換掉。

回歸鎖 `PrinterFormPolicyTests`：核心那條斷言「掃過所有 `FormMatch` × 虛擬與否的組合，
會寫入的**恰好只有一格**」——動全域共用狀態的入口只能有一個，新增分支想放行就會打破它。
`ToResult` 的字串是與 `electron/print-form-core.ts` 的 `HELPER_RESULTS` 的跨語言契約
（少一個 → `parseHelperOutput` 把整包退成 `helper-error`），兩邊各有測試鎖住。

#### 9c. 寫入前先自己轉一次 PrintTicket，轉不過就不寫（2026-08-08，KYOCERA PA2000）

**起點**：客訴回報 **KYOCERA PA2000 在 v2.4.2（決策 9a）之後仍然噴 `0x80010105`**。
9a 修的是「旗標與值不同進退」——那個 bug 是真的，也真的修好了，但它**不是唯一的失敗方式**。

**新的認識**：`DevModePaperFields` 能保證的只有「我們沒違反**已知的**那條不變式」。
驅動吃不吃我們手工組的 DEVMODE，始終是**猜的**。而 v4 驅動的使用者預設**本體是 PrintTicket**，
DEVMODE 只是一層要即時轉換的相容介面，轉換由驅動廠商實作——品質不在我們手上。
再重讀一次舊系統（`SignupForm.cs:1764-1798`）還發現兩件先前沒寫進來的事：

- **會噴這句錯誤的那個轉換，舊系統根本不會走到**：`PrintDialog.ShowDialog()` 帶著**自己那份
  hDevMode**（非 null）進 `PrintDlgEx`，初值不從每使用者預設拿。我們的 Chromium 檢視器路徑
  `hDevMode` 是 null，初值只能來自每使用者預設——**正是因為要讀它，才被迫去寫它**。
- **舊系統連失敗都是軟的**：`pss != null ? pss : paperSize`，找不到同名表單就退回自己 `new` 的
  `PaperSize`，最壞是這次紙不對。

**處置：把「猜驅動會不會吐」換成「問驅動會不會吐」。** 在 `DocumentProperties` merge 之後、
`SetPrinter` 之前，自己呼一次 `prntvpt.dll` 的 `PTConvertDevModeToPrintTicket`——
**那正是 Windows 列印 UI 開啟印表機設定時走的同一條轉換**，也就是會吐 `RPC_E_SERVERFAULT` 的那一支。
過了才寫。判定收進平台中立的 `Ceremony.Domain.Reports.PrintTicketPreflight`：

| 預檢結果 | `formResult` | 寫入？ |
|---|---|---|
| 轉換成功 | （沿用 `exact` / `unchanged`） | ✅ 唯一會寫的情況 |
| 驅動吐錯誤 | `skipped-printticket-reject` | ❌ |
| 檢查跑不起來（缺 dll／provider 開不了／例外） | `skipped-printticket-unavailable` | ❌ |

**刻意 fail-closed**：檢查跑不起來也不寫。理由與 9b 同一條——檢查失敗代表我們**不知道**這次寫入
會不會變好，而不知道時的預設值必須是不動。代價是那台機器退回「每次手動選紙」（多按幾下），
換掉的是「在使用者機器上留一份壞掉的共用設定」。
兩種失敗分成兩個 `formResult` **不是**為了讓呼叫端分流（都不寫），而是為了診斷紀錄查得下去：
「這台驅動不接受」與「這台機器連檢查都做不了」要修的東西完全不同。
若現場出現大量 `skipped-printticket-unavailable`，那是**要查的訊號，不是放寬的理由**。

**還原路徑刻意不做預檢**：還原寫回去的是使用者原本的值，而預檢失敗會讓我們選的表單**永遠留在
他機器上**——那正是預檢要防的事。還原寧可寫，也不要卡住。

回歸鎖 `PrintTicketPreflightTests`（核心：掃過 HRESULT 組合，`MayWrite` 為真的**只有兩段都成功**）
＋ `electron-print-form.spec.ts` 兩條（標題要講「手動選哪張紙」、兩種結果都不得留還原 journal）。
**⚠️ Windows 實機待複驗**——這支 API 在 macOS 上連呼都呼不到，而 `PTOpenProvider` 對 v3 驅動
是否一律成功，只有現場的診斷紀錄答得出來。

**這不是終局解**：它讓「壞掉的共用設定」不再發生，但那台機器也就沒有自動選紙了。
真正的結構解是讓我們自己擁有列印工作（＝舊系統的形狀），見〈待評估：回到 WinForms PrintDialog〉。

#### 9d. 出過一次事就永遠不碰那台驅動（2026-08-10，KYOCERA PA2000 卡死）

**起點**：客訴回報 **PA2000 在 v2.4.5（決策 9c 的預檢）之後症狀變了**——不再跳
`0x80010105`，改成「**按下檢視器的列印鈕之後整個程式卡死**，選不了其他印表機、也關不掉預覽，
只能重新啟動法會系統」。現場確認卡住的時間點是**按 🖨 之後**（預覽視窗本身開得起來）。

**推論**：9c 是 fail-closed 的，所以在那台機器上我們**早就不寫入**了。既然不是寫入，
剩下能怪的只有**接觸本身**——而 9c 正好新增了一條接觸面：

| 面向 | 9c 之前 | 9c 之後 |
|---|---|---|
| 對驅動的呼叫 | `DeviceCapabilities`（紙張清單）+ `DocumentProperties` | 再加 `PTOpenProvider` / `PTConvertDevModeToPrintTicket` |
| 逾時處理 | `execFile({ timeout: 3000, killSignal: 'SIGKILL' })` | 同左 |

v4 驅動的設定模組多半跑在 `PrintIsolationHost.exe`，上面那兩支是 COM 呼叫。
**在 COM 呼叫進行到一半 TerminateProcess，對方留在什麼狀態不由我們決定**；
之後 `PrintDlgEx` 去問同一個 provider 就可能永遠等不到回應——而 PrintDlgEx 是 modal 的，
它的 owner 視窗會被 disable，看起來就是「預覽關不掉、也不能選別台印表機」。
再加上我們**每次列印都問一次**，等於把這個機率乘以列印次數。

**處置（三件事，都在「不再接觸」這條線上）**：

1. **失敗印表機黑名單**（`%APPDATA%/Ceremony/print-form-printers.json`）：
   `skipped-printticket-reject` / `skipped-printticket-unavailable` / `driver-rejected` / `error`
   → 記下該台的 `printerHash`，**之後 helper 在任何驅動呼叫之前就結束**（`skipped-printer-blocked`）；
   `helper-timeout` / `helper-error` 判斷不出是哪一台 → 整台機器停用預選。
   判定是純函式 `blockScope`（TS 端）與 `PrinterContactPolicy`（C# 端），兩邊都有測試。
   ⚠️ **`not-found` / `mismatch` / `skipped-virtual` 不記帳**：那是驅動好好回答了，
   記下去會讓「現場還沒建表單」被誤鎖成永久黑名單，建好之後也不會生效。
2. **逾時不再 kill**：`execFile` 拿掉 `timeout` / `killSignal`。逾時只代表「我們不等了」，
   不代表「它必須立刻死」。配套是 helper 自己守住「呼叫端不等了就不寫入」
   （`--budget-ms`，在 `SetPrinter` 前檢查 → `skipped-over-budget`），
   外加 `onLate`：萬一它真的卡在檢查與寫入之間寫成功了，補一份還原 journal（不變式二）。
   同時新增 `skipped-helper-busy`——上一次的 helper 還在跑就不疊第二個驅動呼叫上去。
3. **使用者自己按得到的開關**：`/reports/preview` 的「自動選紙：開／關」
   （`config.json` 的 `printFormPreselect`）。原本唯一的關閉手段是 `CEREMONY_PRINTFORM_EXE`
   環境變數——**寺方按不到**，等於每次現場出事都要遠端協助。按回「開」時**同時清空黑名單**，
   那是現場「驅動換過了，再試一次」的入口。

   ⚠️ **2026-08-11 修：這顆開關原本活不過重開機。** 現場診斷紀錄裡連著三行
   `form-preselect-toggled {"enabled":false}`，中間各夾一次 `formResult: "skipped-disabled"`
   的列印——關掉當次真的有效，重開就沒了，使用者只好再關一次（連關三次）。
   根因不在本模組：bootstrap 每次啟動用出廠種子覆寫 `config.json`，而它是**整包 assign**、
   只手動撿回 `jwtKey`，種子又只有連線五欄 ⇒ `printFormPreselect` 每次開機被清掉。
   修法是 `electron/config-merge.ts` 的 `mergeConfig`（per-key merge，`undefined` ＝沒有意見）。
   **可轉移教訓：止血鍵必須活得比故障久——一個要重開程式才生效、卻在重開時被重設的開關，
   剛好在使用者最需要它的時候失效。** 另補一行 `app-start` 診斷紀錄：現場紀錄原本看不出
   「重開過」，而這類「設定自己跑掉」的第一個問題就是「是不是重開之後」。

**追加（同日，客戶回報「找不到那些檔案」）**：排障 ④「到列印喜好設定改一次紙」也做成按鈕
——「**印表機設定**」直接叫 `rundll32 printui.dll,PrintUIEntry /e /n "<預設印表機>"`。
**覆寫那份壞掉的 DEVMODE 的人是 Windows 自己的 UI，不是我們**：要「修」它就得先讀它，
而讀本身就是一次對驅動的接觸——正是 9d 剛決定不要再做的事。附帶好處是它同時完成排障 ① 的分流：
連那個視窗都開不起來 ⇒ 與本程式無關。

⚠️ **不要把復位動作放進安裝程式**（本次評估過並否決）：`perMachine` 的 NSIS 跑在提權帳號下，
`$APPDATA` 可能指到另一個 profile（一台多使用者時也只清得到一個）；真正該復位的東西
（驅動的每使用者預設 DEVMODE）根本不是檔案；而 `print-form-restore.json` **刪掉比留著糟**
——它是把使用者原本的紙張設定放回去的唯一憑證，正解是啟動時 `recoverPendingFormRestore()` 還原後再刪。
**現場的復位步驟一律要有 UI 入口**，`%APPDATA%` 路徑只寫給遠端協助的人看。

**判準的轉變**：9b 是「只有確定會變好才**寫入**」，9c 是「寫入前先問驅動接不接受」，
9d 把同一條線再往前推一格——**連問都要有額度**。理由是這次的證據顯示，
在某些驅動上「問」本身就有代價，而那個代價（整個程式卡死、只能重啟）遠大於自動選紙的收益。

**可轉移教訓**：*fail-closed 的預檢只保證「我們沒把它寫壞」，不保證「我們沒把它弄卡」。
一個會叫醒第三方行程的檢查，本身就是一次接觸；接觸失敗過的對象要記帳，不能每次重來。*

**⚠️ Windows 實機待複驗**——黑名單的效果（第二次列印起完全不啟動 helper）與
「不 kill 之後 PrintDlgEx 不再卡死」都只有現場答得出來。若仍卡死，
那就指向殘留的每使用者預設 DEVMODE（排障 ④）或純驅動問題，處置見〈待評估：回到 WinForms PrintDialog〉。

### 9e. 排障鈕不得掛在「要先產出預覽」的工具列裡（2026-08-11）

**回報**：使用者裝完 v2.4.6 後說「**工具列沒看到**」。

**根因**：9d 與排障 ④ 的三顆按鈕（診斷紀錄／印表機設定／自動選紙）當初放在
`.preview-toolbar`，而那條工具列整段在 `@if (previewUrl())` 之內——
**要先成功產出一份 PDF 才會出現**。但它們要救的故障情境正好是
「按下列印鈕整個卡死」「印表機叫不出來」，那種時候根本產不出預覽。
換句話說：**這三顆復位鍵在唯一需要它們的情況下必定不可見**。

**處置**：獨立成 `/reports/preview` 頁面上方的 `.trouble-bar`（page-header 之下、
表單列之上；2026-08-11 之前寫作「mode tabs 之上」，該列 tabs 已隨「單筆列印」分頁移除），`@if (isDesktop)` 之外不再有任何前提；預覽工具列裡的重複按鈕移除。
左側選單本來就有「列印預覽」，所以就算主流程整條壞掉也走得到。

**判準**（寫死）：*復位鍵的可見性不得依賴任何會被故障本身破壞的前提。*
「現場的復位動作一律要有 UI 入口」還不夠——那個入口必須在**故障發生時**仍然按得到。

**可轉移教訓**：*把逃生門裝在只有一切正常時才打得開的房間裡，等於沒有逃生門。*

### 8. 順帶修掉「列印完卡在列印中」

**現場回報（2026-08-02）**：列印完成後畫面卡在「列印中」，無法再選下一個列印，
要跳到其他功能再回來才恢復。

根因：`signup-list-page.ts` 的 `printing` signal 沒解除——`finally` 一定會 `set(false)`，
所以真正卡住的是 `await bridge.printPdfBuffer(...)` → `webContents.print(options, callback)`
的 **callback 沒回來**（紙已印出＝job 已進 spooler，這在 Windows 上是已知會發生的），
唯一的出口是 10 分鐘的 `PRINT_TIMEOUT_MS`，逾時後還會誤報「列印逾時（印表機無回應）」。

本次改版讓它由架構消失：`openReportInViewer` 在視窗 `loadFile` 完成就 resolve，
UI 不再與 spooler 的 callback 綁在一起。

### 10. 關掉預覽後把焦點交還主視窗（2026-08-10）

**現場回報**：同時開著其他應用程式時，列印完把預覽關掉，**系統畫面會沉到別的程式後面**，
使用者要自己從工作列點回來。

根因不在我們的程式做錯了什麼，而在**沒有人負責把焦點接回來**：Windows 關閉一個視窗時，
焦點交給 **z-order 的下一個視窗**，而 owner（`parent`）關係只保證子視窗壓在主視窗上面，
**不保證關掉之後焦點回到 owner**；中間還隔了一個 modal 的 PrintDlgEx（它會 disable owner、
關閉時自己再轉一次焦點），焦點鏈更容易斷在別的程式上。決策 1 把預覽做成獨立的子視窗，
這條就是它的連帶代價。

處置：`electron/window-focus.ts` 的 `returnFocusOnClose(child, parent)`，兩個開窗點都掛上——
`print.ts` 的檢視器視窗、`main.ts` 的 `did-create-window`（報表預覽頁自己 `window.open` 的那個）。

**刻意只在三個條件同時成立時才搶焦點**（搶過頭比原本的 bug 更煩人）：

| 條件 | 為什麼 |
|---|---|
| 預覽視窗**關閉當下自己持有焦點** | 使用者若已切到 Word、再從工作列關掉預覽，那是他刻意離開，不該把畫面拉回來 |
| 主視窗未被 destroy | 先關主視窗再關預覽時不要碰它 |
| 主視窗未最小化 | 最小化＝使用者本來就沒在看它，`focus()` 會把它整個叫起來 |

實作上焦點狀態只有 `close` 問得到（`closed` 時原生視窗已銷毀），交還焦點卻必須等 `closed`
（太早呼叫會被視窗銷毀時系統自己的焦點轉移蓋掉）——所以是「`close` 記狀態、`closed` 才動作」。
回歸鎖 `electron-window-focus.spec.ts`（5 條，三個「不搶」的分支各一條）。

**⚠️ Windows 實機待複驗**——macOS 的焦點模型不同（app 層而非視窗層），這條只有現場算數。

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
| Domain | `Ceremony.Domain/Reports/PrinterContactPolicy.cs` | 決策 9d：黑名單比對 + 寫入預算（平台中立、可測） |
| PrintForm | `Ceremony.PrintForm/`（Windows-only exe） | `PrinterSettings.PaperSizes` 名稱比對 + `SetPrinter` Level 9 + 還原 |
| Electron | `electron/api-stream.ts` | `streamApiToFile`（備份下載共用） |
| Electron | `electron/print.ts` | `openReportInViewer` / `openPdfInViewer` / `sweepTempDir` |
| Electron | `electron/print-form.ts` | 子行程呼叫、refcount、還原 journal、失敗印表機黑名單、現場開關（best-effort） |
| Electron | `electron/print-form-core.ts` | 純函式：輸出解析 / 視窗標題 / log 欄位白名單 / `blockScope` / `applyArgs` |
| Electron | `electron/print-log.ts` | 診斷紀錄（寫入 / 輪替 / 過期清理） |
| Electron | `electron/window-focus.ts` | 決策 10：子視窗關閉後把焦點交還主視窗（純條件邏輯、可測） |
| Electron | `electron/main.ts` | `plugins: true`、4 個列印 IPC（含自動選紙開關）、temp/log 清理、開機還原紙張 |
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

## 決策 11：把列印工作拿回來（＝舊系統的形狀）—— Phase 1 已實作

> **Phase 1 實作完成 2026-08-17**（預設仍走舊路徑，等驗收 A 全綠才翻）。
>
> | 層 | 檔案 |
> |---|---|
> | Domain 純函式 | `PrintScalePolicy`（Fit／StretchPhysical）、`PrintPageRange`、`PrintDialogResults` |
> | helper | `PrintDialogNative`（`comdlg32!PrintDlgW` ＋ GDI）、`PdfiumNative`、`DialogPrinter`、`print` 子命令 |
> | Electron | `print-dialog-core.ts`（純）、`print-dialog.ts`（spawn／NDJSON）、`viewer-page.ts`（wrapper 頁）、`viewer-preload.ts` |
> | UI | 報表預覽頁排障列第 4 顆「列印方式：檢視器／對話框」 |
>
> **開關**：`config.json` 的 `printViaDialog`（per-machine，走 `mergeConfig`）。
> 開著時 `applyReportForm()` 一律回 `skipped-dialog-path`（互斥不變式，寫在程式碼裡）。
>
> **實作時修正的兩件事**：
> 1. 「publish 要改資料夾發布」**不必**——`PublishSingleFile` 只打包 managed 組件，
>    native 的 `pdfium.dll` 本來就會留在 exe 旁邊，正是 `DllImport("pdfium")` 要的。
>    改了反而會引入每次啟動的自解壓成本。
> 2. 「再列印一次」（決策 4 的續印）**自動落在 Phase 1**：預覽頁那顆列印鈕本身就是它，
>    對同一份既有 temp PDF 再叫一次 helper，不重跑渲染。原本排在 Phase 2。

## 決策 11 的原始評估（Phase 0）

> 狀態：**2026-08-15 由「只評估」轉為執行**，目前在 **Phase 0（阻斷性驗證）**。
> 完整計畫見規劃檔；探針程式碼在 `backend/src/Ceremony.PrintProbe/`（一次性，不隨產品出貨）。
>
> **觸發事件**：KYOCERA PA2000 GX 在排障 ①③④ 全部走完之後**仍然完全印不出來**
> （`reference/print.png` 顯示列印按鈕呈灰色——先前以為「會噴錯但還能印」是錯的）。
> 現場證據已排除我們的寫入：`reference/print-20260815.log` 兩次列印皆 `formResult:skipped-disabled`
> （helper 連啟動都沒有）、三次 `app-start` 皆 `formPreselect:false`、全檔無 `form-restore-recovered`；
> `reference/print.mov` 顯示客戶在 Kyocera 自己的列印喜好設定選了「資料卡」按確定
> ⇒ **最後一個寫那份 DEVMODE 的是 Windows 自己**。
>
> **⚠️ 修正下表的一個用詞**：本節原寫 WinForms `PrintDialog`（PrintDlgEx）。實作改為
> **直接 P/Invoke `comdlg32!PrintDlgW`**——WinForms 的對話框選擇是框架內部分支
> （`UseEXDialog` × OS × 位元數），舊系統是靠「AnyCPU 實際跑 32-bit」這個**偶然**走到 legacy 的；
> 把偶然搬進新程式＝換個地方踩同一顆雷。且 `PrintDlgExW` 是**新版**對話框，正是現場出錯的那一條。
> 另見 [../gotchas.md](../gotchas.md)〈越新的 .NET 印表機 API 綁得越死在壞掉的那一層上〉。

決策 9 系列的每一個問題都源自同一件事：**我們不擁有那個列印工作**，所以只能去改機器的共用設定。
舊系統擁有它，所以完全沒有這一整類失敗模式。要根治就得把送印拿回來，做成 WinForms helper：

```
API 產 PDF → helper 光柵化成頁面圖 → PrintPreviewDialog
           → 使用者按列印鈕 → PrintDialog（帶我們自己的 hDevMode）→ printDocument.Print()
```

**關鍵發現：2026-08-02 放棄「程式送印」的理由，在這個方案上不成立。** 當時的比較是
「**自建對話框** vs **Chromium 檢視器**」，自建對話框輸在沒有「印表機內容」按鈕。
但舊系統用的不是自建對話框，是**系統的 `PrintDialog`（PrintDlgEx）**，內容按鈕一應俱全——
當時其實是三選一漏了一個：

| 選項 | 原生「內容」 | hDevMode | 會不會噴 `0x80010105` |
|---|---|---|---|
| 自建對話框（v2.3.7，已棄） | ❌ | 自己組 | — |
| Chromium 檢視器（現行） | ✅ | **null → 讀每使用者預設** | 會 |
| **WinForms PrintDialog（＝舊系統）** | ✅ | 自己那份（非 null） | 機制上不會 |

這條路一次拿掉：`SetPrinter` 整段、還原 journal、`0x80010105` 這一類，
而「自動選對紙」變成免費附贈（就是 `SignupForm.cs:1770-1787` 那個比對迴圈）。

**入場費（三項，2026-08-15 全部重新定價）**：

1. ~~整份座標表會再次作廢~~ —— **這一項可以不付。** 原本的定價假設新路徑照舊系統
   `DrawImage` 拉滿 `PageBounds`（非等比）。但我們**不必那樣做**：光柵化之後自己用
   `scale = min(HORZRES/pageW, VERTRES/pageH)` 等比縮放並置中於可列印區，
   就是複製現行 Chromium 的行為 ⇒ 現有 ±0.05cm 座標表**在構造上不作廢**。
   ⚠️ 但「構造上不作廢」≠「已驗證不作廢」——**六報表對照組（同機、同紙、同資料，
   兩條路徑各印一張疊放量測）仍是不得跳過的驗收項**，這正是 2026-08-01 那條元教訓的要求：
   「上線前必須做 X」的 X 必須是可勾選的驗收項。
2. **向量變點陣** —— **這一項比原本貴。** 原句寫「舊系統就是這樣」是**錯的**：
   舊系統 render 出來的是 **EMF 向量中繼檔**，`DrawImage(metafile, …)` 在印表機 Graphics 上是
   **中繼檔重播**（見 [../gotchas.md](../gotchas.md) 該條 2026-08-15 更正）。
   ⇒ 光柵化是**新方案獨有的新風險**，不能用「回到舊行為」安慰自己。
   減價手段：用 PDFium 的 `FPDF_RenderPage(HDC…)` 而非 `FPDF_RenderPageBitmap`——
   HDC 為 `DT_RASPRINTER` 時 PDFium 走 GDI 印表機裝置驅動，以圖元送進 DC 而非攤成大點陣圖。
   驗收必須包含造字（增補平面）與細筆畫的樣張比對。
3. **終局要取代不能並存** —— 成立，但**不是上線第一步**：驗收 A 的對照組本身就要求
   同一台機器能跑兩條路徑，所以 Phase 1 需要一個 per-machine 開關（`printViaDialog`，
   走 `mergeConfig` 以免重蹈 v2.4.8「開關被出廠種子清掉」）。那是**驗收工具**不是並存策略，
   Phase 3 一併刪除。



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
