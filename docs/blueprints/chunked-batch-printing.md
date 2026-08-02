---
title: 大量列印分段（Chunked Batch Printing）
purpose: （已廢止）曾用分段解決大量列印的卡紙續印；2026-08-02 改由 Windows 原生列印對話框的「頁面範圍」承接
status: superseded
applicable_when: 要改批次列印流程、要調段大小、大量列印出問題、要加續印能力時
related_agents:
  - frontend-architect
  - backend-engineer
related_docs:
  - print-channel-electron.md
  - printing-reports.md
  - api-endpoints/post-reports-batch-plan.md
  - api-endpoints/post-reports-batch-jobs.md
  - ../design/performance.md
  - ../design/frontend-design.md
  - ../gotchas.md
keywords: [大量列印, 分段, chunk, 續印, 重印, 暫停, SEGMENT_SIZE, 2GB, PdfSharp, spooler]
last_updated: 2026-08-02 (**整份廢止**：分段機制連同 batch/plan 端點、ChunkedPrintService、分段面板全部移除，理由見下方「為什麼廢止」；本檔保留為決策史)
---

## ⚠️ 已廢止（2026-08-02）

本文描述的分段機制**已整個移除**。現在的大量列印是「合併成一份 PDF → 開預覽視窗 →
使用者按原生列印對話框」，見 [print-channel-electron.md](print-channel-electron.md)。

### 為什麼廢止

分段真正解的是**卡紙續印**（下方客訴原文），不是記憶體——記憶體只是順帶。
改走 Windows 原生列印對話框之後，**「頁面範圍」欄位把續印能力補了回來**：
卡在第 3000 張時預覽視窗還開著，再按一次列印鈕填 `3000-` 即可。
這比分段面板更接近舊系統（舊系統 `CombinePDFs` 就是全部合成一份），操作也更直覺。

2 GB 的技術天花板改由**後端串流落檔**解決（`IPdfMerger` 改吃檔案路徑、job 成品存暫存檔），
也就是本文「不做什麼」原本說「分段後沒有收益」的那件事——取消分段後那個前提消失了。

### 已移除的東西

`core/reports/chunked-print.service.ts`、`chunked-print.types.ts`、
`shared/batch-print-panel/`、`POST /reports/batch/plan`（及其 blueprint）、
`SEGMENT_SIZE`、段狀態機、暫停／單段重印。

以下內容保留為決策史。

---

## 背景與動機

**客訴（2026-07-31）**：使用者實際一次列印 1000–5000 筆，「印到一半卡紙就要整批重印，很痛苦」。

追下去發現整條管線每一段都是 O(N) 一次性：

| 段落 | 原本的做法 | 大量時的後果 |
|---|---|---|
| `BatchReportComposer.Render` | `List<byte[]>` 累積**全部**單筆 PDF | 15000 筆 ≈ 2 GB 純資料在記憶體 |
| `PdfSharpMerger.Merge` | 全部 `PdfReader.Open` → `MemoryStream` → 再 `.ToArray()` 複製一份 | 合併瞬間峰值 ≈ 成品的 3–4 倍 |
| `BatchPrintJob.Pdf` | 成品 `byte[]` 常駐 job | 再一份 |
| Electron `printPdfFile` | 整份載進隱藏 BrowserWindow | 幾千頁的 Chromium 渲染 |
| `webContents.print` | **一個 spooler job** | 第 3000 張卡紙 → 整批重來 |

實測基準（[performance.md §8](../design/performance.md)）：**799 筆 datacard = 107 MB**
（約 134 KB/筆），render 5.4s + merge 1.6s；**19018 筆時 PdfSharp 丟
`System.IO.IOException: Stream was too long`**（2 GB `MemoryStream` 上限）。

技術天花板是 2 GB，但**真正痛的是最後一列**——那是操作問題，不是效能問題。

## 決策

### 1. 分段，而不是把單一 PDF 做大

