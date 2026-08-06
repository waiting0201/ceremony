---
title: GET /api/v1/reports/tablet
purpose: 產生單筆薦牌（牌位）PDF，11.5×25.5cm 窄長預印紙套印；9 變體自動選擇；含現場對位校正版
status: shipped
endpoint: get-reports-tablet
http_method: GET
route: /api/v1/reports/tablet
legacy_form: SignupForm.cs
legacy_lines: 1148-1333 (PrintTablet) + 559 (編號組法) + 244-262 (右鍵入口)
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../../design/api-design.md
  - ../../design/backend-design.md
  - ../printing-reports.md
  - ../printing-reports-positions.md
  - ../legacy-coverage/signup-form.md
keywords: [薦牌, tablet, 牌位, 列印, 套印, PDF, debugGrid, debugOverlay, 對位校正, 變體, ParaFontSize]
last_updated: 2026-08-06 (建檔。補上這支長期缺的 forward 藍圖〔規則 10〕，並記載同日新增的 `debugGrid` 現場對位校正版——起因是客訴「四位往生者的字壓到預印的靈位」，但依現行座標算 3+ 位矩陣下緣最遠只到 13.1946cm、離樣板量到的「靈」上緣 13.462cm 還有 0.27cm 餘裕，`MatrixLayout` 又保證不超框、回掃 13.144 印證 → 算出來的餘裕與實印矛盾，代表**量測基準或送印路徑**有問題，需要現場刻度反推。`debugGrid` **刻意不做 Development 阻擋**，與 `debugOverlay` 相反。**同日結案**：客訴根因確認為 2 位分支 `avail=6.31`〔客戶用全形空格把兩個人名塞在同一格＝8 個字素，走 2 位分支不是矩陣〕，三種排法下界收斂成單一 `DeadTextBottom=13.1946`、字級改自動縮到剛好、`RealCharCount` 移除)
---

## 規格

### Route + Method

`GET` `/api/v1/reports/tablet`

### Request（query）

| 參數 | 型別 | 必填 | 說明 |
|---|---|---|---|
| `signupId` | `Guid` | ✅ | 報名 id |
| `debugOverlay` | `bool` | – | 疊 `reference/template/薦牌.jpg` 樣板掃描照。**僅 `Development`**，其他環境整支回 404（不是 403，避免洩漏功能存在） |
| `debugGrid` | `bool` | – | **現場對位校正版**：同一筆真實資料再疊 1cm 桃紅刻度格線。**所有環境可用**（見下方「為什麼不 dev-gate」） |

### Response

`200 application/pdf`

| Header | 值 |
|---|---|
| `Content-Type` | `application/pdf` |
| `Content-Disposition` | `attachment; filename="tablet-{Year}-{NumberTitle}-{Number}.pdf"`；`debugGrid=true` 時檔名多 `-calibration` 尾綴 |
| `X-Report-Page-Size` | `115000x255000`（微米；權威值在 `Ceremony.Domain.Reports.ReportPageSizes`） |

### 錯誤碼

| HTTP | errorCode | message | 觸發條件 |
|---|---|---|---|
| 401 | – | – | 無 JWT |
| 404 | `SIGNUP_NOT_FOUND` | 找不到報名 | `signupId` 不存在 |
| 404 | – | – | `debugOverlay=true` 且非 `Development` |

詳見 [api-design.md 業務錯誤碼表](../../design/api-design.md)。

## 舊系統對照（規則 A — forward）

### 對應 Form / 事件

| 舊方法/事件 | 舊 code line ref | 行為摘要 |
|---|---|---|
| `SignupForm.tsmiPrintTablet_Click` | `SignupForm.cs:244-262` | 右鍵「列印薦牌」→ 取選取列 → `PrintTablet` |
| `SignupForm.PrintTablet` | `SignupForm.cs:1148-1333` | 依亡者/陽上人數挑 `tmpTablet*.rdlc` 9 變體之一、算 `ParaFontSize`、render 成點陣圖後 `DrawImage` 拉滿 `PageBounds` 送印 |
| `SignupForm.btnPrint_Click`（批次路徑） | `SignupForm.cs:559` | 薦牌編號組法：`SignupType==2`（寺方）只印 `NumberTitle`，否則 `NumberTitle`+避4號碼 |

### 驗證規則對照

