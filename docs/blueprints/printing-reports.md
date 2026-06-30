---
title: 列印與報表
purpose: 5 種報表（資料卡 / 收據 / 薦牌 / 文牒 / 普桌）+ 19 個 RDLC 變體 + PDF 合併 + Excel 匯出；以 QuestPDF 取代 RDLC
status: draft
applicable_when: 要新增/修改報表版面、調整列印流程、處理字型、加列印類型
related_agents:
  - software-architect-blueprint
  - backend-engineer
  - visual-design-architect
related_docs:
  - ../design/visual-design.md
  - ../design/api-design.md
  - signup-management.md
  - printing-reports-positions.md
keywords: [print, 列印, 報表, RDLC, QuestPDF, 資料卡, 收據, 薦牌, 文牒, 普桌, PDF, NPOI, ClosedXML, 位置, position]
last_updated: 2026-06-30 (薦牌/文牒第 6 位往生/陽上已實作＋回歸測試＋影像驗證)
---

## 背景與動機

舊系統有 **19 個 RDLC 模板** 涵蓋 5 種報表，加上 PDF 合併（PDFsharp）、垂直地址圖片（GDI+ DrawText）、堂號拆分、避 4 顯示等複雜邏輯。

> ✅ **技術選型決策定案（2026-05-27 PoC 結論）：走 QuestPDF 路徑**
>
> ### 決策歷程
> - **v1（2026-05-26）**：規劃 QuestPDF 重畫所有 19 個模板（664 行 [printing-reports-positions.md](printing-reports-positions.md) 規範完成）
> - **v2（2026-05-27 上午）**：客戶提出「如果可行，可直接 copy 舊 RDLC 使用」，啟動 PoC
> - **v3（2026-05-27 下午）PoC 結論**：RDLC 重用**不可行**，定案 QuestPDF；已 ship `GET /api/v1/reports/datacard`（第一個 PoC endpoint）
>
> ### PoC 評估結果
>
> | 項目 | RDLC 重用 | QuestPDF 重畫（本系統採用） |
> |---|---|---|
> | **.NET 10 套件可用性** | ❌ `AspNetCore.Reporting` 2.1.0 在 .NET 10 載入時 throw `FileNotFoundException: System.Security.Permissions`（依賴 .NET Framework GDI+） | ✅ `QuestPDF` 2026.5.0 跨平台 .NET 10 native |
> | **授權成本** | ❌ AspNetCore.Reporting 商用 $500-2000/yr (per trial) | ✅ Community License 免費（寺方營收 < $1M USD/yr 適用） |
> | **跨平台** | ❌ 多數套件 Windows only | ✅ macOS / Linux / Windows / Docker 全跑 |
> | **字型相依** | ❌ 需在部署機安裝標楷體 + 修改 RDLC XML | ⚠️ 同樣需在部署機有標楷體；但無需改 XML（純 C# code） |
> | **位置精度** | 保留原 RDLC 1:1 | ✅ QuestPDF 支援 cm 單位 + TranslateX/Y 絕對定位，1:1 對位 |
> | **dashed line 支援** | RDLC 內建 | ✅ QuestPDF 2026 收回 SkiaSharp 公開 Canvas API，改用 `SkiaImageHelpers.DashedLine` 自繪虛線 PNG 再 embed（2026-05-29 解決） |
> | **維護性** | XML 編輯（停產的 ReportBuilder） | ✅ C# code review、版控、單元測試 |
> | **PoC 實證** | 無法 build | ✅ `/api/v1/reports/datacard` 已 ship，產 34KB PDF 含真實資料 |
>
> ### 採用範圍
>
> - **19 個 RDLC 模板全部用 QuestPDF 重畫**
> - 座標 / 字級 / 變體選擇仍以 [printing-reports-positions.md](printing-reports-positions.md) 為 ground truth（雙路徑共用設計，現實際走 QuestPDF）
> - 首個 PoC：`tmpDataCard.rdlc` → `Ceremony.Infrastructure.Reporting.DataCardRenderer`（已 ship）
> - 後續 18 個（Receipt / 9 Tablet 變體 / 2 Text 變體 / 6 Worship 變體）逐個實作；變體選擇邏輯 ([SignupForm.cs:1148-1696](../../reference/old/Ceremony/SignupForm.cs#L1148)) 用 Domain Service 實現
>
> ### 已知 trade-off / TODO
>
> - ~~**Dashed line**：QuestPDF 2026 不直接支援；目前 PoC 用 solid~~ ✅ **已解決（2026-05-29）**：`SkiaImageHelpers.DashedLine` 產虛線 PNG 再 embed（見「列印資料套入」段）
> - **印表機實機對位**：需客戶在預印格式紙上實際列印 1 張薦牌做最終驗收（PoC 已產出 PDF，可印；deferred 等客戶提供印表機環境）
> - **效能**：QuestPDF 渲染 1 頁 ~50-200ms，可接受；大批次合併 PDF（如報名清單列印）用 `PdfMerger` 或同 QuestPDF

## 列印資料套入（**2026-05-29 完成 4 項 + 關鍵 bug 修正**）

「列印資料還沒套入」的根因其實是一個 QuestPDF bug（見下方），順帶完成 4 項版面精修。

### 🐛 關鍵 bug：QuestPDF `.Height()` + 預設 line-height 靜默裁字

5 個 renderer 原本用 QuestPDF `.Height()` 把每個 text box 夾成 RDLC 的精確高度；但 QuestPDF 預設 line-height ≈ 1.2–1.5× 字級**超過**那些貼緊的高度，QuestPDF 遇放不下的文字會**靜默裁切 / 丟棄**（不報錯）。結果 Number、所有 label（亡者:/陽上:/地址:/電話:）、phone、prepay、簽名 label 等**沒印出**（pdftotext 驗證修正前 ~17 欄位只有 4 個有渲染）。

**修正（5 個 renderer 全改）**：
- 不再用 `.Height()` 夾文字；VerticalAlign=Middle/Bottom 改用 **translate Y offset** 模擬
- 每個 text span 設 `.LineHeight(1f)`
- 修正後全欄位正確渲染（pdftotext + pdftoppm 影像雙重驗證）

> 詳見 [gotchas.md](../gotchas.md)「QuestPDF `.Height()` + 預設 line-height」條。**新做任何精確套印的 text，一律 `LineHeight(1f)` + translate 控高，不用 `.Height()` 夾。**

### 完成的 4 項

1. **worship2.png 背景嵌入**（WorshipRenderer）：worship2.png copy 到 `Ceremony.Infrastructure/Reporting/Assets/worship2.png` 設 `EmbeddedResource`，載一次當底層繪製（Top 0.26141 Left 0.42 W 20.04729 H 28.88438 cm，FitProportional）；Worship Number 改置中（TextAlign center）且不再被裁。
2. **Tablet 9 變體座標**（TabletRenderer）：依 `TabletTemplate` 切換；座標由 9 個 `tmpTablet*.rdlc` XML **權威抽出**（含 Tablix→cell→Rectangle 巢狀絕對座標計算）；`tmpTabletOneOne` 用 Page Top/Bottom margin 2cm；DeadName 字級來自 `ParaFontSize`。
3. **Text 2 變體 + PhotoAddress 垂直地址**（TextRenderer）：依 `TextTemplate`（Base / Two）切換；DeadName 座標取自 `tmpText.rdlc` / `tmpTextTwo.rdlc` 的 Rectangle2（絕對 = Rect 原點 + 相對）。PhotoAddress 改為真正的 **25×605px 垂直地址 PNG**，由新 `SkiaImageHelpers.VerticalAddress` 產生（1:1 移植舊 [Commons/Library.cs:34-124](../../reference/old/Ceremony/Commons/Library.cs#L34-L124) System.Drawing → SkiaSharp）：中文直排、`[a-zA-Z0-9\-\(\)]` 旋轉 90°；嵌入 Top 4.1 Left 25.4 W 0.66 H 16.8 FitProportional。
4. **DataCard 虛線**（DataCardRenderer）：虛線（Line2）改為真虛線 PNG（新 `SkiaImageHelpers.DashedLine`，先前因 QuestPDF 2026 收回 SkiaSharp Canvas API 而以 solid 替代）；實線簽名線（Line1）不變。

### SkiaSharp 採用（補 QuestPDF 缺口）

QuestPDF 2026 收回公開 SkiaSharp `Canvas` API，像素級自繪（旋轉文字、虛線）改走新 helper `Ceremony.Infrastructure/Reporting/SkiaImageHelpers.cs`（`VerticalAddress` + `DashedLine`，SkiaSharp 3.119.4 早已被引用）產 PNG 再 embed。**取代了原本「QuestPDF 直接畫垂直文字、虛線」的規劃**（見下方「技術選型」表已更新）。

### 座標抽取方法（authoritative）

variant-specific 座標**以 RDLC XML 為唯一權威**逐張抽出，含 Tablix → cell → Rectangle 的巢狀「絕對座標 = 父原點 + 相對位移」計算，非目測。

### 字型註冊（**2026-05-29 修正：必須顯式註冊，否則 silently fallback**）

QuestPDF **與** SkiaSharp **都**需要標楷體。**關鍵踩雷**：renderer 用 `FontFamily("BiauKai")`，但 macOS 的 BiauKai.ttc 內部家族名其實是「標楷體-繁 / BiauKaiTC」，並非 "BiauKai" → SkiaSharp 找不到 → **silently fallback 到 PingFang TC**（`pdffonts` 實測）。PingFang 字寬與標楷體不同，導致薦牌/文牒直書字寬、文字尺寸、位置全跑掉（這就是「薦牌尺寸有問題」真因，**非座標錯**——座標經 RDLC 逐一比對皆正確）。

**修正**：新增 [ReportFonts.cs](../../backend/src/Ceremony.Infrastructure/Reporting/ReportFonts.cs)，啟動時（`AddCeremonyInfrastructure`）以 `FontManager.RegisterFontWithCustomName("BiauKai", stream)` 把 OS 的標楷體檔註冊成自訂家族名 "BiauKai"，所有 renderer 即穩定解析。字型來源依序：
1. 環境變數 `CEREMONY_KAI_FONT`（部署明確指定，最高優先）
2. Windows `C:\Windows\Fonts\kaiu.ttf`（DFKai-SB 內建）
3. macOS `BiauKai.ttc`（on-demand asset，glob AssetsV2）
4. Linux TW-Kai（全字庫正楷體，開源可散布）
找不到 → 印警告（**不 silently fallback**，對齊「禁止 fallback」條款）。**部署機仍必須安裝/打包標楷體**（Windows 內建即可；Linux/容器需 TW-Kai 或 `CEREMONY_KAI_FONT`）。

> ⚠️ **直書姓名不可靠窄欄自動換行**：薦牌姓名欄寬≈字級，換成真標楷體（全形字寬≈1em≈欄寬）後 QuestPDF 因「單字放不下」**靜默丟字**（整欄消失）。TabletRenderer 已改為**顯式每字一行**（`StackVertical`，\n 分隔、不約束寬度）。**Worship（普桌）尚未套用此修正 → 普桌陽上姓名目前不顯示（known issue，見下）。**

### 薦牌字型/直書/重疊修正驗證（2026-05-29）

**字型 + 直書（第一輪）**：dev real DB `GET /reports/tablet`（1 亡 1 陽 OneOne）：
- `pdffonts`：PingFangTC-Semibold（fallback）→ **BiauKaiHK-Regular（標楷體）**
- 影像：姓名整欄消失（只剩編號）→「往/生/甲」「陽/上/一」正確直書、位置對齊 RDLC

**多名重疊（第二輪，回應「往生/陽上重疊」）**：以測試 harness 渲染 9 變體 × 多字姓名（3 字）目視：
- **判斷名字數量**：`PrintTemplateSelector.ChooseTablet` 與舊 [SignupForm.cs PrintTablet:1148](../../reference/old/Ceremony/SignupForm.cs#L1148) 連續填值情境一致；**Base 版面只有 5 亡 + 5 陽格**（RDLC 無第 6 格 → 第 6 個名字不印，**與舊系統一致**，非 bug）
- **文字大小**：ParaFontSize（亡者 >7 字→0.6cm 否則 0.8cm）與舊一致；陽上字級依變體 0.6/0.8cm 對齊 RDLC
  - **字長以「真實字數」計（排除半/全形空格，`PrintTemplateSelector.RealCharCount`）**：使用者在姓名中間刻意輸入的排版間隙不計入 >7 字門檻（直書渲染仍保留間隙）。**刻意偏離 legacy**（舊 `Trim().Length` 計入中間空格）；詳見 [business-rules-implicit.md](../business-rules-implicit.md) 薦牌字級段與 [gotchas.md](../gotchas.md)「姓名中間空格」
- **文字位置**：與 RDLC 絕對座標一致
- **重疊真因**：RDLC 次要格 `CanGrow=true`，名目格高很矮但實際會長到「下一格之前」。可用高度應取**到下一格的列距**，不是名目格高：往生上排(Top7.5825)→下排(Top9.4464) 列距 **1.8639cm**；陽上上排(Top14.00389)→下排(Top15.44174) 列距 **1.43785cm**
- **修正（兩段）**：
  1. 直書改**整組統一字級縮字**（見下「字級策略修正」）；縮字級不壓行高 → 字不互疊
  2. **可用高度改用「列距」而非名目格高**（第二輪，回應「字體大小」）：先前用名目格高（往生 1.5875）會把 3 字往生名過度縮小；改用列距 1.8639 後，**3 字往生名（0.6×3=1.8<1.8639）維持舊系統 0.6cm 不縮**、且不溢出。陽上列距 1.43785 較窄，3 字陽上仍會略縮（無法避免，舊版同樣擠）
- 影像驗證（2d4l/4d4l/3d3l）：往生名**全部回到 0.6cm 原字級且不重疊**；陽上次要欄略縮但分開不疊；主姓名維持原字級

**文牒往生/陽上重疊（第三輪，回應「文牒往生者重疊」）**：TextRenderer 先前**未**套用直書/縮字修正（仍 width-wrap）→ 同類重疊。
- **數量判斷**：`ChooseText` 與舊 [SignupForm.cs PrintText:1335](../../reference/old/Ceremony/SignupForm.cs#L1335) 一致（恰 2 亡→tmpTextTwo，否則 tmpText）
- **文字大小**：舊 PrintText **無 ParaFontSize**，亡/陽固定 0.8cm（RDLC）→ 與新版一致
- **修正**：抽共用 [`VerticalText`](../../backend/src/Ceremony.Infrastructure/Reporting/VerticalText.cs)（`Stack` + `GroupFontPt` + `Avail`），薦牌/文牒共用；文牒亡者矩陣列距 **2.06375cm**、陽上 **1.98436cm**

**字級策略修正（第四輪，回應「文字變有大有小」）**：逐格各縮 → 同張字有大有小（主欄大次要小），使用者要**一致字級**。
- 改 `GroupFontPt`：**整組（所有往生 / 所有陽上 各一組）統一字級** = min(舊字級, 每格「可用高/字數」) → **全組同大小**、最擠的也塞得下、不重疊；不需要時 = 舊字級
- 影像驗證：文牒 5 亡 → 5 個往生**同大小**且不疊（之前主欄較大）；文牒/薦牌 3 亡 3 陽 → 全 0.8/0.6 不縮；薦牌 4 亡 4 陽 → 往生同大小、陽上整組同縮不疊
- 往生與陽上是**兩組各自統一**（對齊舊系統 ParaFontSize 只管往生、陽上另有固定字級）
- 影像驗證（文牒 3d3l/5d2l/2d2l + 薦牌 3d3l/4d4l 回歸）：皆不重疊，且僅在「正下方有名字」時才縮字

### 測試

`backend/tests/Ceremony.Infrastructure.Tests/Reporting/RendererSmokeTests.cs` — 9 Tablet 變體 + 2 Text 變體 + DataCard/Receipt/Worship + `VerticalText.Stack`（直書）+ `VerticalText.GroupFontPt`（整組統一字級，4 case）helper 等；Infrastructure 全套 **42 綠**。

### 本輪仍未做（remaining）

- **🔴 Worship（普桌）陽上姓名不顯示** — 同類 silent 丟字（陽上字級 2–3cm 在 2.2cm 欄寬被丟）；需比照 TabletRenderer 套用 `StackVertical` + 不約束寬度，並以 worship RDLC 重新核對欄距/字級（3cm 字在 2.2cm 欄距是否重疊待查）
- **客戶實機列印驗收**（需印表機環境）
- **Worship 6 變體各自座標 layout**（本輪只做了字級切換 + 背景；選定的 4 項**不含** Worship 變體座標）
- **±0.05cm CI 座標量測自動化**
- ✅ **(已修 2026-05-29)** SkiaImageHelpers（文牒垂直地址 PNG）字型：原 `SKTypeface.FromFamilyName("BiauKai")` 在 OS 找不到家族名 → `SKTypeface.Default`（無中文字符）→ 地址整排 **tofu 方框**。**關鍵**：Skia 的 `FromFamilyName` 與 QuestPDF 的 `FontManager` 是兩條獨立解析路徑，ReportFonts 註冊進 QuestPDF 救不到 Skia。已改為 `SKTypeface.FromFile(ReportFonts.ResolvedPath)` 載入同一字型檔（快取）；影像驗證地址正確直排標楷體、數字旋轉 90°
- ✅ **(已修 2026-05-29)** 文牒垂直地址**字會黏在一起**：每字往下步進原照搬舊 GDI+ 公式 `MeasureString.Height − 9`，但 GDI+ MeasureString.Height 膨脹（含 line gap，≈1.4–1.5× 字級），SkiaSharp 的 `Descent−Ascent` 已是緊湊行高（25.6px），再 −9 變 16.6px < 字面 23px → 重疊。改為**步進 = 字型行高**（不再 −9/−10）→ 字距正常、不黏（影像驗證）

> **🔴 列印有位置限制（必看）**
>
> 牌位 / 普桌 / 文牒會印在**預印格式紙**（紙廠提供、含框線/紋飾/底圖）上，欄位位置偏差超過 ±0.2cm 就會印出框線外、覆蓋紋飾、或超出實體印表機可印區（0cm margin = 滿版）。
>
> **19 個 RDLC 的所有控件精確座標（Top / Left / Width / Height / FontSize，全部 cm）已抽出至 [printing-reports-positions.md](printing-reports-positions.md)**。新版 QuestPDF 模板必須照此 1:1 還原，否則套印錯位。

## 範圍

### 做什麼
- 5 種報表類型：資料卡 / 收據 / 薦牌 / 文牒 / 普桌
- 對應舊 19 個 RDLC 的版面（紙張尺寸、字級、欄位位置 ≤ 0.2cm 誤差）
- PDF 輸出 + 預覽（嵌入前端 PDF.js）
- 多筆批次列印 → PDF 合併為單檔
- 垂直地址圖片（文牒）— 用 SkiaSharp 產 25×605 PNG（`SkiaImageHelpers.VerticalAddress`）再 embed（QuestPDF 2026 收回 Canvas API，無法直接畫旋轉文字）
- 堂號拆分（2 字 1+1、4 字 2+2）
- 編號避 4 顯示
- Excel 匯出（信眾與報名 32 欄）

### 不做什麼
- HTML 印表機驅動（仍用後端 PDF + OS 列印）
- 雲端列印
- 報表動態設計（admin 編模板）— 未來功能

## 報表版面規格（必須對齊舊系統）

> 各模板的逐欄位精確座標見 [printing-reports-positions.md](printing-reports-positions.md)。本節僅列**頁面級**規格。

### 1. 資料卡（tmpDataCard）

| 屬性 | 值 |
|---|---|
| 紙張 | 21cm × 14.8cm（A5 橫式） |
| 方向 | Portrait（自定義卡片） |
| 邊界 | 0cm 滿版 |
| 字型 | 標楷體 |
| 字級 | 0.6cm（小標注）~ 1cm（主內容） |
| 特殊 | 實線/虛線分隔、簽名欄「確認無誤請簽名」 |

欄位：HallName / Number / Prepay / 6×LivingName / 6×DeadName / Address / Phone / Remark

### 2. 收據（tmpReceipt）

| 屬性 | 值 |
|---|---|
| 紙張 | 21cm × 29.7cm（A4），Tablix 高 59.4cm（雙聯） |
| 方向 | Portrait |
| 邊界 | 0cm |
| 字型 | 標楷體 |
| 特殊 | 雙聯（收據聯 + 存根聯），上下兩半 A4 |

欄位：Name / Zipcode / Address / Fee / Number / Year / Month / Day（民國年月日）/ Prepay

### 3. 薦牌（tmpTablet × 9 變體）

| 屬性 | 值 |
|---|---|
| 紙張 | 11.5cm × 25.4cm（牌位窄長） |
| 方向 | Portrait |
| 邊界 | 0cm |
| 字型 | 標楷體 |
| 特殊 | 9 變體（基本 + One / OneOne / OneTwo / Two / TwoOne / TwoTwo / _One / _Two） |

欄位：HallNameFirst / HallNameSecond / Number / 6×LivingName / 6×DeadName

> ✅ **第 6 位往生/陽上必印滿（修正 legacy 缺陷，2026-06-30 已實作）**：legacy RDLC + 原 renderer 只畫 `d[0..4]`/`l[0..4]`，第 6 格 silently 丟掉。已在 `TabletRenderer`（往生 default + 陽上 Two/One/Base）補 `[5]`，座標（補矩陣空位）往生6 `Top9.4464/L4.9`、陽上6 `Top15.44174/L1.56167`，納入 `GroupFontPt` 分組。回歸鎖 `RendererSmokeTests`、pdftoppm 影像驗證。視覺圖 [reference/diagrams/tablet-text-sixth-name-position.png](../../reference/diagrams/tablet-text-sixth-name-position.png)。詳見 [business-rules-implicit.md §18](../business-rules-implicit.md)。

> **變體選擇邏輯（已完整反推）**：詳見下方「變體選擇邏輯」章節。可選擇：(a) 還原 9 個 RDLC 結構，或 (b) 新版改用單一彈性模板 + 條件式渲染。**建議 (a)**：版面已驗證可用，1:1 還原最安全。

### 4. 文牒（tmpText × 2 變體）

| 屬性 | 值 |
|---|---|
| 紙張 | 36.5cm × 26.2cm（超寬） |
| 方向 | Landscape |
| 邊界 | 0cm |
| 字型 | 標楷體 |
| 特殊 | 含**垂直地址**（PhotoAddress byte[]） |

舊系統用 Library.DrawText 生成 25×605 PNG 嵌入 RDLC。**新版同樣產 25×605 PNG**（`SkiaImageHelpers.VerticalAddress`，1:1 移植 Library.cs：中文直排、`[a-zA-Z0-9\-\(\)]` 旋轉 90°）再 embed — 因 QuestPDF 2026 收回 SkiaSharp Canvas API，無法直接畫旋轉文字。

欄位：HallNameFirst / HallNameSecond / Number / 6×LivingName / 6×DeadName / 垂直地址圖

> **往生/陽上欄位的逐格絕對列印座標、列距、主欄**見 [printing-reports-positions.md § 12（tmpText）/ § 13（tmpTextTwo）](printing-reports-positions.md)：tmpText 往生 5 格矩陣列距 **2.06375cm**、陽上 **1.98436cm**；tmpTextTwo 恰 2 亡皆整欄高。此表為對位驗收與排查重疊的 single source of truth。

> ✅ **第 6 位往生/陽上必印滿（同薦牌，2026-06-30 已實作）**：文牒同印往生 + 陽上，原 `TextRenderer`（陽上 inline / 往生 tmpText）只畫 `[0..4]`。已補 `[5]`，座標（補矩陣空位）往生6 `Top5.72264/L12.41251`、陽上6 `Top17.25916/L21.87382`。回歸鎖 + 影像驗證（往生+陽上各 6 位全印）。詳見 [business-rules-implicit.md §18](../business-rules-implicit.md)。

### 5. 普桌（tmpWorship × 6 變體）

| 屬性 | 值 |
|---|---|
| 紙張 | 21cm × 29.6cm（A4） |
| 方向 | Portrait |
| 邊界 | 0cm |
| 字型 | 標楷體 |
| 特殊 | 含背景圖 `worship2`；姓名 2cm 大字；分頁設定 StartAndEnd |

欄位：Number / 6×LivingName（無 DeadName、無 HallName）

## 變體選擇邏輯（**完整反推自舊 SignupForm.cs**）

### 薦牌 9 變體（PrintTablet line 1148）

3×3 矩陣：**DeadName 深度** × **LivingName 深度**

| | LivingName 1 only | LivingName 2 only | LivingName 3+ 或多個 |
|---|---|---|---|
| **DeadName 1 only** | tmpTabletOneOne | tmpTabletOneTwo | tmpTabletOne |
| **DeadName 1+2** | tmpTabletTwoOne | tmpTabletTwoTwo | tmpTabletTwo |
| **DeadName 3+** | tmpTablet_One | tmpTablet_Two | **tmpTablet（預設）** |

「DeadName 1 only」= DeadName1 有值 + DeadName2..6 全空
「LivingName 1 only」= LivingName1 有值 + LivingName2..6 全空

### 薦牌字級（ParaFontSize 參數）

| DeadName 條件 | 字長判定 | ParaFontSize |
|---|---|---|
| 僅 DeadName1 | > 7 字 | 0.6cm |
| 僅 DeadName1 | ≤ 7 字 | 0.8cm |
| DeadName1+2 任一 | > 7 字 | 0.6cm |
| DeadName1+2 都 | ≤ 7 字 | 0.8cm |
| DeadName 3+ | 任意 | **固定 0.6cm** |

### 文牒 2 變體（PrintText line 1335）

| 條件 | RDLC |
|---|---|
| DeadName2 有值 **AND** DeadName3..6 全空 | `tmpTextTwo` |
| 其他（含 DeadName 3+） | `tmpText`（預設） |

### 普桌 6 變體（PrintWorship line 1554）

**依 LivingName 最高有值位置**決定：

| 最高有值的 LivingName | RDLC |
|---|---|
| LivingNameSix（或全填）| `tmpWorship` |
| LivingNameFive | `tmpWorshipFive` |
| LivingNameFour | `tmpWorshipFour` |
| LivingNameThree | `tmpWorshipThree` |
| LivingNameTwo | `tmpWorshipTwo` |
| 僅 LivingNameOne | `tmpWorshipOne` |

> **6 個普桌 RDLC 都用同一張 `worship2` 背景圖**（不是 worship2/3/4/5 分別配對）。

### 資料卡 / 收據 — 固定無變體

| 報表 | RDLC |
|---|---|
| 資料卡 | `tmpDataCard`（永遠） |
| 收據 | `tmpReceipt`（永遠） |

## 列印流程

### 單筆 / 多筆列印

```
1. SignupForm 多選列 → 右鍵「列印XX」
2. 彈出 CustomDialogForm 對等元件：選 PDF / 預覽
3. 後端 POST /reports/{type} body: { signupIds[] }
4. 後端流程：
   a. 從 DB 取所有 signupIds 對應 SignupView 資料
   b. 套用堂號拆分、避 4 顯示
   c. 對每筆建立 QuestPDF Document
   d. 多筆 → 各自 render → PdfSharp 合併（或 QuestPDF 內建 multi-page）
   e. 回傳 application/pdf bytes
5. 前端：
   - PDF：用 Electron dialog.showSaveDialog 存檔
   - 預覽：iframe + PDF.js
6. 預覽可再按「列印」呼叫 OS 印表機 dialog
```

### 批次範圍列印

```
1. SignupForm 底部 plPrint：起~迄編號 + 類型
2. 「列印」→ 套用當前搜尋篩選 + 編號範圍
3. POST /reports/batch 將 query 與 type 傳給後端
4. 後端依 query 取資料 → 同上 4-6
```

## 技術選型

| 面向 | 舊 | 新 | 理由 |
|---|---|---|---|
| 模板引擎 | RDLC + Microsoft.Reporting.WinForms | **QuestPDF** | 跨平台、純 C#、編譯期型別安全 |
| PDF 合併 | PDFsharp | QuestPDF 內建 multi-document 或 PdfSharpCore | 跨平台版本 |
| 垂直文字 | GDI+ DrawText 產生 PNG | **SkiaSharp 產 25×605 PNG**（`SkiaImageHelpers.VerticalAddress`，1:1 移植 Library.cs）再 embed | QuestPDF 2026 收回 Canvas API，改走 SkiaSharp 自繪 |
| 虛線 | RDLC 內建 | **SkiaSharp 產虛線 PNG**（`SkiaImageHelpers.DashedLine`）再 embed | 同上；先前 solid 替代已解決 |
| Excel | NPOI .xls | **ClosedXML .xlsx** | 較新格式、活躍維護 |
| 預覽 | PrintPreviewDialog (EMF) | PDF.js in iframe | 跨平台、簡單 |

## QuestPDF 結構範例（薦牌）

```csharp
public class TabletReport : IDocument
{
    private readonly TabletViewModel _data;

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(11.5f, 25.4f, Unit.Centimetre);
            page.Margin(0);
            page.DefaultTextStyle(x => x.FontFamily("BiauKai").FontSize(14));

            page.Content().Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().AlignCenter().Text(_data.HallNameFirst);
                    row.RelativeItem().AlignCenter().Text(_data.HallNameSecond);
                });

                col.Item().Element(NumberSection);
                col.Item().Element(LivingNamesSection);
                col.Item().Element(DeadNamesSection);
            });
        });
    }

    void NumberSection(IContainer c) => c.AlignCenter().Text(_data.NumberDisplay);
    // ...
}
```

## 字型策略

- 桌面 Electron 端：bundle 標楷體 (BiauKai) + 微軟正黑體於 `assets/fonts/`，CSS `@font-face` 註冊
- 後端 QuestPDF：從 `Reporting:FontDirectory` 載入字型檔 → `FontManager.RegisterFontFromEmbeddedResource`
- 跨平台 fallback：`'BiauKai', '標楷體', 'DFKai-SB', serif`

## 避 4 顯示工具

`Domain.Services.AvoidFourFormatter.Format(int number) → string`

```csharp
public static string Format(int number)
{
    var s = number.ToString();
    var left = s[..^1];  // 除末位
    var last = number % 10;
    return last == 4 ? $"{left}3-1" : s;
}
```

例：104 → "103-1"、14 → "13-1"、105 → "105"。**單元測試覆蓋所有個位 0-9 與多位數**。

## 堂號拆分工具

`Domain.Services.HallNameSplitter.Split(string hallName) → (string First, string Second)`

```csharp
public static (string, string) Split(string hallName)
{
    var clean = (hallName ?? "").Replace("-", "").Trim();
    return clean.Length switch
    {
        2 => (clean[..1], clean[1..]),
        4 => (clean[..2], clean[2..]),
        _ => (clean, "")  // 其他長度：原樣放 First
    };
}
```

## 跨層影響

| 層級 | 是否影響 | 變動摘要 |
|---|---|---|
| 視覺 | 是 | 5 種報表版面對齊 |
| 前端 | 是 | reports feature + PDF.js viewer |
| 後端 | 是 | Reporting module + QuestPDF templates × 5 |
| API | 是 | `/reports/*` |
| 資料庫 | 否 | 純讀取 |
| 基礎建設 | 是 | 字型檔 bundle |
| 安全 | 部分 | 列印 audit log（含 signupIds） |

## 位置驗收（**重要**）

每個 RDLC 變體都要與舊系統列印物**並排比對**：
- 用實體紙列印一張 → 疊在舊系統的同尺寸列印物上對齊四角
- 透光（或在燈箱）檢查每個欄位的偏移
- 偏移 ≤ 0.2cm 才算通過
- 牌位 / 文牒這類預印紙特別嚴格：欄位必須完全落在預印區內

座標來源：[printing-reports-positions.md](printing-reports-positions.md) 為 single source of truth；如需調整版面，**先改該檔，再改 code**。

## 驗收標準

- [ ] 5 種報表逐張與舊 RDLC 並排比對，欄位位置誤差 ≤ 0.2cm
- [ ] 雙聯收據對位正確（測實體列印）
- [ ] 文牒垂直地址正確排列（中文不旋轉、ASCII 90°）
- [ ] 堂號拆分 2/4 字均正確；其他長度回 fallback
- [ ] 避 4：個位 4 → "3-1"；逐位數測試
- [ ] 寺方（SignupType=2）的 number 顯示**僅 NumberTitle**「寺」，不附數字
- [ ] 普桌限 SignupType=4 才可列印
- [ ] 多筆合併 PDF 順序與選取順序一致
- [ ] Excel 匯出 32 欄與舊系統一致；.xlsx 可開
- [ ] 列印 audit log 寫入

## 風險與未解問題

- ~~19 個 RDLC 變體的觸發條件未文件化~~ ✅ **已反推**（見上方「變體選擇邏輯」）
- 標楷體在 macOS/Linux 缺字 — 確認 bundle 字型後字數覆蓋率
- 雙聯 59.4cm 紙張在某些印表機可能不支援 — 改為兩頁分別 21×29.7cm 也可
- ~~普桌背景圖 `worship2` 需從舊 RDLC 提取~~ ✅ **已提取** [reference/extracted-images/worship2.png](../../reference/extracted-images/worship2.png)

## 參考資料

- [scratch/05-printing.md](../../.scratch/explore/05-printing.md)：19 RDLC inventory + ViewModel + DrawText + Hall split
- [scratch/03-signup-main.md](../../.scratch/explore/03-signup-main.md) §9：Print pipeline、CombinePDFs
- 舊原始碼：[reference/old/Ceremony/Commons/Library.cs](../../reference/old/Ceremony/Commons/Library.cs)、所有 `tmp*.rdlc`