把「一個 N 筆的 job」換成「⌈N/K⌉ 個 K 筆的 job」，逐段渲染、逐段送印。

連鎖好處：
- 峰值記憶體從 O(N) 變 **O(K) 固定值**，與總筆數無關 → 2 GB 上限自動消失
- **每段都能預覽**（大檔略過預覽的限制隨之解除）
- **可續印**：第 7 段卡紙就重印第 7 段 ← 這才是客訴的痛點
- 進度變成「已送印 1400 / 5000」而非「已渲染」——後者印壞了還顯示 100%
- 一段一個 spooler job，印表機不會被單一巨大 job 塞住

**因此後端串流化（`Merge` 改吃 `Stream`、job 存暫存檔）變成不必要的工作**：每段只有
200 筆 ≈ 27 MB，`List<byte[]>` + merge 峰值約 100 MB，完全安全。後端只多一個查詢端點。

### 2. 分段做在前端

後端只新增 [`POST /reports/batch/plan`](api-endpoints/post-reports-batch-plan.md)
（複用 `ResolveAsync`，回有序的 `{id, number}` 清單，不渲染）。

- 暫停、單段重印、逐段預覽都需要逐段控制，後端做分段仍得把「段」的概念傳到前端。
- 選取的**權威仍在後端**：前端不重查一次，否則「畫面說 500 筆、實際印出 498 筆」。
- 段大小 200 是前端常數，改它不必動後端。

### 3. `SEGMENT_SIZE = 200`

三個理由收斂到同一個值：

| 理由 | 說明 |
|---|---|
| 記憶體 | 200 筆 datacard ≈ 27 MB 成品、峰值 ~100 MB |
| 預覽 | 與 `PrintService` 的預覽門檻同值 → 每段都在可預覽範圍內 |
| **SQL 參數上限** | `SearchByIdsAsync` 的 `WHERE SignupID IN @Ids` 經 Dapper 展開成 N 個參數，**SQL Server 上限 2100**。分段之前，勾選 3000 筆列印會直接炸在這裡（既有潛在缺陷，分段順帶修掉） |

### 4. 列印設定只問一次，且帶第一段的預覽

第一段渲染完 → 跳列印對話框（含該段 PDF 預覽）→ 使用者確認印表機／份數／縮放 →
**其餘所有段沿用同一組設定**。25 段跳 25 次對話框是不能接受的。

在對話框按取消 = 整批不印（不是只跳過第一段）。

### 5. 「已送印」不是「已完成」

`webContents.print` 的 callback 只代表 job 已交給 spooler——紙上有沒有字是之後的事。
所以段狀態叫 `printed`（顯示「已送印」），且**面板永遠保留每段的重印鈕**，
連全部跑完之後也是。面板不自動關閉，等使用者對照實體紙張後自己按「關閉」。

### 6. 暫停 = 送完當前段就停

已交給 spooler 的段停不了（那是印表機的事），能控制的只有「不要再送下一段」。
所以 `pause()` 只在段邊界生效，目前正在渲染／送印的那一段會做完。

### 7. 重印重新渲染，不快取

每段印完立刻釋放 blob（同時只有一段在記憶體，峰值才與總筆數無關）。
重印時重新建 job 重新渲染（200 筆約 1.7s）——順帶確保印的是最新資料。

## 資料流

```
PrintService.printBatch(req)
  │
  ├─ 非 Electron → 既有的單一大 PDF 開新分頁（dev 環境，不做分段）
  │
  └─ POST /reports/batch/plan → { total, items: [{id, number}] }   ← 選取權威
       │
       ├─ total ≤ 200 → BatchPrintService.run（ProgressOverlay + 列印對話框）
       │                 體驗與改版前完全相同，不開分段面板
       │
       └─ total > 200 → ChunkedPrintService.start(req, items, …)
             │            buildSegments() 依 SEGMENT_SIZE 切段
             │            BatchPrintPanelService.open(run) 顯示分段面板
             │
             └─ 逐段（drive 迴圈，每段之間檢查 phase）
                  POST batch/jobs { signupIds: 該段 }
                  → 輪詢 250ms → GET …/file
                  → 第 1 段：列印對話框（含預覽）取得 choice
                  → ceremony:printPdfBuffer(type, bytes, choice, pageSizeHeader)
                  → 段狀態 printed，釋放 blob
```

