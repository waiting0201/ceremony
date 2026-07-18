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
last_updated: 2026-07-18 (收據第 1 頁座標依客戶樣張 reference/收據.jpg 校正：Name/Number/Prepay/年月日四項位移上下聯同步，待實體複驗；同日收據補第 2 頁郵寄封面（客訴沒印封面；Zipcode/Address/Name 16pt，空地址也輸出維持頁數）＋ Year 改民國年＋ Fee 千分位 N0 ＋ Prepay 改「預繳至X年Y」；資料卡/文牒 Address 改文牒地址（先前誤用郵寄地址，舊系統兩報表皆取 Text*）；同日普桌/普桌資料卡解鎖：移除 SignupType=4 限制（單筆 422 與批次過濾皆撤回），對齊舊系統選什麼印什麼——客訴右鍵選項被鎖；先前 2026-07-04 新增 §6 普桌資料卡 worshipcard：全新報表、A5 橫預印卡紙、葫蘆內普桌 6 變體縮小版墨跡仿射映射＋右側 Phone/Remark 套印、限 type-4、debugOverlay 支援，疊圖目視 OK 待實體驗收；普桌列印修正完成：One/Two/Three 丟字修復 + 6 變體各自座標 + 每格 5 字縮字 + 同欄上下排全形空格，340 測試綠；先前稽核：丟字範圍精確化為 One/Two/Three 變體、6 變體座標缺口量化、客戶樣張 reference/普桌.jpg 確認 RDLC 排版即客戶要求＋新增「每格容納 5 個字」需求；薦牌實體對位使用者確認 OK 結案；先前：記錄開發用列印位置檢視工具的手動產出 PDF 慣例：一律輸出到 reference/output/，用 CEREMONY_PDF_DUMP + dotnet test filter，暫時測試檔案用完即刪；先前新增 GET /reports/tablet/sample：5 亡者+5 陽上固定樣本 PDF，免 signupId，供列印位置檢視工具直接測試 Base 變體；2026-07-05 薦牌 OneOne 變體 Number/陽上/亡者 Y 座標修正 2cm Margin 偏移；debugOverlay 改用 page.Background()；亡者中心線置中)
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

> ⚠️ **直書姓名不可靠窄欄自動換行**：薦牌姓名欄寬≈字級，換成真標楷體（全形字寬≈1em≈欄寬）後 QuestPDF 因「單字放不下」**靜默丟字**（整欄消失）。TabletRenderer 已改為**顯式每字一行**（`StackVertical`，\n 分隔、不約束寬度）。**Worship（普桌）已於 2026-07-04 套用同一修正（連同 6 變體各自座標，見下）。**

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

`backend/tests/Ceremony.Infrastructure.Tests/Reporting/RendererSmokeTests.cs` — 9 Tablet 變體 + 2 Text 變體 + DataCard/Receipt/Worship + `VerticalText.Stack`（直書）+ `VerticalText.GroupFontPt`（整組統一字級，4 case）helper + `debugGrid` 校正格線 等；Infrastructure 全套 **69 綠**。

### 文牒兩項客戶回饋修正（2026-07-02，源自 `reference/文牒問題.pdf` 手寫註記）

客戶提供一張舊系統實際列印的文牒樣張（郵撥類報名，「郵1」= `NumberTitle=郵` + 編號 1，非另一種列印模板；見下方「編號欄」考據），上面手寫兩處修正意見：

1. **地址要再黑一點**：[SkiaImageHelpers.VerticalAddress](../../backend/src/Ceremony.Infrastructure/Reporting/SkiaImageHelpers.cs)（垂直地址 25×605px PNG）原用 `IsAntialias=true`；在這麼窄的欄位、這麼小的點陣尺寸下，抗鋸齒邊緣的半透明灰階像素佔比偏高，肉眼看起來偏灰而非純黑。**修正**：改 `SKFont.Edging=SKFontEdging.Alias` + `IsAntialias=false`（與同檔案 `DashedLine` 既有的做法一致），有畫到的像素一律純黑（alpha=255 時 RGB=0,0,0）。回歸鎖：`Skia_VerticalAddress_NoAntiAliasedGrayEdges`（掃描每個像素驗證無灰階邊）。
2. **往生（DeadName）姓名字級要跟陽上（LivingName）一樣大，但不能因此重疊，且不能靠縮小陽上來湊**：`TextRenderer` 「往生」「陽上」兩組**各自獨立**呼叫 `VerticalText.GroupFontPt`（共用同一 0.8cm 基準），各自都是「自己那組不重疊」的安全上限。姓名字數不多的常見情境（如該 PDF 樣張：2 位往生、3 位陽上）兩組本來就都不需縮字，**自然一樣大**，不需要額外邏輯。
   - **曾經走過的彎路（已撤回）**：一度加了 `VerticalText.Harmonize(a, b) = Math.Min(a, b)`，兩組算完各自安全上限後取交集套用到兩組，想讓「兩組視覺一致」。但真的拿一筆往生超過 3 位、且有名字帶開頭全形空格的 dev DB 真實資料（`987F3061-...`，5 位往生）測過後發現：這種資料往生會被自己的次要格擠到需要縮到 ~0.516cm，Harmonize 會把陽上（原本可以維持 0.8cm 的「黃清霞」）也一起拖小到 0.516cm——客戶明確反映**不要縮小陽上**，要的是「盡量放大往生」而非「必要時犧牲陽上」。
   - **數學上的硬限制**：往生次要格（Two/Three）可用高度＝列距（如 tmpText 為 2.06375cm），當名字字數（含刻意保留的開頭全形空格）撐到 4 行以上時，該格能用的字級上限就是「列距 ÷ 行數」，物理上不可能再放大而不疊到下一格（Four/Five）。這種擁擠情境下往生就是會比陽上小，這是版面空間不夠、不是程式邏輯錯誤，**不能靠犧牲陽上換取兩組一致**。
   - **最終定案**：往生／陽上維持各自獨立計算，不跨組對齊。名字不多時自然一樣大（滿足客戶原始訴求）；名字擁擠到需要縮小時，只縮往生自己，陽上不受影響。回歸鎖：`Text_DeadAndLivingFontSizes_MatchWhenNeitherNeedsShrinking`（取自該 PDF 樣張的真實姓名/地址）+ `Text_DeadNameShrinks_WithoutDraggingDownLivingName`（驗證往生縮字不影響陽上）。已用 `pdftoppm` 影像驗證：常見情境兩組字級一致、地址列印純黑；擁擠情境（dev DB 真實 5 位往生）往生縮小但陽上維持 0.8cm。

