---
title: Gotchas
purpose: 紀錄已知陷阱、反模式、踩雷紀錄，避免重複犯錯
applicable_when: 開始實作前的 sanity check、遇到奇怪問題時、code review 時
related_agents:
  - qa-test-engineer
  - code-review-optimizer
related_docs:
  - conventions.md
keywords: [gotchas, 陷阱, 踩雷, 反模式, anti-pattern]
last_updated: 2026-06-02
---

## 通用陷阱

### 文件腐化
- **症狀**：doc 寫的設計與實際 code 不符
- **預防**：嚴格遵守 [../CLAUDE.md](../CLAUDE.md) 「文件同步規則」；變更 code 必檢視相關 doc
- **檢測**：定期跑 doc-lint（待開發）；每月抽樣比對

### Agent 錯置
- **症狀**：用 backend-engineer 改前端、用 qa-test-engineer 寫 code
- **預防**：先查 [../CLAUDE.md](../CLAUDE.md) 路由表
- **特例**：qa-test-engineer **絕不**修改 code，只審查；要求其改 code 應改用 code-review-optimizer 或 backend/frontend agent

## 專案層級陷阱

### CSS Grid `1fr` 被 iframe / nowrap 子元素撐爆（2026-05-28）
- **症狀**：兩欄 grid（`320px 1fr`）中 1fr 那欄包 `<iframe>` 或 `white-space: nowrap` 元素，欄位寬度超過 grid cell、視覺上**覆蓋到隔壁欄**
- **原因**：CSS Grid 中 `1fr` 隱含為 `minmax(auto, 1fr)`，`auto` 下限是子元素 `min-content`；iframe 的 min-content 不為 0 → 1fr 被撐成大於 `(parent - 320px)`
- **修正**：把 `1fr` 寫成 `minmax(0, 1fr)`，並在子元素加 `min-width: 0`；必要時補 `max-width: 100%`
- **延伸**：flex 也有同類問題（flex item 預設 `min-width: auto`）；遇到「元素超出容器」先想 min-width: 0
- **真實案例**：[reports-preview-page.scss](../frontend/src/app/features/reports/reports-preview-page.scss) — 第一版兩欄並排碰到，後改為垂直堆疊一次解決（[visual-design 列印預覽段](design/visual-design.md#列印預覽頁面reportspreview2026-05-28-重新設計)）
- **真實案例 2（2026-05-29）**：新增報名表單地址列 `grid-template-columns: 1fr 1fr 96px`，兩個「郵遞區號」`<input>` 超出 overlay。`<input>` 的 min-content 不為 0，加上全域 `.field input` **未設 `box-sizing`/`width`**（content-box + border/padding）更易溢出。修正：tracks 改 `minmax(0, 1fr)`、`.grid > * { min-width: 0 }`、表單內 `input/select/textarea { width:100%; max-width:100%; box-sizing:border-box }`（[signup-edit-form.component.scss](../frontend/src/app/features/signups/signup-edit-form.component.scss)）
- **通則**：表單控件放進 grid/flex 前，先確保 `box-sizing: border-box` + `width: 100%` + 容器 `min-width: 0`，否則 input 固有寬會撐爆版面

### position: sticky + 100vh 高度 → 視覺「覆蓋」感（2026-05-28）
- **症狀**：右欄 `position: sticky; height: 100vh` 想做「跟著捲動的預覽」，但捲動時感覺它「壓過」左欄內容
- **原因**：sticky 元素高度接近 viewport 時，使用者捲動到下半時，原本應該並列的內容已捲出視野，剩下 sticky 元素獨自佔據畫面，造成「覆蓋」的視覺感
- **修正**：sticky 適合「短工具列／aside」，不適合「滿版預覽」；後者改用垂直堆疊 + iframe 自己給固定高
- **適用判斷**：sticky 元素應該比 viewport **明顯短**才會自然

### 按鈕 variant hover 文字不見（`.btn-danger` / `.btn-primary`）（2026-05-28.b）
- **症狀**：`<button class="btn btn-danger">確認刪除</button>` 一 hover，文字消失（變成淺米色背景上的白字）
- **原因**：基底 `.btn:hover { background: var(--c-bg-darker) }` 與 variant `.btn-danger:hover { filter: brightness(0.92) }` 兩個規則 **specificity 相同（都是 class+hover+not，共 30）**。CSS 同優先級下後者只設 `filter`，**沒有 override `background`**，結果 background 取自更晚出現的 `.btn:hover`（淺米色）而 text color 留下 `.btn-danger { color: #fff }`（白）。底色淺 + 字白 → 失明。
- **修正**：variant `:hover` 規則 **必須顯式重設 `background` + `border-color` + `color`**，不能只設 filter / brightness 等視覺效果。`.btn-primary:hover` 也比照辦理（即使現況沒撞到，未來 `.btn:hover` 改色就會中招）。
- **要點**：以後新增 `.btn-*` variant（success / warning ...）一律遵守此規則：
  ```scss
  .btn-warning {
    background: var(--c-warning);
    border-color: var(--c-warning);
    color: #fff;

    &:hover:not(:disabled) {
      // ⚠ 三件套必填
      background: var(--c-warning);
      border-color: var(--c-warning);
      color: #fff;
      // filter / box-shadow / transform 是視覺增強，可選
      filter: brightness(0.92);
    }
  }
  ```
- **延伸**：同樣道理也適用於 `<a class="btn btn-*">`；上一輪已修 `a:not(.btn):hover` 排除按鈕類的 anchor，但 `:hover` 三件套（bg/border/color）才是治本
- **檢查清單**（新增任何 colorful button variant 時 code review 必看）：
  - [ ] base 有 `background / border-color / color`
  - [ ] `:hover:not(:disabled)` 有 `background / border-color / color`（即使值與 base 相同也要寫，因為 `.btn:hover` 會搶 background）
  - [ ] `:disabled` 有 `background / border-color / color`


### QuestPDF `.Height()` + 預設 line-height 會「靜默裁掉」文字（2026-05-29）
- **症狀**：5 個報表 renderer（DataCard / Receipt / Tablet / Text / Worship）用 QuestPDF `.Height()` 把每個 text box 限制成 RDLC 的精確高度後，輸出 PDF **缺字**——Number、所有 label（亡者:/陽上:/地址:/電話:）、phone、prepay、簽名 label 等都沒印出來（pdftotext 驗證修正前只有 ~17 欄位中的 4 個有渲染）
- **原因**：QuestPDF 預設 line-height ≈ 1.2–1.5× 字級，**超過** RDLC 那些貼緊的 box 高度；QuestPDF 遇到放不下的文字會**靜默裁切 / 丟棄**，不報錯
- **修正**：(a) 停止用 `.Height()` 來夾文字；要做 VerticalAlign=Middle/Bottom 改用 translate Y offset 模擬 (b) 每個 text span 設 `.LineHeight(1f)`。修正後 5 個 renderer 全部欄位正確渲染（pdftotext + pdftoppm 影像驗證）
- **要點**：QuestPDF 對位用「絕對座標 + translate」而非「box 高度夾擠」；任何要精確套印的 text 一律 `LineHeight(1f)`，高度由 translate 控制
- **真實案例**：這就是「列印資料還沒套入」的根因（[blueprints/printing-reports.md](blueprints/printing-reports.md)）

### QuestPDF 2026 收回 SkiaSharp Canvas → 垂直地址 / 虛線改自繪 PNG（2026-05-29）
- **症狀**：文牒垂直地址、資料卡虛線（Line2）無法用 QuestPDF 直接畫（2026 版收回了公開 SkiaSharp `Canvas` API）
- **修正**：新增 `SkiaImageHelpers`（`Ceremony.Infrastructure/Reporting/SkiaImageHelpers.cs`）用 SkiaSharp 3.119.4 直接產 PNG：
  - `VerticalAddress` — 1:1 移植舊 [Commons/Library.cs:34-124](../../reference/old/Ceremony/Commons/Library.cs#L34-L124)（System.Drawing → SkiaSharp）：中文直排、`[a-zA-Z0-9\-\(\)]` 旋轉 90°；輸出 25×605px 嵌入 RDLC 座標
  - `DashedLine` — 真虛線 PNG（取代先前 solid 替代）；實線簽名線（Line1）不變
- **要點**：QuestPDF 只負責版面組合，像素級自繪（旋轉文字、虛線）走 SkiaSharp 產 PNG 再 embed

### QuestPDF `FontFamily("BiauKai")` 即使字型已安裝仍 silently fallback（2026-05-29）
- **症狀**：薦牌「尺寸/文字尺寸/位置/字體都不對」。實測 `pdffonts` → PDF 嵌的是 **PingFangTC**（macOS CJK fallback），不是標楷體
- **原因**：renderer 用 `FontFamily("BiauKai")`，但 macOS BiauKai.ttc 的**內部家族名是「標楷體-繁 / BiauKaiTC」**，不叫 "BiauKai" → SkiaSharp 比對不到家族名 → silently fallback。**字型有裝也沒用**，因為查的是「家族名」不是「檔名」。fallback 字寬不同 → 直書換行/尺寸/位置全跑掉（**不是座標錯**）
- **修正**：啟動時 `FontManager.RegisterFontWithCustomName("BiauKai", stream)` 把 OS 字型檔註冊成我們指定的家族名（見 [ReportFonts.cs](../backend/src/Ceremony.Infrastructure/Reporting/ReportFonts.cs)）；找不到字型檔就印警告不 fallback
- **要點**：QuestPDF/Skia 靠「家族名」解析；要嘛用字型內部真名，要嘛自己註冊自訂名。**永遠用 `pdffonts` 驗證實際嵌入字型**，別只看畫面

### 全形 CJK 在「窄欄自動換行」會被 QuestPDF 靜默丟字（2026-05-29）
- **症狀**：薦牌直書姓名整欄消失（換成真標楷體後才出現；fallback PingFang 較窄時剛好沒事）
- **原因**：直書靠「欄寬≈字級 + 自動換行」達成一字一列；但全形標楷體字寬≈1em≈欄寬，QuestPDF 遇「單字（單一 CJK glyph）放不下可用寬度」會**靜默丟棄整段**（同 `.Height()` 裁字那條的水平版）
- **修正**：直書改**顯式每字一行**（`\n` 分隔）且**不要 `.Width()` 約束**；位置仍用 translate。共用工具 [`VerticalText`](../backend/src/Ceremony.Infrastructure/Reporting/VerticalText.cs)（薦牌 + 文牒共用，普桌待補）
- **但完全移除高度約束 → 換成「姓名互相重疊」**：RDLC 次要姓名格 `CanGrow=true`，3 字名直書會溢出到下一格、與下一個姓名重疊
- **縮字策略：整組統一字級（`VerticalText.GroupFontPt`），不要逐格各縮**：
  - 第一版逐格 `FitFontPt`（每格各算各的）→ 同一張牌位**字有大有小**（主欄大、次要欄小），使用者不接受（舊系統是「同大小」）
  - 舊系統字級邏輯是**固定/統一**（薦牌 ParaFontSize：主名>7字→0.6 否則 0.8、3+亡固定0.6；陽上依變體；文牒固定 0.8），同類所有名字同大小
  - 正解：以舊字級為起點，算「每格能容字級 = 可用高/字數」，取**整組最小值**當統一字級套到全組 → **全組同大小**、最擠的也塞得下、不重疊；不需要時完全等於舊字級
- **每格「可用高」用 `VerticalText.Avail`**：次要格只在「正下方有名字」時才以**列距**為界（薦牌往生 1.8639 / 陽上 1.43785；文牒往生 2.06375 / 陽上 1.98436cm），下方空 → 整欄高（不限）。主欄一律整欄高
- **要點**：直書五件事 — (1) 不約束寬度（免水平丟字）(2) 顯式換行（穩定一字一列）(3) **整組統一字級**（不逐格縮，免大小不一）(4) 縮字級不壓行高（5) 可用高用列距且下方空則不限
- **遺留**：Worship（普桌）陽上姓名尚未套用此修正 → 目前不顯示（見 [printing-reports.md](blueprints/printing-reports.md) remaining）；薦牌/文牒 Base 版面僅 5 亡+5 陽格（第 6 名不印，與舊系統一致）

### 姓名中間空格：兩個 helper 語意刻意相反，勿「統一」（2026-06-02）
- **背景**：使用者會在姓名中間刻意輸入空格作**排版間隙**（如「王 大明」），要求列印時保留。`Trim()` 只去頭尾，中間空格進 DB（names 不經全形→半形轉換，半形/全形空格皆留存）
- **兩個 helper 對中間空格的處理刻意相反，各有正確理由，不可合併**：
  - `PrintTemplateSelector.RealCharCount`（**字級門檻**）→ **排除**空格（`char.IsWhiteSpace`）。理由：「> 7 字 → 0.6cm」是衡量**真實姓名長度**，間隙不該讓字級誤縮。**刻意偏離 legacy**（舊 `SignupForm.cs:1179/1203` 用 `Trim().Length` 會計入中間空格）
  - `VerticalText.GroupFontPt` / `Stack`（**直書渲染**）→ **計入**空格。理由：`Stack` 把每個空格渲染成**一整列空白**，`GroupFontPt` 的列數必須與之一致；若少算列數 → 字級沒縮 → Stack 多渲染的列溢出、**蓋到下一格（疊字）**
- **⚠️ 子雷：`GroupFontPt` 算列數必須用 `name.Length`，不可用 `Trim().Length`（2026-06-02 修正）**
  - `Stack` 逐字一列、**完全不 trim**（開頭/結尾/中間空格都渲染成列）；`GroupFontPt` 一度用 `name.Trim().Length`，對**開頭/結尾全形空格**（U+3000，常用來「把名字往下推」作排版縮排）會少算一列 → 字級沒縮 → 溢出蓋下一格
  - **真實案例**：signup `543EA33D-3DFB-472B-8DCF-C8663792F12D` 的 `DeadNameTwo="　蔡炎城"`（開頭 U+3000）渲染 4 列、但 `Trim().Length=3` → 0.6cm 沒縮 → 2.4cm > 列距 1.8639cm → 蓋到下方「蔡貴仁」。改 `name.Length` 後縮到 0.466cm、剛好塞進列距、不蓋
  - **render 路徑不 trim**：`SignupReportContext.Extract` 直接回傳 `s.DeadNames`（不 trim），故 DB 裡的開頭空格會原樣進渲染 → `GroupFontPt` 也必須照原樣算列
- **⚠️ 寫入端也不可 trim 姓名開頭/結尾（2026-06-02 決策，前後端共 5 處）**
  - 原本各層都 `s.Trim()` 會把「開頭全形空格」這種**刻意排版**剝掉 → 舊資料經新系統編輯儲存後，下推排版就遺失
  - 改為**不 trim**、僅純空白（`IsNullOrWhiteSpace` / `s.trim()` falsy）→ null（與 `IsPresent` 一致；長度上限改用實際儲存值驗證）。**這也是刻意偏離 legacy**（舊 `SignupForm.cs:218-232` save 前 `.Trim()`）
  - **已改 5 處**：後端 `CreateSignupHandler` / `UpdateSignupHandler` 的 `NormalizeNames`、`BelieverWriteValidator.NormalizeNames`；前端 `signup-edit-form.component.ts` / `believer-edit-form.component.ts` 的 `livingNames/deadNames .map`
  - **教訓**：「不 trim」必須**前後端一起改**——任一層先 trim 就救不回。新增任何「姓名輸入→儲存」路徑時都要套同一條（純空白才 null，否則原樣）
- **陷阱**：有人日後「順手把兩處統一成同一個字數函式」就會重新引入 bug —— 統一用「排除空格」→ 疊字；統一用「計入空格做門檻」→ 字級誤縮。已用測試鎖住三種語意（`PrintTemplateSelectorTests` 空格 case + `RendererSmokeTests.GroupFontPt_counts_middle_space_as_a_row` / `GroupFontPt_counts_leading_fullwidth_space_as_a_row` / `StackVertical "陳 明"`）
- **要點**：「字數」在本系統有兩個語意 —— **真實姓名長度**（門檻用，排除空格）vs **渲染列數**（排版用，含所有空格、不 trim、須等於 `Stack` 列數）；勿混用

### 移植 GDI+ 文字排版到 SkiaSharp：行高公式不可照搬（2026-05-29）
- **症狀**：文牒垂直地址「字黏在一起」（移植自舊 `Library.cs` 的逐字往下堆疊）
- **原因**：舊 GDI+ 用 `MeasureString(c).Height − 9` 當每字步進；GDI+ MeasureString.Height **膨脹**（含 line gap/padding，≈1.4–1.5× 字級），減 9 後仍 > 字面。SkiaSharp 的 `Descent−Ascent` 已是**緊湊行高**（25px 字 → 25.6px），照搬「−9」變 16.6px < 字面 23px → 重疊
- **修正**：步進改用**字型行高本身**（`Descent−Ascent`，不再 −9/−10）
- **要點**：跨繪圖引擎移植排版時，magic number（如 −9）通常綁定原引擎的度量定義，不能直接搬；改用語意等價的度量（行高、advance）重算

### 部署機字型 bundling（標楷體 / TW-Kai / DFKai-SB）（2026-05-29）
- **症狀**：QuestPDF **與** SkiaSharp **都**需要標楷體（BiauKai）；部署機（Windows / Electron sidecar）若沒裝，文字與垂直地址會 fallback 成錯誤字型
- **修正**：部署機必須 bundle TW-Kai 或 DFKai-SB（或標楷體）；`ReportFonts` 啟動解析字型檔路徑並註冊進 QuestPDF，候選含 Windows `kaiu.ttf` / macOS BiauKai.ttc / Linux TW-Kai，可用環境變數 `CEREMONY_KAI_FONT` 覆寫
- **⚠️ QuestPDF 與 SkiaSharp 是兩條獨立的字型解析路徑**：`ReportFonts` 把字型註冊進 QuestPDF 的 `FontManager`，**救不到** SkiaSharp 的 `SKTypeface.FromFamilyName("BiauKai")`（後者查 OS 家族名，macOS 叫「標楷體-繁」找不到 → `SKTypeface.Default` 無中文 → 文牒垂直地址整排 **tofu 方框**）。SkiaSharp 路徑必須改用 `SKTypeface.FromFile(ReportFonts.ResolvedPath)` 載**同一個字型檔**（見 `SkiaImageHelpers.LoadKaiTypeface`）
- **要點**：兩條繪圖路徑都吃字型，但**各自解析**；打包時確認字型檔存在，且兩邊都用「檔案路徑」載入而非靠 OS 家族名

### worship2.png 以 EmbeddedResource 內嵌（2026-05-29）
- **要點**：普桌背景 `worship2.png` copy 到 `Ceremony.Infrastructure/Reporting/Assets/worship2.png` 設為 `EmbeddedResource`，runtime 載一次當底層繪製（Top 0.26141 Left 0.42 W 20.04729 H 28.88438 cm，FitProportional）；不要依賴外部檔路徑，避免部署遺漏

### DB 備份路徑屬「SQL Server 主機」而非 API 執行機（2026-05-29）
- **症狀**：呼叫備份回 500 `Cannot open backup device 'D:\Backup\/2026….bak'. Operating system error 5 (Access is denied)`，路徑出現混用分隔符 `\/`。
- **根因 1（分隔符）**：`Path.Combine(directory, fileName)` 用的是「API 執行機」的分隔符。API 跑在 macOS/Linux、但 `Backup:Directory` 是 Windows 風格（`D:\Backup\`）時，Path.Combine 會接出 `D:\Backup\/file`。
  - **修正**：改用 `SqlBackupService.JoinForSqlServer()` — 目錄含 `\` → 用 `\`（Windows），否則用 `/`（Unix）；以 **SQL Server 主機**的風格組路徑。
- **根因 2（目錄建立）**：舊 code `Directory.CreateDirectory("D:\\Backup\\")` 在 macOS 會建出一個**名稱含反斜線的垃圾資料夾** `src/Ceremony.Api/D:\Backup\`，而該反斜線目錄名會讓 MSBuild 的 `**/*.resx` glob 列舉失敗 → 整個 Api 專案 build 噴 `MSB3552 Resource file "**/*.resx" cannot be found`（`dotnet build slnx` 有時用快取看不出來）。
  - **修正**：`Directory.CreateDirectory` 改 best-effort（try/catch）。sidecar 架構下 SQL Server 與 API 常不同機（DB 在容器 / 遠端），備份目錄屬於 DB 主機檔案系統，由 DBA 預建並授權 SQL Server 服務帳號，API 端不該硬建。
  - **善後**：若已誤建，刪除 `find src -type d -name 'D:*' -exec rm -rf {} +`。
- **dev 設定**：dev DB 是 `(local)` Docker Linux MSSQL → 無 `D:\`；`appsettings.Development.json` 的 `Backup:Directory` 設為容器可寫的 `/var/opt/mssql/data/`。prod Windows 才用 `D:\Backup\` 或 UNC。
- **size 回報**：`.bak` 落在 DB 主機，API 不同機時 `File.Exists` 看不到 → `sizeBytes` 改 fallback 查 `msdb.dbo.backupset.backup_size`（File 可見時仍以實檔大小優先）。

### 清交易紀錄檔必須依 recovery model（2026-05-29）
- **重點**：`clearLog=true` 清交易紀錄檔不能「只 `DBCC SHRINKFILE`」。在 **FULL / BULK_LOGGED** recovery model 下，log 未截斷前 shrink 不會釋放空間 → 必須先 `BACKUP LOG`（正確截斷、保留 `.trn`）再 shrink。**SIMPLE** 才可只 `CHECKPOINT` + shrink。
- **不可**用 `BACKUP LOG ... TO DISK = N'NUL'` 丟棄式截斷：會**破壞還原鏈**（等同舊 `TRUNCATE_ONLY`，DBA 反模式）。本實作在完整備份成功後才清，且 FULL 保留 `.trn` 以續鏈。
- 由 `SqlBackupService.BuildClearLog(recoveryModel, logName, dbName, dir, now)` 純函式產 SQL（已單元測試 SIMPLE/FULL/BULK_LOGGED + 分隔符 + 跳脫）。清 log 失敗以 try/catch 吞掉、回 `logCleared=false`+`logClearError`，**不讓已成功的備份連帶失敗**。

### 報表「編號欄」字串格式各報表不同（2026-06-02 交叉稽核修正）
- **踩雷**：新版 `SignupReportContext.Extract` 一律組成 `{NumberTitle}-{號}`（連字號）給 5 種報表共用。但舊 `SignupForm.btnPrint_Click`（SignupForm.cs:488-637）每種報表格式**都不一樣**：
  - **資料卡 datacard**：`NumberTitle + "." + 號`（點分隔，例 `光明燈.123`）
  - **收據 receipt**：**只印號碼、無 NumberTitle**（例 `123`）← 新版錯加了 title
  - **薦牌 tablet / 文牒 text**：`SignupType==2（寺方）→ 只印 NumberTitle`，否則 `NumberTitle+號`（無分隔，例 `寺` 或 `光明燈123`）← 新版漏掉 type==2 特例
  - **普桌 worship**：`NumberTitle + 號`（無分隔，例 `普123`）
- **連字號全錯**：沒有任何一種報表用 `-`。**修正**：`SignupReportContext` 改 per-type 方法（`DataCardNumber`/`ReceiptNumber`/`TabletTextNumber`/`WorshipNumber`），`ReportModelBuilders` 各自取用；新增 `ReportNumberFormatTests` 9 case 鎖住。
- **教訓**：「編號」這種看似單純的欄位，舊系統在不同列印路徑有**刻意的格式差異**（尤其寺方只印 title），抽共用 helper 時不可一刀切。

### SignupLog 預設排序是 DESC（最新在前），不是 ASC（2026-06-02）
- **踩雷**：`SignupLogRepository` 用 `ORDER BY Createdate ASC`（最舊在前），但舊 `SignupLogForm.LoadSignupLog`（:41）是 `OrderByDescending(Createdate)`（最新在前）→ 操作員看到的歷程順序跟舊系統**完全相反**。前端 `signup-logs-page` 不 re-sort，直接照收到順序顯示，差異直接呈現給使用者。
- **修正**：改 `ORDER BY Createdate DESC`。doc（get-signup-logs.md / signup-log-form.md）同步。

### 地址 city→area 連動下拉要每個表單都接（2026-06-02）
- **踩雷**：報名表單（signup-edit-form）做了城市→區域 cascade，但**信眾表單（believer-edit-form）漏接**，仍是「郵遞區號 ID」數字輸入框 → 操作員得知道 FK 整數才能填。交叉稽核才發現。
- **修正**：把 signup 表單的 `onCityChange`/`applyAddress`/`refreshZipcode`/`onSameMailAddressChange` 移植到 believer 表單；form 內 zipcodeId 以**字串**持有、submit 轉 number（`BelieverUpsertRequest` 契約不變）。
- **教訓**：同類 UI 模式（地址、名單、避4 顯示）在多個 CRUD 表單要逐一確認都接上，別假設「報名做了信眾就有」。

### Electron 包裝 / Sidecar（2026-06-02）

- **`get-port` v7+ 是 ESM-only，CJS Electron main `require` 會炸**：改用 node `net` 自寫 `findFreePort()`（[sidecar.ts](../frontend/electron/sidecar.ts)）。一般凡是 sindresorhus 系列新版多半轉純 ESM，CJS main 引用前先確認。
- **`environment.apiBaseUrl` 必須在 `bootstrapApplication` 之前覆寫**：各 `*.api.ts` 用 `private readonly base = ${environment.apiBaseUrl}/...` 在 **DI 建構時**讀取 → 只要在 bootstrap 前 mutate 那個 mutable const 就生效；bootstrap 後才改則已建構的 service 抓不到。sidecar 動態 port 經 `?apiBase=` query 傳入（[src/main.ts](../frontend/src/main.ts)）。
- **Angular `ng build` 預設 `<base href="/">`，file:// 載入會 404**：Electron 打包用 `ng build --base-href ./`（package.json `build:renderer`），否則 `index.html` 的絕對路徑資源在 `file://` 下找不到。搭配既有 `withHashLocation()` 路由。
- **CanActivate guard 內 `await` 後不能再 `inject()`**：injection context 在第一個 await 後失效 → 必須先 `const router = inject(Router)` 再 await（[electron-ready.guard.ts](../frontend/src/app/core/platform/electron-ready.guard.ts)）。
- **sidecar renderer 從 `file://` 載入 → fetch 的 Origin header 多為字串 `"null"`**：後端 CORS 要明確 allow `null` 與 `file://`（Electron 啟動時注入 `Cors__AllowedOrigins__0=null` / `__1=file://`），否則 API 全被 CORS 擋。
- **framework-dependent .NET 10 sidecar 需 client 裝 .NET 10 ASP.NET Core Runtime**：不是 self-contained，缺 runtime spawn 會 ENOENT/啟動失敗 → 開機 prereq 偵測（`dotnet --list-runtimes` 找 `Microsoft.AspNetCore.App 10.*`）先擋；SkiaSharp 列印另需 VC++ Redistributable（registry 偵測）。
- **備份 `.bak` 在「DB 主機」檔案系統，client API process 不一定讀得到**：下載 endpoint 需 `Backup:Directory` 對 API process 可讀；prod sidecar 走 **UNC 共用**，dev docker 容器內路徑讀不到 → 404（已知限制，非 bug）。
- **大 `.bak`（~100MB+）下載別在 renderer 抓 blob**：Electron 走 main `net` 串流寫檔（[download.ts](../frontend/electron/download.ts)）；只有瀏覽器 fallback 才用 blob + `<a download>`。
- **`backend/global.json` 把 SDK pin 在 `10.0.103`，但打包機常只有 `10.0.102`**：`dotnet publish` 會先印 `A compatible .NET SDK was not found / Requested SDK version: 10.0.103` 警告，靠 rollForward 仍能完成（易誤判為失敗）。修法：把 version 降到實際安裝版本，或加 `"rollForward": "latestFeature"` 明確允許。
- **Windows 打包別直接跑 `npm run dist`**：該 script 是 bash 寫法（`bash ../backend/publish.sh`、`CEREMONY_RENDERER_URL=...` 前綴），Windows 原生殼會炸。改分步：`npm install` → `pwsh backend/publish.ps1` → `npm run electron:build` → `npx electron-builder --win`。產物在 `frontend/release/`。
- **`EnableCompressionInSingleFile` 不能配 framework-dependent**：publish.ps1/.sh 是 `--self-contained false`（framework-dependent）+ `PublishSingleFile=true`。新 SDK（10.0.300）對 `EnableCompressionInSingleFile=true` 直接報 `NETSDK1176: Compression in a single file bundle is only supported when publishing a self-contained application`（舊 SDK 只警告）。修法：移除該旗標（壓縮本來就只對 self-contained 有效）。**CI 跑得到的 SDK 比本機新時最易踩**。
- **CI `npm ci` EUSAGE（lock 不同步）**：`package-lock.json` 與 `package.json` 對不上（如 `@emnapi/*` transitive 版本漂移）→ `npm ci` 直接 fail（比 `npm install` 嚴格）。修法：本機 `npm install --package-lock-only` 重新同步後 commit。手動編輯/部分整理 lock 容易留不一致。
- **NSIS 安裝資料夾名 = `productName`（中文）**：要固定英文資料夾又不改 productName，用 `nsis.include` 指向自訂 `.nsh`，在 `!macro preInit` 用 `WriteRegExpandStr ... InstallLocation "$PROGRAMFILES64\Ceremony"`（HKLM+HKCU、SetRegView 64+32 都寫）。改 productName 會連帶改 app/捷徑/開始功能表名，通常不是你要的。
- **出廠連線種子 `frontend/build/default-config.json` 沒放就沒效**：首次啟動會退回 `/setup`（非錯誤，是 fallback）。打包前要先從 `default-config.example.json` 複製並填真實連線；它已 gitignore（含 sa 密碼，**絕不入 repo**），所以 clone 出來的 repo 一定缺，需手動建。種子只放連線，`jwtKey` 由 `writeConfig` 每機自動產生（別寫進種子）。
- **安裝檔內含明文 sa 密碼**：出廠預寫連線後，`resources/default-config.json` 在 installer 內可被解出 → 安裝檔限內部交付勿外流（取捨見 [security.md](design/security.md)）。
- **🔴 spawn single-file sidecar 一定要設 `cwd`，否則 appsettings.json 不載入**：ASP.NET Core single-file exe 的 ContentRoot 取自**工作目錄**（不是 exe 路徑）。`sidecar.ts` 若 `spawn(exe, …)` 不帶 `cwd`，ContentRoot 會變成 Electron 的 cwd（如 repo root / 安裝目錄）→ 找不到同層 `appsettings.json` → `Backup:Directory`、`Cors`、`Jwt:Issuer/Audience` 等全為 null。實際後果：**「資料備份」回 500 `BACKUP_NOT_CONFIGURED: Backup:Directory 未設定`**（連 backdoor 登入仍可，因 `SuperAdminEnabled` 有 code 預設值，故易被誤判成只有備份壞）。修法：`spawn` 帶 `cwd = path.dirname(exe)`（packaged = `resources/api`）。診斷招：看 API 啟動 log 的 `Content root path:` 是否指向 exe 所在資料夾。
- **「立即備份」直接寫 `Backup:Directory`（D:\Backup），不跳選資料夾**：寺方為同機部署（程式裝在 DB 主機上），.bak 由 SQL Server 寫本機 D:\Backup 即可，使用者不需選位置（2026-06-02 決策，撤回先前「先選位置再備份」）。`D:\Backup` 須存在且 SQL Server 服務帳號（`NT Service\MSSQLSERVER`）可寫，否則 BACKUP DATABASE 即時失敗（`Cannot open backup device … Operating system error 3/5`）。dev 機可 `icacls "D:\Backup" /grant "NT Service\MSSQLSERVER:(OI)(CI)M"`。`electron/download.ts` 的下載另存仍保留為備用能力（UI 未掛）。

## 反模式速查

| 反模式 | 為什麼不好 | 替代做法 |
|---|---|---|
| 在 doc 內寫「請呼叫 X agent」 | agent 改名要改一堆 | 只在 frontmatter `related_agents` 維護 |
| `get-port` 進 CJS Electron main | ESM-only，require 會炸 | 用 node `net` 自寫 findFreePort |
| bootstrap 後才改 apiBaseUrl | api service 已建構抓不到 | bootstrap 前 mutate environment |
| Electron 打包忘了 `--base-href ./` | file:// 絕對路徑資源 404 | build:renderer 帶 `--base-href ./` |
| CLAUDE.md 塞滿細節 | 吃 context 預算 | 細節分散到 docs/，本檔只放索引 |
| blueprint 一寫完就放著 | 文件腐化 | 變更時更新 `last_updated` 與 `status` |
| 改 code 不更新 doc | 設計與實作脫節 | 依「文件同步規則」對照影響面 |
| variant button `:hover` 只設 filter | `.btn:hover` 搶走 background → 失明 | `:hover` 三件套：bg / border / color 全寫，filter 是輔助 |