段狀態機：`pending → rendering → printing → printed | failed | canceled`

## 檔案

| 層 | 檔案 | 職責 |
|---|---|---|
| Api | `Controllers/ReportsController.CreateBatchPlan` | 複用 `ResolveAsync`，回清單不渲染 |
| Application | `Reports/BatchReportComposer.cs` | `BatchReportPlanResponse` / `BatchReportPlanItem` DTO |
| Renderer | `core/reports/chunked-print.service.ts` | 分段狀態機、暫停／續印、`buildSegments`、`SEGMENT_SIZE` |
| Renderer | `core/reports/chunked-print.types.ts` | 段狀態模型、`segmentLabel`、`printedCount` |
| Renderer | `shared/batch-print-panel/` | 分段進度面板（段清單、總進度、暫停／繼續／重印） |
| Renderer | `core/print/print.service.ts` | plan-first 分流：≤200 走單段、>200 走分段 |

## 因為分段而移除的東西

分段之後每次送印最多一段（≈27 MB），IPC 傳 bytes 已無成本問題，**「主行程自己去 sidecar
串流取檔」整條路徑變成永遠不會被執行的死碼**，全部移除：

- `electron/print.ts` 的 `printReport` / `printBatchJob`
- `ceremony:printBatchJob` IPC（`main.ts` handler、`preload.ts`、`CeremonyBridge` 型別）
- `BatchPrintService` 的 `takeFile` 選項、`BatchPrintJobHandle`、`isJobHandle`

`electron/api-stream.ts` 的 `streamApiToFile` **保留**——備份下載（`download.ts`）仍在用，
2026-07-31 補的 mkdir 修正也仍然有效。

## 不做什麼

- **後端串流化合併**（`Merge` 吃 `Stream`、job 存暫存檔）：分段後每段只有 27 MB，
  這層工作沒有收益。若日後段大小要拉到數千筆才需要重新評估。
- **段與段之間 pipeline（邊印邊渲染下一段）**：200 張紙以 30ppm 算要印 7 分鐘，
  渲染只要 1.7s——重疊的收益不到 0.5%，不值得換來兩個 job 並行的複雜度
  （也會撞到 `MaxRunningPerOwner = 2`）。
- **自動重試失敗的段**：卡紙需要人去處理，自動重送只會多印一疊廢紙。

## 物理現實（不是軟體問題）

5000 張紙以雷射機 30ppm 算約 **2.8 小時**，薦牌那種窄長紙更慢。分段解決的是
「中途出事不用從頭」，不會讓列印變快。

`AccessTokenMinutes = 600`（10 小時）> 列印時長，所以 token 不會中途過期。
但若使用者已登入 9 小時才開始印，後半段會 401——屆時重新登入後可用「重印」補印未完成的段。

## 待驗證（Windows 實機）

1. **`SEGMENT_SIZE = 200` 是否要調**：量 200 筆的「取檔 → IPC 傳 bytes → 寫 temp」實際延遲
   與記憶體峰值。太慢就調小；綽綽有餘就調大（使用者當然希望愈多筆愈能一次預覽）。
2. **連續 25 段送印的 spooler 行為**：段與段之間會不會被驅動當成不同工作而插入分隔頁；
   段之間是否需要加一點間隔避免佇列塞爆。
3. **暫停／重印在實機的體感**：卡紙 → 暫停 → 排除 → 繼續，全程是否順手。
4. **段標題的編號範圍是否對得上實體紙張**（`ORDER BY Number` 與實際出紙順序一致）。