> **「郵1」考據**：舊系統 `SignupType` 共 5 類（1=No、2=寺、3=觀、4=普、**5=郵＝郵撥**），列印 Number 欄為 `NumberTitle+號`（見下方「報表『編號欄』字串格式」段），故「郵1」= 郵撥類第 1 號，用同一套 `tmpText`/`tmpTextTwo` 模板列印，**不是**額外的郵寄專用列印格式（已查證舊 19 個 RDLC 無第三種文牒變體）。

### 薦牌實體對位（2026-07-02 發現，2026-07-03～07-05 多輪修正，**✅ 2026-07-04 使用者確認 OK，結案**）

跟上面文牒的同一個蔡家測試資料，但這次客戶反映的是**薦牌**（[TabletRenderer](../../backend/src/Ceremony.Infrastructure/Reporting/TabletRenderer.cs)）：實際列印紙條插入蓮花瓶牌位座後，文字位置對不準視窗（跑到視窗外、蓋到雕花邊框）。

排查已排除三種常見成因：座標對照 `tmpTablet.rdlc` XML 逐一核對 1:1 吻合、實際跑 `TabletRenderer` 轉圖檢視文字不重疊不超出頁面邊界、`GroupFontPt` 的全形空格修正（見上方「文牒兩項客戶回饋修正」#2 同源 helper）本來就已套用在薦牌。判斷是「RDLC 校準當年的牌位座實體尺寸」與「客戶現有牌位座」不一致——這是**紙條 vs 實體外殼視窗的對位問題**，原本判斷光看 PDF 無法反推正確修正量。

**已做**：`TabletRenderer.Render(data, debugGrid: true)` 疊 1cm 刻度格線的診斷版本（不進生產路徑），供印出後插入實體牌位座量測。回歸鎖 `Tablet_DebugGrid_ForRealComplaintScenario_DumpsCalibrationPdf`。

**2026-07-03 用 `debugOverlay` 樣板照片量測，修正了一個確定的 bug**：疊上 `reference/template/薦牌.jpg`（200 DPI）後用像素分析量出雕花窗框內緣 Y 範圍 6.2294~16.0782cm。發現 Base/UnderscoreOne/UnderscoreTwo 變體主欄（One，無第 6 位搭配時）原本可用高度 `deadFull=11.0331cm` 從 top=7.5825 算到 18.6156cm，比窗框內緣底部多出約 2.5cm——14 字以上長名字會被印到窗框外。已改為量測值 `deadFull=8.4957`，回歸鎖 `Tablet_Base_LongDeadName_StaysWithinMeasuredWindow`。這個修正是「從量測值直接反推」，不是憑空猜測，但**樣板照片是否等於客戶目前實際使用的牌位座仍未確認**，所以仍歸類為部分修正而非結案。

**2026-07-05 使用者確認/微調**：
- 亡者（3 位以上變體，Base/UnderscoreOne/UnderscoreTwo）的「1 中間上、2 右邊上、3 左邊上、4 右邊下、5 左邊下、6 中間下」排法——使用者確認**現況已經是這樣，不用改**（One/Two 等其他變體維持原本的簡單高欄排版，不套用這個矩陣）
- 陽上排法——使用者要求**維持參照舊系統（RDLC）排法**，不重新設計成跟亡者一樣工整的中間/左/右對稱座標（目前 `DrawLivingNames` 各變體的 Left 座標，例如 Base 分支 `l[4] Left=0.13528` 跟 `l[1] Left=0.83528` 不對稱，是 RDLC 原始值，非 bug，不動）
- **Number 位置**：左上角原點往下、往右各移 0.1cm（`0.0, 0.0` → `0.1, 0.1`），純位置微調，不影響字級/字型