| 欄位 | 舊驗證 (line) | 新驗證 | 差異 / 取捨 |
|---|---|---|---|
| 選取列 | `SignupForm.cs:244` `SelectedRows.Count > 0` | `signupId` 必填，查無回 404 `SIGNUP_NOT_FOUND` | 等價（單筆化；多筆走 `POST /reports/batch/jobs`） |
| `SignupType` | `:559` 寺方只印抬頭 | `SignupReportContext.TabletTextNumber` | 逐字等價 |

### 業務邏輯區塊

1. **變體選擇（9 個 rdlc）**（舊：`SignupForm.cs:1150-1210`）
   - 舊行為：三段 if/else 逐**槽位**判定亡者/陽上是 1 位／2 位／其餘，組合成 9 個變體。
   - 新實作：`PrintTemplateSelector.ChooseTablet` + `SlotTier`。
   - 差異：**slot-based 不是 count-based**——名字填在後面欄位（中間有空洞）一律落 fallback 變體逐槽全畫。2026-07-18 客訴根因正是誤實作成 count-based 導致有空洞時往生者沒印出來。
2. **`ParaFontSize`（往者字級起點）**（舊：`SignupForm.cs:1179/1203`）
   - 舊行為：1／2 位往者且名字 `Trim().Length > 7` 時把 `ParaFontSize` 調小。
   - 新實作（2026-08-06 起）：`ChooseTablet` 只回**字級起點**——1、2 位一律 `"0.8cm"`、3+ 位 `"0.6cm"`，**不再看字數**；實際字級由 `VerticalText.GroupFontPt` / `MatrixLayout` 在可用高內自動等比縮（8 字素 → 0.7cm、12 字 → 0.47cm）。
   - 差異：**舊系統的字數門檻整條不再有對應規則**。中途（2026-07-21～08-06）曾以 `RealCharCount > 7 → 0.5cm` 復刻它並刻意偏離 legacy（不計中間空格），但客訴實測證明**字數門檻擋不住溢出**——客戶那筆 8 個字素只有 6 個真字、門檻沒觸發卻已壓到「靈位」。溢出改由可用高（`DeadTextBottom`）唯一負責，`RealCharCount` 已移除。
3. **座標**（舊：`tmpTablet*.rdlc` XML）
   - 座標權威表在 [printing-reports-positions.md §3-11](../printing-reports-positions.md)，歷經多輪實體套印客訴修正，**不在本檔重複**。
4. **送印**（舊：`SignupForm.cs:1737-1762`）
   - 舊行為：RDLC → 點陣圖 → `DrawImage` 非等比拉滿整張紙，所以驅動載什麼紙都「不用調」。
   - 新實作：QuestPDF 直接產 11.5×25.5cm 的 PDF，紙張在產 PDF 那一刻定案（＝舊 `DeviceInfo` 的位置），送印交回 Windows 原生對話框。
   - 差異：1:1 送印後，驅動自訂表單尺寸若 ≠ PDF 頁面尺寸，`fit-to-printable-area` 會讓**整份**位移／縮放——這正是 `debugGrid` 第一步要先排除的事。見 [gotchas.md](../../gotchas.md)。

### 邊界 case

| 場景 | 舊 code 行為 (line) | 新版行為 | 對應測試 |
|---|---|---|---|
| 第 6 位亡者／陽上 | `:1150-1210` 靜默丟字 | 逐槽全畫（修正 legacy 缺陷，business-rules-implicit §18） | `Tablet_Base_SixthDeadAndLiving_AreRendered` |
| 增補平面造字（surrogate pair） | 點陣圖路徑不受影響 | `VerticalText` 一律以**字素**為單位 | `VerticalText` 測試 + `RendererSmokeTests` |
| 1／2 位往者長名 | `:1179/1203` 依字數縮字 | **不看字數**；起點 0.8cm，renderer 依可用高自動縮到剛好 | `Tablet_1and2Dead_LongNames_AutoShrinkToFit_DumpOverlays` |
| 一格內用全形空格塞兩個人名（現場常見用法） | 同上（字數門檻算不到空格） | 走 2 位分支，字素數（含空格）決定字級；下緣鎖在 `DeadTextBottom` | `Tablet_AllLayouts_DeadNames_NeverCrossDeadTextBottom` |
| `OneOne` 變體有 2cm Page Margin | RDLC 內建 | 三處共用座標常數要扣 `marginCompensation` | `Tablet_DebugOverlay_DumpsCalibrationPdf(OneOne)` |