**2026-07-05 追加：使用者反映「薦牌亡者的列印沒有很正」，追查後發現並修正兩個確定的邊界問題**（用 `debugOverlay` 疊圖 + 像素量測比對，不是憑感覺）：
- **Two/TwoOne/TwoTwo 變體（恰好 2 位亡者）**：次要欄位（Two，d[1]）原 RDLC 值 `Left=4.2`，跟樣板量測窗框內緣左界 `4.191cm` 幾乎貼齊（實測渲染墨色起點只有約 0.06cm 淨空），肉眼看起來壓在雕花邊框上。改為 `Left=4.34`（4.191+0.15 安全邊界），跟主欄 One（`Left=5.3`）之間仍留有間距不會互撞
- **Base/UnderscoreOne/UnderscoreTwo 變體（3 位以上亡者）**：次要欄位（Three/Five，d[2]/d[4]）原 RDLC 值 `Left=4.0`，比窗框內緣左界（4.191cm）**還要更偏左**——比 Two 變體的問題更嚴重。用 3 位亡者實測（`蔡氏三`）確認文字整個印到雕花邊框外面。改為 `Left=4.25`（剛好清邊框，留約 0.06cm 淨空）。**已知取捨**：受窗框寬度物理限制（扣掉安全邊界後，剩餘寬度不夠同時容納「不縮字的中間欄」+「不貼邊框的左欄」），無法像 Two 變體一樣留到 0.15cm 邊界，只能做到剛好清邊框；使用者已確認接受這個取捨（極端情況：中間欄與左欄同時用滿版基礎字級、皆未觸發縮字時，理論上仍有極小機率互相貼近，但已比修正前直接印出邊框外好很多）
- 回歸鎖：現有 `Tablet_DebugGrid_ForRealComplaintScenario_DumpsCalibrationPdf`（Two 變體場景）新增疊圖產出 `tablet_alignment_complaint_overlay.pdf`；新增 `Tablet_Base_ThreeDeadNames_DumpsThreeColumnOverlay`（Base 變體 3 位亡者疊圖，`tablet_base_three_dead_overlay.pdf`）
- PDF 存於 `reference/output/{tablet_two_variant_overlay,tablet_base_three_dead_overlay}.pdf`

**2026-07-05 再追加：改用「故／靈位」字符中心線為排版基準，取代逐一微調的固定座標**。使用者給出明確規則：亡者排版以樣板紙預印的「故」「靈位」兩組靜態字的**字符中心線**為準——1 位亡者完全置中在中心線上；2 位時分居中心線左右；3 位以上時沿用既有 2×3 矩陣（1st 中間上、2nd 右邊上、3rd 左邊上、4th 右邊下、5th 左邊下、6th 中間下），中間欄置中在中心線上。

- **量測中心線**：用像素分析抓「故」與「靈位」兩組字的 bounding box 中心，分別為 5.6769cm / 5.696cm（幾乎重合，取平均 `DeadCenterX = 5.685`）；這個值也跟窗框內緣量測寬度的幾何中心（`(4.191+7.163)/2=5.677cm`）幾乎一致，互相印證量測無誤——另外把這條中心線疊回**原始樣板照片**（無任何渲染文字）核對，精確貫穿「故」與「靈位」視覺中心。
- **改成動態算位置**：這是這輪最大的方法論改變——之前所有變體的欄位 X 座標都是編譯期常數（不管字級縮多小，位置固定），這次改成**先算好 `GroupFontPt` 共用字級，再用字級動態算置中位置**（`Left = DeadCenterX − fontCm/2` 這類公式），因為要置中的是「實際渲染寬度」，縮字後如果位置不跟著變就會偏一邊。三種情境都改寫：
  - 1 位（One/OneOne/OneTwo）：`Left = DeadCenterX − fontCm/2`，完全置中
  - 2 位（Two/TwoOne/TwoTwo）：以 `DeadColumnGap=0.1cm` 對稱分居中心線左右（One 右、Two 左），取代前一輪用固定值 `Left=4.34` 硬修
  - 3+ 位（Base/UnderscoreOne/UnderscoreTwo）：中間欄置中、左右欄各以 `DeadColumnGap` 對稱分居兩側，取代前一輪用固定值 `Left=4.25` 硬修——這個新算法在字級縮到最小的極端情況下自動有更多邊框淨空（不像前一輪的固定值只精確涵蓋單一測試案例），一併解決了 Base 變體「剛好清邊框」的取捨疑慮
- 回歸測試新增 `Tablet_OneDeadName_DumpsCenteredOverlay`（1 位亡者置中疊圖）；`Tablet_DebugGrid_ForRealComplaintScenario_DumpsCalibrationPdf`／`Tablet_Base_ThreeDeadNames_DumpsThreeColumnOverlay` 同步驗證新算法。PDF 存於 `reference/output/tablet_one_dead_centered_overlay.pdf` 等
- `dotnet test` 314 個測試全數通過

**2026-07-05 再追加、後又修正：1 位亡者的垂直位置**。使用者指出「只有一位時，亡者位置沒在故靈位正中間」——當時把它理解成「整體垂直置中在故～靈位的空隙裡」，改成 `topY = 故下緣 + (空隙高度 − 實際文字高度) / 2`。使用者驗收後糾正：**「還是不對，要在故的正下方」**——「正中間」指的是水平方向在中心線上，不是把文字整塊漂浮置中在故跟靈位中間的空白處；垂直方向應該緊接在「故」正下方起排。改回 `topY = DeadGapTop`（故下緣 Y=7.5946cm，跟改版前的舊值 `7.5825`幾乎相同，等於保留原本的垂直起點、只套用水平置中）。`GroupFontPt` 的 avail 仍保留收緊到「故～靈位空隙 5.8674cm − 0.1 安全邊界」，避免長名字縮字上限跟實測空隙脫節（這部分改動是對的，沒有被這次糾正推翻）。**只有 1 位亡者這個情境需要水平置中**——2 位與 3+ 位矩陣都是明確的「上排/下排」列位，維持故下緣起排不變。

**✅ 結案（2026-07-04）**：使用者確認薦牌目前列印結果 OK。先前「樣板照片是否 100% 對應客戶實際牌位座」的疑慮由使用者驗收解除，不再需要 debugGrid 刻度回報。`debugGrid` / `debugOverlay` 工具保留（dev-only），日後客戶更換牌位座樣式或再出現對位客訴時，依同一套「疊圖量測 → 修正 → 實體驗收」流程處理。

### 開發用列印位置檢視工具（樣板疊圖，2026-07-03）

在 `debugGrid`（格線）之外，新增另一種對位輔助：把 `reference/template/` 底下的實體樣板掃描照（文牒.jpg / 資料卡.jpg / 薦牌.jpg，200 DPI）疊在產出 PDF 的文字層底下，讓開發人員直接肉眼比對欄位是否落在樣板框線/欄位內，比純格線更直覺。

- **觸發**：既有 3 個 GET endpoint 加 `debugOverlay` query 參數 —
  `GET /api/v1/reports/{datacard,tablet,text}?signupId=...&debugOverlay=true`。收據沒有樣板圖、普桌本身已有真背景圖，故不適用。
- **環境限制**：僅 `ASPNETCORE_ENVIRONMENT=Development` 可用；其他環境一律回 404（不是 403，避免洩漏功能存在），見 [ReportsController](../../backend/src/Ceremony.Api/Controllers/ReportsController.cs)。
- **實作**：比照 [WorshipRenderer](../../backend/src/Ceremony.Infrastructure/Reporting/WorshipRenderer.cs) 既有的 `EmbeddedResource + LoadBackground()` 手法，樣板照複製進 `Reporting/Assets/DebugTemplates/`（英文檔名，中文檔名做 EmbeddedResource 邏輯名稱有建置風險）；`DataCardRenderer` / `TextRenderer` / `TabletRenderer` 的 `Render(...)` 各加 `bool debugOverlay = false`，預設 `false` 不影響生產路徑。`TabletTemplate.OneOne`（頁面有 2cm 上下 margin）疊圖需對齊內容區高度，其餘變體對齊整張頁面——跟既有 `debugGrid` 一樣可以同時開啟。
- **已知限制（重要，不要誤用）**：掃描樣板尺寸跟 RDLC 座標換算出的頁面尺寸有小誤差（例如文牒頁 36.5×26.2cm，掃描圖換算約 36.4×25.7cm），資料卡樣板另有 EXIF 側拍需轉正。這個工具**只能做粗略肉眼比對**，不能取代 [printing-reports-positions.md](printing-reports-positions.md) 的 RDLC ground truth、`debugGrid` 實體校正、或下面「本輪仍未做」列的 `±0.05cm CI 座標量測自動化`（規劃中，尚未實作）。
- **回歸測試**：`RendererSmokeTests.cs` 的 `DataCard_DebugOverlay_DumpsCalibrationPdf` / `Text_DebugOverlay_DumpsCalibrationPdf` / `Tablet_DebugOverlay_DumpsCalibrationPdf`。
- **⚠️ 手動產出 PDF 供肉眼檢視時，固定輸出到 `reference/output/`（CRITICAL，勿忘）**：這個資料夾已整個列在 `.gitignore`（`reference/`），不會誤入 repo，且既有大量疊圖 PDF 都放在這裡（如 `tablet_current_base_overlay.pdf`、`datacard_five_living_wrapped_overlay.pdf`），維持慣例方便使用者一次找到所有檢視用 PDF。做法：`CEREMONY_PDF_DUMP=/Users/tim/agents/ceremony/reference/output dotnet test backend/tests/Ceremony.Infrastructure.Tests --filter "FullyQualifiedName~<TestName>"`（`RendererSmokeTests.cs` 底部的 `DumpIfRequested(pdf, "name.pdf")` 只在此環境變數有設值時才落地，CI 不設就不影響）。若沒有現成測試涵蓋要的資料組合（例如任意亡者/陽上人數排列），可在 `backend/tests/Ceremony.Infrastructure.Tests/Reporting/` 新增一個暫時測試檔案直接組 `TabletData`/`DataCardData`/`TextData` 呼叫對應 `Renderer.Render(data, debugOverlay: true)`，`dotnet test` 跑完＋確認 PDF 落地後**該暫時測試檔案要刪除**（不是長期回歸測試，只是產出工具，避免污染測試套件）。
- **附帶發現**：疊圖後可直接肉眼看到薦牌 Base/OneOne 變體的文字貼近甚至超出雕花窗框邊緣、資料卡的欄位標籤（陽上／地址／電話／備註）與樣板紙上已印的標籤重複繪製而略為錯位——與「薦牌實體對位開放問題」互相印證，兩者後續處理見下方對應段落。
- **✅ 2026-07-03 追加：`GET /api/v1/reports/tablet/sample`（5 位亡者 + 5 位陽上固定樣本，免 signupId）**——原本 `debugOverlay` 只加在既有 3 個 GET endpoint 上，仍要求 `signupId` 對應一筆真實 DB 報名資料；使用者要能直接輸出「5 位亡者 + 5 位陽上」的薦牌 PDF 做列印位置檢視，但 dev DB 不一定剛好有這種資料組合。新增 `GenerateTabletSampleHandler`（`Ceremony.Application.Reports`，不依賴 `ISignupRepository`）+ `ReportModelBuilders.TabletSample()`（固定假資料：亡者一~五、陽上一~五，經 `PrintTemplateSelector.ChooseTablet` 算出落在 `TabletTemplate.Base`——3+ 亡 3+ 陽的 fallback、也是排版最擁擠的 2×3 矩陣變體）。`ReportsController` 加 `[HttpGet("tablet/sample")]`，`debugOverlay` 預設 `false`、可搭配 `?debugOverlay=true` 疊樣板照片；同既有 debugOverlay 端點僅 `Development` 環境放行，其他環境 404。整合測試 `GET_tablet_sample_returns_5dead5living_PDF_in_development`；`dotnet test` 322 個測試全數通過；實機用 `pdftoppm` 轉圖確認 5 位亡者置中在「故／靈位」矩陣、5 位陽上落在陽上欄，樣板疊圖正確對齊。
- **✅ 2026-07-05 修正：`.FitArea()` 導致疊圖縮小、留白**——使用者反映「只有一位往生者的 template 引用有問題，似乎 template 變比較小，上方跟右方有一大片留白」。根因：三個 renderer 最初都用 `.Image(TemplateImage).FitArea()`（保留原圖比例、置中/靠邊留白），但樣板掃描照的實際比例（掃描誤差）跟我們假定的頁面／內容區 cm 比例對不上，尤其**薦牌 OneOne 變體**（內容區扣掉上下 2cm margin 後是 11.5×21.5cm，比例 1.87）跟樣板照片原生比例（11.52×25.69cm，比例 2.23）落差最大，疊圖因此明顯縮小、右側留白達寬度 16%。改用 `.FitUnproportionally()`（直接拉伸填滿容器，忽略原圖比例）——這個工具的用途本來就是「假設樣板照片＝我們的 cm 座標系統」去比對位置，容許非等比縮放反而更符合這個假設，比保留比例留白更正確。像素量測確認修正後三個 renderer 的疊圖都填滿容器寬高達 99.8%+ 以上（先前薦牌 OneOne 只有 83.7%）。三個 renderer 都受影響已一併修正（不只薦牌）。
- **✅ 2026-07-05 真正修正：改用 `page.Background()` 繞開 Margin 裁切，OneOne 疊圖終於蓋滿整張紙**——使用者追問「上下也（跟）右有留白，再確認一下」，且明確指出「那不是正常的留白」，不接受「這是 QuestPDF 限制」的說法，並提示參考 3 位亡者（Base 變體，無 margin）的疊圖方式。重新檢討後發現關鍵：先前失敗的負值 `TranslateY(-2cm)` 是在 **`page.Content().Layers(...)`** 底下操作，這個座標系統本來就會被 `page.Margin(...)` 裁切；但 QuestPDF 另外提供 **`page.Background(...)`**，是畫在「整張實體紙」座標系統、完全不受 `page.Content()` 的 Margin 影響。把疊圖從 `page.Content()` 內的 `Layer` 改成 `page.Background().Image(TemplateImage).FitUnproportionally()`，並把 `layers.PrimaryLayer().Background("#FFFFFF")` 在 `debugOverlay=true` 時改用 `Colors.Transparent`（否則白底會蓋掉 Background 疊的樣板照片；PrimaryLayer 仍要保留呼叫以維持 Layers 容器尺寸）。修正後像素量測確認 OneOne 疊圖四邊留白全部歸零（含上下 margin 區域），完整看到牌位圖案全貌，文字仍在 `page.Content()` 座標系統內、依然遵守 margin。回歸測試 `Tablet_DebugOverlay_DumpsCalibrationPdf(OneOne)` 通過（先前用負值位移的版本這裡會失敗）。**教訓**：先前「這是 QuestPDF 限制、不是 bug」的結論下得太早——只證明了「在 `page.Content()` 座標系統內」這條路走不通，沒有進一步排查 QuestPDF 是否有繞過 Margin 的其他 API；使用者不接受「限制」說法、要求再查，才找到 `page.Background()` 這個正確路徑。