## 現場對位校正版（`debugGrid`，2026-08-06）

**為什麼不 dev-gate**（與 `debugOverlay` 相反的刻意決定）：它是要在**客戶現場**的 Windows 機器、實體薦牌紙、實體牌位座上使用的量測工具。擋在 `Development` 等於這個工具不存在——開發機上沒有那張紙，也沒有那個牌位座。`debugOverlay`（疊樣板掃描照）沒有這個需求，維持 dev-only。

**為什麼做它**：客訴「往生者的字壓到預印的靈位」當下無法從座標推出重疊（誤判成走 3+ 位矩陣，而矩陣數學上有界），因此先做量測工具反推基準。**後來拿到客戶實印照片，根因確認是 2 位分支的 `avail=6.31`，與送印路徑無關**（見「開放問題」）。校正版仍保留：常見情況的往者下緣普遍只離邊界 0.18~0.23cm，送印端若有整份位移就會整批壓字，值得一次量清楚。

**接線**：`ReportsController.Tablet` → `GenerateTabletHandler.HandleAsync(..., debugGrid)` → `IReportRenderer.RenderTablet(model, debugOverlay, debugGrid)` → `TabletRenderer.Render(data, debugGrid)`（renderer 這層 2026-07-03 就有，一直沒有現場入口）。前端入口是報名維護清單右鍵「列印薦牌（對位校正）」，`reportType` 仍是 `'tablet'`——**校正版若用錯紙張，量出來的刻度沒有意義**。

量測步驟與判讀見 [printing-reports.md](../printing-reports.md)「現場對位校正版」。

## 業務規則

- [business-rules-implicit.md](../../business-rules-implicit.md) §18：第 6 位亡者/陽上必印（修正 legacy 靜默丟字）
- [glossary.md](../../glossary.md)：「薦牌」「陽上」「往生者」「堂號」

## 資料存取

| Table / View | 用途 | 注意 |
|---|---|---|
| `SignupView` | 單筆讀取（`ISignupRepository.GetByIdAsync`） | 堂號等欄位有 `COALESCE` 回退，清空要存空字串不是 null |

不寫入任何資料表（純讀）。

## 驗收標準

- [x] 規格段所有欄位有型別 + 驗證 + header
- [x] 舊系統對照表對到舊 code line ref
- [x] 錯誤碼與舊 MessageBox 文字 verbatim（`SIGNUP_NOT_FOUND`「找不到報名」）
- [x] 回歸測試：`GET_tablet_realSignup_returns_PDF`、`GET_tablet_debugGrid_returns_calibration_PDF`、`GenerateTabletHandlerTests`、`RendererSmokeTests` 17 支 Tablet smoke
- [ ] 對應的 [legacy-coverage/signup-form.md](../legacy-coverage/signup-form.md) 行勾選（`PrintTablet` 段已勾，本檔補的是 forward 藍圖）
- [ ] **實體套印複驗**：修正後的往者下界（薦牌）＋ 字級起點改 0.8cm 後的資料卡長名

## 開放問題

- ~~**往者可用區的真實下界未知**~~ — **2026-08-06 已結案**。客戶實印照片逐像素重現後確認：客訴那筆是 **2 位往生者、每格用全形空格塞兩個人名**（8 個字素）走 2 位分支，根因是該分支可用高用 RDLC 遺留值 `6.31`（下緣 13.89，越過「靈」上緣）。三種排法的下界已收斂成單一 `TabletRenderer.DeadTextBottom = 13.1946`（＝3+ 位矩陣沿用至今、生產無客訴的框底），矩陣數值不變。
- ~~**縮字改成「自動縮到剛好」**~~ — **已實作**：`ChooseTablet` 撤掉 `>7 真字 → 0.5cm` 分支，1/2 位一律 `0.8cm` 起點，`RealCharCount` 一併移除。⚠️ **資料卡同源需一併實體複驗**。
- **剩餘（非阻塞）**：送印端是否有整份縮放／位移，用 `debugGrid` 校正版量相鄰刻度是否 1.00cm 即可判定。常見情況的往者下緣普遍只離邊界 0.18~0.23cm，值得一次量清楚。