- **✅ 2026-07-05 疊圖修好後，露出了一個更早就存在、被舊工具遮住的真實排版 bug**：使用者接著指出「留白可以了，但是 y 軸的位置不對，請參考三位亡者的 y 軸位置」，具體點名 Number、陽上、亡者三處。因為疊圖以前只顯示內容區（21.5cm）裁切後的畫面，OneOne 的文字位置錯誤剛好被裁掉的範圍「巧合遮住」；疊圖蓋滿整張紙後，錯位第一次真正被看見。用 cm 尺標疊在渲染結果上精確量測，確認根因：**Number（`Top=0.1`）、LivingNames「1 位陽上」分支（`Top=14.00389`，OneOne 與 TwoOne/UnderscoreOne 共用）、DeadNames「1 位亡者」分支（`DeadGapTop=7.5946`，OneOne 與 OneTwo/One 共用）這三處都是「多個變體共用同一個座標常數」，但只有 OneOne 有 2cm Page Margin**——`page.Content()` 的座標原點比真實頁面頂端低 2cm，所以只有 OneOne 印出來的實體位置會比共用同一個常數的其他變體（TwoOne/UnderscoreOne/OneTwo/One，這些都沒有 margin）低 2cm。實測驗證：LivingNames 修正前印在 true-page Y≈16.0cm，比樣板紙預印的「陽上」標籤（Y≈12.5~13.7cm）多空了快 2.3cm；Number 修正前印在 Y≈2.3cm，不是預期的頁面頂端附近。
  - **修法**：三處都加 `var marginCompensation = data.Template == TabletTemplate.OneOne ? 2.0 : 0.0;`，content-Y 一律減去這個補償值，讓 OneOne 印出來的實體頁面高度跟共用同一個座標常數的其他變體一致。
  - **關鍵技術發現**：原本擔心負值 `TranslateY`（例如 `0.1 - 2.0 = -1.9`）會像先前 debugOverlay 圖片疊圖那樣被 QuestPDF 整層裁掉——**但實測文字（`Text` fluent API）不會被裁掉**，只有先前疊圖用的 `Image` + `.FitUnproportionally()`/`.FitArea()` 那條路徑會被裁；`回歸測試（含檢查 PDF 位元組數變大的測試）全數通過證實這點。這代表 QuestPDF 對「超出 Margin 範圍的內容」是否裁切，**依內容類型而不是座標系統統一決定**，不能從一種元素的行為直接類推到另一種。
  - PDF 已更新至 `reference/output/`，`dotnet test` 314 個測試全數通過（無需新增測試，既有測試已能驗證內容真的畫出來）。

### 資料卡改版（2026-07-03，用 `debugOverlay` 樣板量測發現版面結構落差後改版）

用 `debugOverlay` 疊 `reference/template/資料卡.jpg` 後，用 cm 格線 + 像素分析精確量出樣板實際版面，發現舊 25-TextBox 版面（1:1 還原 `tmpDataCard.rdlc`）跟樣板紙有結構性落差：**樣板 Y=0~2.85cm 完全空白，沒有「亡者」欄、也沒有堂號（HallName）欄**——樣板上第一個出現的欄位是「陽上：」（Top≈2.6924cm），跟舊程式碼畫在 Top=4.707cm 差了約 2cm；樣板右側印有一個跟薦牌同款的「故◯◯靈位」窗框圖案（量測內緣 Left=14.986~17.9705cm，「故」字下緣 Y=5.6388cm、「靈位」上緣 Y=11.4427cm）。

**使用者確認的改版方向**：
- Number（掛號）留左（沿用原座標，不受影響）
- 預繳（Prepay）留右（沿用原座標——原本以為會跟窗框重疊而縮窄，後來確認窗框 Top=4.40cm 起，跟預繳所在列（Top 0.776~1.667cm）不重疊，改回原寬度）
- 堂號（HallName）不印——**已從 `DataCardData`/`DataCardModel`/`ReportModelBuilders.DataCard` 整條移除**，不是保留欄位只是不畫
- 亡者姓名改印進右側樣板窗框裡，比照 [TabletRenderer](../../backend/src/Ceremony.Infrastructure/Reporting/TabletRenderer.cs) 用 `VerticalText.Stack` + `GroupFontPt` 直書堆疊（見 `DataCardRenderer.DrawDeadNamesInWindow`）：多位亡者以「、」串接成一欄，縮字塞進窗框缺口（量測高度 5.8039cm，扣 0.3cm 安全邊界）
- 陽上／地址／電話／備註／簽名：陽上整段（含 2 排）依樣板量到的 Top≈2.69cm 上移（其餘列距不變）；地址/電話/備註/簽名原本座標跟樣板量測值已經很接近（誤差 <0.35cm），沿用不動
- 原本分隔「亡者」跟「陽上」的虛線（Line2）已移除——樣板該位置本來就沒有印線，亡者欄拿掉後這條線也失去意義

**2026-07-03 追加：拿掉重複標題**。疊圖後發現「陽上：」「地址：」「電話：」「備註：」「確認無誤請簽名：」樣板紙本身就已經預印這些欄位標題（跟簽名底線），程式又在幾乎同一位置重畫一次同樣的文字，肉眼看起來是淡淡的疊字/雙重印。使用者確認樣板已有標題，**程式不再印任何標題文字，只印欄位內容**（Left 座標維持在標題右側原本留給內容的位置不變）：拿掉 5 個標題 `DrawText` 呼叫、拿掉整個 `Line1`（簽名底線，樣板已印）；連帶清掉這之後就沒人用的 `DrawLine` method 與 `DrawText` 的 `vAlign`/`VerticalAlign` 參數（原本只有簽名標題那行用 `Bottom`，拿掉後全部呼叫都是預設 `Top`，簡化掉未使用的分支）。

**回歸測試**：`DataCard_MultipleDeadNames_StayWithinMeasuredWindow`（4 位亡者串接縮字仍需留在窗框內）、`DataCard_DebugOverlay_DumpsCalibrationPdf`。

**跟薦牌不同的地方**：資料卡是平面 A5 紙，不像薦牌要塞進實體 3D 牌位座——這裡的座標修正可以直接照樣板照片量測值定案，不需要像薦牌那樣等實機插入測試。

**2026-07-04 使用者指定版面微調**：
- **陽上改 3 排 × 2 欄**（原本 2 排、欄寬過寬較擠）：`LivingNames[0]` 第一排；`[1]`／`[3]` 第二排前／後；`[2]`／`[4]` 第三排前／後。欄寬只留 6 字寬（`0.8cm × 6 = 4.8cm`，剛好夠不用更寬）；前欄 `Left=4.328`，後欄 `Left=9.986` 寬 `4.8` 結束於 `14.786`，跟右側樣板窗框（`Left=14.986` 起）留 0.2cm 不重疊；三排 `Top` 分別為 `2.690`／`3.643`／`4.596`（沿用原本 0.953cm 列距）
- **地址上移 1cm**（`6.753→5.753`），**寬度收到 10.4cm**（`4.328` 起算結束於 `14.728`，避開窗框），不設 `.Height()` 故文字過長會自動換行、不裁切
- **備註下移 0.5cm**（`9.602→10.102`），寬度同樣收到 10.4cm 避開窗框，可多行
- **亡者窗框內文字再靠右 0.3cm**（`DrawDeadNamesInWindow` 的 `columnLeft` 從 `15.985` 改 `15.985 + 0.3`），窗框內緣右界 `17.9705` 仍有 1.4cm+ 餘裕，不會超框
- 電話欄位使用者未要求調整，維持原座標不動
- 回歸測試新增 `DataCard_FiveLivingNamesAndWrappedText_DumpsCalibrationPdf`（5 位陽上 + 刻意寫長的地址/備註觸發換行），用 `debugOverlay` 疊圖目視確認新版面互不重疊；PDF 存於 `reference/output/datacard_five_living_wrapped_overlay.pdf`

**2026-07-05 使用者再指定版面調整**：
- **地址／電話／備註改為對齊陽上的方式**（對齊樣板量到的標題文字「上緣」，而非用位移量推算）：地址 `Top=6.4135`、電話 `Top=8.8392`、備註 `Top=9.8679`，直接取代前一版用 ±1cm/±0.5cm 位移算出來的座標——原本的位移量沒有對齊到樣板實際標題位置，這次改成跟陽上同一套方法（量測標題上緣）才會準
- **亡者改成跟薦牌一樣的 2×3 矩陣**，取代原本單欄「、」串接：1st 中間上、2nd 右邊上、3rd 左邊上、4th 右邊下、5th 左邊下、6th 中間下（完全比照 [TabletRenderer.DrawDeadNames](../../backend/src/Ceremony.Infrastructure/Reporting/TabletRenderer.cs) default 分支的排法），並整體再往下 0.1cm、往左 0.1cm
  - `DrawDeadNamesInWindow` 改寫：`topRowY=5.7388`、`rowPitch=2.6`、`bottomRowY=8.3388`、`fullHeight≈2.9039`（到「靈」字上緣扣 0.2 安全邊界），三欄 X：`centerX=16.185`、`leftX/rightX = centerX∓0.75`
  - **踩雷**：窗框內緣只有 2.9845cm 寬，容不下 3 欄用 0.8cm 字級——`GroupFontPt` 只會縮不會放大，短名字（1 字）在只受高度限制時可以維持 0.8cm base，但 0.8cm 字寬跟 0.75cm 欄距幾乎沒有間隙，肉眼看起來 3 欄黏在一起。改把這個窗框專用的 `baseFontCm` 降到 0.6cm（其餘欄位不受影響），才留得出欄距；用 `甲乙丙丁戊己` 6 個相異單字疊圖實測確認 3 欄真的分開、不黏在一起（回歸測試 `DataCard_SixDistinctDeadNames_MatrixColumnsDoNotTouch`）
- 回歸測試：`DataCard_SixDeadNames_MatrixStaysWithinMeasuredWindow`（滿 6 位亡者含長名字驗證矩陣不超框）、`DataCard_SixDistinctDeadNames_MatrixColumnsDoNotTouch`、`DataCard_OneDeadName_MatrixCenterTopRenders`（典型單一亡者情境）；PDF 存於 `reference/output/{datacard_six_dead_matrix_overlay,datacard_six_distinct_dead_overlay}.pdf`

### 本輪仍未做（remaining）

- ✅ **(已修 2026-07-04) Worship（普桌）陽上姓名不顯示 + 6 變體各自座標**（新舊對照稽核後確認為同一工作項，一次修完）
  - **原問題（實際渲染驗證）**：silent 丟字發生在 One/Two/Three 變體（3cm 字塞進寫死的 2.2cm 欄寬 → QuestPDF 整欄丟掉，PDF 只剩 Number）；且 6 變體全部共用 Base 的 2×3 矩陣座標、只切字級
  - **客戶樣張佐證（[reference/普桌.jpg](../../reference/普桌.jpg)，紅筆標註普595–600）**：客戶手圈 ①–⑥ 位置順序與舊 RDLC 各變體完全一致 → 確認「照 RDLC 1:1 還原」即客戶要求。並帶出兩條定案需求：**「各容納5個字」**（每格 5 字，對應「3 字姓名＋闔家」型態）與**「同欄上下排名字之間要有空格」**
  - **修法（WorshipRenderer 全面改寫）**：姓名改 `VerticalText.Stack` 顯式每字一行 + 不約束寬度（免丟字）；6 變體各自座標依 [printing-reports-positions.md](printing-reports-positions.md) §14–19（One 單欄置中 8.55021 / Two 雙欄 10.34938+6.62188 / Three 三角＝主欄 8.55021 通過下排 12.10938+5.00792 之間 / Four 2×2 / Five 上 2 下 3 / Base 2×3 矩陣）；`GroupFontPt` 整組統一字級守格高（One/Two/Three base 3cm、其餘 2cm；avail=RDLC 格高）；有上下排的變體（Base 0↔3/1↔4/2↔5、Four 0↔2/1↔3、Five 上下欄 X 錯開取重疊者）套 `WithBottomGap` 全形空格——5 字＋空格＝6 列由縮字吸收（10.21125/6≈1.70cm）
  - **驗證**：新增回歸鎖 `Worship_LivingNames_AreNotSilentlyDropped`（6 變體逐一比對有/無姓名 PDF 位元組差）+ `Worship_CustomerSampleScenarios_DumpCalibrationPdfs`（樣張普595–600 六情境）；全套 340 測試綠；6 張 PDF 轉圖目視與客戶樣張排版一致，存於 `reference/output/worship_*.pdf`
- **客戶實機列印驗收**（需印表機環境）
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
| 特殊 | 虛線分隔、簽名底線、所有欄位標題文字（陽上/地址/電話/備註/確認無誤請簽名）皆已於 2026-07-03 改版移除——樣板紙本身已預印，程式只印內容 |

欄位（2026-07-03 改版後）：Number / Prepay / 5×LivingName / 5×DeadName（印進右側樣板窗框）/ Address / Phone / Remark。**HallName 已移除**（樣板無堂號欄，見下方「資料卡改版」）

> ✅ **2026-07-18 對齊舊系統兩項**：(1) Address 改用**文牒地址**（`TextCity+TextZone+TextAddress`）——舊系統右鍵與批次兩路徑都取 Text\*（SignupForm.cs:233/502），新版先前誤用郵寄地址；(2) Prepay 字樣改「預繳至X年Y」（SignupForm.cs:220/489），先前為「預繳 X Y」。回歸鎖 `DataCard_uses_text_address` / `DataCard_prepay_uses_legacy_wording`。

### 2. 收據（tmpReceipt）

| 屬性 | 值 |
|---|---|
| 紙張 | 21cm × 29.7cm（A4），Tablix 高 59.4cm（雙聯） |
| 方向 | Portrait |
| 邊界 | 0cm |
| 字型 | 標楷體 |
| 特殊 | 雙聯（收據聯 + 存根聯），上下兩半 A4；**每筆固定 2 頁**：第 2 頁為郵寄封面（Zipcode / Address / Name，16pt），地址空白也照樣輸出（維持舊系統頁數與送紙順序） |

欄位：Name / Zipcode / Address / Fee / Number / Year / Month / Day（民國年月日）/ Prepay

> ✅ **2026-07-18 客戶樣張座標校正**：客戶實印套版後在 `reference/收據.jpg` 手寫標註，第 1 頁四項位移已套用（上下聯同步）：Name 下移 0.2cm、Number 下移 0.8cm＋右移 1.0cm（原壓到預印「為」字）、Prepay 下移 0.3cm、年月日列下移 0.5cm。新座標值見 [printing-reports-positions.md §2 改版覆蓋註記](printing-reports-positions.md)；**待客戶實體套印複驗**。樣張大字「郵」為手工郵撥戳，非系統列印。
>
> ✅ **2026-07-18 四項修正**：(1) 客訴收據沒印封面——`ReceiptRenderer` 原本只畫第 1 頁上下聯，漏了 RDLC Tablix 59.4cm 的第二半（郵寄封面頁 Textbox22-24），已補第 2 頁（座標見 [printing-reports-positions.md §2 郵寄標籤區](printing-reports-positions.md)，Top 取原始值 −29.7cm）；Zipcode 用 `MailZipcode`、Address 用 `MailCity+MailZone+MailAddress`（同舊 SignupForm.cs:520-521，**收據是唯一用郵寄地址的報表**，資料卡/文牒用文牒地址）。(2) Year 原誤印西元年，已改民國年（`now.Year - 1911`，對齊舊 `taiwanCalendar.GetYear`）。(3) Fee 補千分位 `ToString("N0")`（舊 SignupForm.cs:522；印 `1,200`）。(4) Prepay 字樣改「預繳至X年Y」（舊 SignupForm.cs:527；先前為「預繳 X Y」）。回歸鎖 `RendererSmokeTests.Receipt_RendersPdf`（驗 2 頁）/ `Receipt_EmptyAddress_StillTwoPages` / `ReportNumberFormatTests.Receipt_prints_roc_year_month_day` / `Receipt_fills_mailing_cover_fields` / `Receipt_formats_fee_with_thousand_separator` / `Receipt_prepay_uses_legacy_wording`；目視 PDF 已存 `reference/output/receipt_with_cover.pdf`。

### 3. 薦牌（tmpTablet × 9 變體）

| 屬性 | 值 |
|---|---|
| 紙張 | 11.5cm × 25.5cm（牌位窄長；2026-07-05 使用者確認實體紙張尺寸，原 RDLC 值 25.4cm 少 0.1cm，已在 `TabletRenderer.PageHeightCm` 修正，9 變體共用） |
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

> ✅ **2026-07-18 地址來源修正**：垂直地址改用**文牒地址**（`TextCity+TextZone+TextAddress`，舊 SignupForm.cs:350-352/608 兩路徑皆同）——新版先前誤用郵寄地址。回歸鎖 `Text_uses_text_address`。

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

### 6. 普桌資料卡（worshipcard，2026-07-04 新增，**全新報表、舊系統無對應 RDLC**）

| 屬性 | 值 |
|---|---|
| 紙張 | 21cm × 14.8cm（A5 橫），**預印卡紙**（reference/template/普桌資料卡.jpg，200 DPI 掃描） |
| 方向 | Landscape |
| 邊界 | 0cm |
| 字型 | 標楷體 |
| 特殊 | 預印內容（左葫蘆輪廓＋右側「電話：／備註：／確認無誤請簽名」標題＋簽名底線）**程式不畫**；生產 PDF 不嵌樣板圖，jpg 僅 `debugOverlay` 對位用 |

欄位：Number / 6×LivingName（葫蘆內）＋ Phone / Remark（右側橫書）

- **葫蘆內＝普桌牌位縮小版**（使用者 2026-07-04 定案）：編號 Bold 置中＋陽上直書，依人數套與普桌完全相同的 6 變體（`PrintTemplateSelector.ChooseWorship`），等於給信眾核對簽名用的牌位預覽。座標**不重新設計**，用「墨跡對墨跡」仿射映射從 `WorshipRenderer` 搬（錨值與公式見 [printing-reports-positions.md](printing-reports-positions.md) §20）；字級同步縮放（2cm→約 0.92cm、3cm→約 1.38cm），`GroupFontPt` 格高用映射後值守住「各容納 5 字」縮字行為
- 右側 Phone（`Signup.Phone`）/ Remark（`Signup.Remark`）對齊樣板預印 label 上緣與冒號右緣（量測值見 positions §20），Remark 過長自動換行（不設 `.Height()` 不裁字）
- **不限 SignupType**（2026-07-18 解鎖，原「限 type-4 丟 `WORSHIP_ONLY_TYPE_4`」已撤回）：與普桌一致選什麼印什麼，對齊舊系統；前端右鍵選單恆啟用
- 實作：[WorshipCardRenderer.cs](../../backend/src/Ceremony.Infrastructure/Reporting/WorshipCardRenderer.cs)；endpoint blueprint [get-reports-worshipcard.md](api-endpoints/get-reports-worshipcard.md)
- 驗證：`RendererSmokeTests.WorshipCard_*`（6 變體、丟字回歸鎖、電話/備註渲染、6 情境 dump 含 overlay 版）；`reference/output/worshipcard_*_overlay.pdf` 疊圖 6 變體目視 OK（2026-07-04）；**實體卡紙套印驗收待使用者確認**

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
- [ ] 普桌／普桌資料卡不限 SignupType，選什麼印什麼（2026-07-18 解鎖，對齊舊系統）
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
