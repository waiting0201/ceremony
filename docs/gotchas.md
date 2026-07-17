---
title: Gotchas
purpose: 紀錄已知陷阱、反模式、踩雷紀錄，避免重複犯錯
applicable_when: 開始實作前的 sanity check、遇到奇怪問題時、code review 時
related_agents:
  - qa-test-engineer
  - code-review-optimizer
related_docs:
  - conventions.md
keywords: [gotchas, 陷阱, 踩雷, 反模式, anti-pattern, 對比度, WCAG, a11y]
last_updated: 2026-07-17 (追加：印表機不可列印邊界會整欄吃掉 Left<0.5cm 的欄位；先前：插入並順移用 set-based UPDATE、薦牌實體對位條結案、色彩對比度要實測)
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

### 印表機不可列印邊界會「整欄吃掉」Left < 0.5cm 的欄位（2026-07-17）
- **症狀**：薦牌 5 位陽上實印只出現 3 位（`reference/薦牌.jpg` 郵27）；編號「郵」字左半被裁。PDF 本身完全正常——只有實體列印才看得出來
- **真因**：RDLC 沿襲的座標把最左欄放在 Left=0.1cm、編號放在 0.1cm，落在使用者印表機的不可列印邊界（估 ≥0.4-0.5cm）內；QuestPDF/PDF 檢視器不會警告
- **預防**：**滿版（Margin 0）報表的任何欄位離紙緣至少 0.5cm**；驗收不能只看 PDF，要實機列印。薦牌已把陽上矩陣左界改 0.5、編號 Left 改 0.5（見 [printing-reports-positions.md](blueprints/printing-reports-positions.md) §3 2026-07-17 條）
- **殘留風險**：薦牌 2 位陽上變體（OneTwo/TwoTwo/UnderscoreTwo）l[1] Left=0.30611 也在風險區，尚未接獲客訴、未改；若再有「少印一欄」回報先查這裡

### 「文字顏色不清楚」抱怨要先算對比度，不要憑感覺調色（2026-07-04）
- **症狀**：使用者反映「文字太小、顏色不清楚」，2026-07-02 已單純把全部字級 +1px 處理過一次，這次同樣抱怨又出現
- **真因**：問題不是（只是）字級，是 `--c-text-secondary`(#7A7466) 對主背景/grid 偶數列的 WCAG 對比度只有 4.41:1/4.38:1，未達一般文字門檻 4.5:1；`.btn-primary` 白字在 `--c-primary`(#CC785C) 底色上更只有 3.28:1。純數字算過才知道，光看螢幕不見得能看出差 0.1-1.2 的落差
- **預防**：色彩 token 一改動或有「看不清楚」類抱怨，先用 WCAG 對比公式（`(L1+0.05)/(L2+0.05)`）實測所有「文字色 on 背景色」「白字 on 按鈕底色」組合，門檻：一般文字 4.5:1、大字/UI 元件 3:1；不要只憑肉眼或加深一階就假設夠了
- **後續**：修法見 [visual-design.md](design/visual-design.md) 色彩表；`--c-border` 對比僅 1.51:1（遠低於非文字元件建議 3:1）本次故意不修（修到位需大幅加深、視覺重量改變太大），留待下次視覺設計討論再評估

### 文件承諾的功能沒做，會被誤認成「還沒修好」（2026-07-04）
- **症狀**：[visual-design.md](design/visual-design.md) 「響應式（DPI / 縮放）」段寫「提供 100% / 125% / 150% 切換」，但 grep 全 repo 找不到任何相關實作
- **原因**：文件在早期規劃階段就寫了這個承諾，但一直沒排進實作，也沒有人回頭把文件改成「待做」狀態，讓文件長期宣稱一個不存在的功能
- **預防**：doc 裡寫「提供 XXX」等於對外承諾存在的功能；如果還沒做，要嘛盡快做，要嘛把敘述改成「規劃中/backlog」，不要讓「已規劃」跟「已實作」在文件裡長得一樣
- **檢測**：使用者反映功能「沒作用」或「找不到」時，先 grep 程式碼確認文件描述是否真的有對應實作，不要預設文件是對的

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

### SkiaSharp `IsAntialias=true` 在窄小點陣圖上看起來偏灰、不是純黑（2026-07-02）
- **症狀**：客戶拿舊系統實際列印的文牒樣張（`reference/文牒問題.pdf`）手寫回饋「地址要再黑一點」
- **原因**：`SkiaImageHelpers.VerticalAddress` 產的垂直地址只有 25×605px，字級 25px 在這麼小的點陣尺寸下，抗鋸齒邊緣的半透明灰階像素占整個字形的比例偏高，肉眼觀感明顯偏灰
- **修正**：改 `SKFont.Edging=SKFontEdging.Alias` + `SKPaint.IsAntialias=false`，讓每個有畫到的像素非黑即透明（同檔案 `DashedLine` 本來就用 `IsAntialias=false`，只是先前 `VerticalAddress` 沒套用同樣的選擇）
- **要點**：小尺寸點陣圖（窄欄、低解析度）畫黑白文字/線條時，**關掉抗鋸齒**通常比開著更接近「印出來是純黑」的視覺預期；抗鋸齒是為了「螢幕上曲線更平滑」，不是為了「印表機上更黑」，兩者目標會衝突
- **真實案例**：[printing-reports.md](blueprints/printing-reports.md)「文牒兩項客戶回饋修正」段；回歸測試 `Skia_VerticalAddress_NoAntiAliasedGrayEdges`

### 往生字級被拖累：「跨組取交集對齊」聽起來公平，實際上是犧牲沒問題的那組（2026-07-02）
- **症狀**：文牒往生（DeadName）與陽上（LivingName）姓名字級各自獨立呼叫 `VerticalText.GroupFontPt` 縮字（各自算「自己那組不重疊」的安全上限）。客戶反映紙本上兩組字級看起來不一樣大，要求「往生跟陽上一樣大」
- **踩過的坑**：第一次修正直覺想「兩組各自算完安全上限後取較小值（`Math.Min`）套用到兩組」，覺得這樣「兩組一樣大」又「兩組都不重疊」可以兼顧。上線前用一筆 dev DB 真實資料（5 位往生、其中 2 位名字開頭帶全形空格排版間隙）產出 PDF 檢視，才發現：往生因為次要格擁擠被迫縮到 ~0.516cm，取交集後**陽上（黃清霞，本來可以維持 0.8cm）也被拖小到 0.516cm**——但陽上自己完全沒有擁擠問題，是被往生「連坐」縮小的，客戶明確表示不要這樣（要放大往生，不是縮小陽上）
- **why 這是硬限制、不是程式錯誤**：往生次要格可用高度＝列距（如 2.06375cm），名字行數（含刻意保留的開頭全形空格，一格算一行）撐到 4 行時，字級上限就是「列距 ÷ 行數」，物理上無法再放大而不疊到下一格。姓名字數不多時兩組本來就會自然一樣大（同一 0.8cm 基準各自獨立算，沒縮就都是 0.8cm）；只有姓名擁擠時往生才會比陽上小，這種情況下**縮小陽上去「配合」擁擠的往生並不能解決問題，只是把好端端的一組也弄糟**
- **正解**：兩組完全獨立計算，不要跨組取交集。姓名不多時自然一致（滿足「一樣大」的常見情境）；姓名擁擠時只讓需要縮的那組自己縮，不牽連另一組
- **要點**：遇到「兩個獨立算安全值的分組，客戶要求視覺一致」時，先用真實的、會讓其中一組被迫收斂的資料實際產出檢視，不要只用理想化的短資料驗證就假設「取交集」是安全的萬用解——那看似公平的作法在真實資料上可能是犧牲了原本沒問題的一方
- **真實案例**：[printing-reports.md](blueprints/printing-reports.md)「文牒兩項客戶回饋修正」段（含撤回 `Harmonize` 的完整過程）；回歸測試 `Text_DeadAndLivingFontSizes_MatchWhenNeitherNeedsShrinking` + `Text_DeadNameShrinks_WithoutDraggingDownLivingName`

### 薦牌實體對位：座標跟 RDLC 1:1 吻合、PDF 本身無重疊，客戶仍反映列印歪（**✅ 已結案，2026-07-04 使用者確認 OK**）
- **症狀**：客戶提供 `reference/薦牌問題.pdf`（手寫註記照片）反映薦牌實際列印紙條插入蓮花瓶牌位座後，文字位置對不準——跑到視窗外、蓋到雕花邊框；跟 [[往生字級被拖累]] 那條（`reference/文牒問題.pdf`）同一個蔡家測試資料，但薦牌（[TabletRenderer](../backend/src/Ceremony.Infrastructure/Reporting/TabletRenderer.cs)）跟文牒（TextRenderer）是不同 renderer、不同物理列印品
- **已排除的可能性**：
  1. **座標搬移手誤**：逐行比對 `TabletRenderer.cs` vs 原始 `tmpTablet.rdlc` XML，所有 Top/Left 值 1:1 吻合（含 Rectangle 巢狀換算）
  2. **PDF 內部排版錯誤**：用同一場景（2 亡 3 陽，`tmpTabletTwo` 變體）實際跑 `TabletRenderer` 轉圖檢視，文字不重疊、不超出 11.5×25.4cm 頁面邊界
  3. **薦牌那輪「開頭全形空格漏算列數」的疊字 bug**（見上方「姓名中間空格」條）：`VerticalText.GroupFontPt` 是薦牌/文牒共用的 helper，該修正已經套用，薦牌不會重蹈覆轍
- **推論**：問題出在「RDLC 當年校準的牌位座實體尺寸」與「客戶現有牌位座」不一致（供應商換過牌位座樣式、或原始校準本來就有落差），屬於**紙條 vs 牌位座視窗的實體對位問題**，不是排版邏輯錯誤——這種問題**無法只靠看 PDF 校正**，原本判斷需要拿實體牌位座量測
- **✅ 已做的診斷工具**：`TabletRenderer.Render(data, debugGrid: true)` 疊一層桃紅色 1cm 刻度格線（`DrawCalibrationGrid`，不進生產列印路徑，預設 `false`）。回歸鎖：`Tablet_DebugGrid_ForRealComplaintScenario_DumpsCalibrationPdf`
- **✅ 2026-07-03 用樣板照片量測、修正了其中一個確定的 bug**：新增的 `debugOverlay` 開發用檢視工具（見 [printing-reports.md](blueprints/printing-reports.md)）疊上 `reference/template/薦牌.jpg`（200 DPI 實體樣板掃描照）後，用像素分析精確量出雕花窗框內緣座標（Y: 6.2294cm ~ 16.0782cm）。發現 Base/UnderscoreOne/UnderscoreTwo 變體主欄（One，無第 6 位搭配時）原本的可用高度 `deadFull=11.0331cm`（RDLC 抽出值，從 top=7.5825 算到 18.6156cm）比窗框內緣底部**多出約 2.5cm**——14 字以上長名字觸發縮字後仍會被印到窗框外，這是可以直接從量測值反推、不需要實體猜測的**真實 porting 落差**（不是「牌位座供應商換款式」那種無法從資料反推的物理問題）。已改為量測值 `deadFull=8.4957`（`TabletRenderer.DrawDeadNames` default 分支），回歸鎖 `Tablet_Base_LongDeadName_StaysWithinMeasuredWindow`
- **✅ 2026-07-05 亡者欄位改用「故／靈位」字符中心線動態置中，取代固定座標微調**：使用者反映「薦牌亡者的列印沒有很正」。第一輪用 `debugOverlay` 疊圖逐一實測發現 Two 變體（`DeadNameTwo Left=4.2`）跟 Base 變體（`DeadNameThree/Five Left=4.0`）都壓到窗框邊緣（Base 更嚴重，直接印到邊框外），先用固定值 `Left=4.34`／`4.25` 修正。使用者接著給出更完整、有原則的規則：**以樣板紙預印的「故」「靈位」兩組靜態字的字符中心線為排版基準**——1 位亡者完全置中在中心線、2 位分居中心線左右、3+ 位沿用 2×3 矩陣但中間欄置中在中心線。量出中心線 `X=5.685cm`（「故」bounding box 中心 5.6769cm 與「靈位」中心 5.696cm 幾乎重合，且跟窗框幾何中心 5.677cm 互相印證），並把這條線疊回**無渲染文字的原始樣板照片**核對，精確貫穿兩組字的視覺中心。**關鍵改法**：位置改成「先算 `GroupFontPt` 共用字級，再用字級動態算置中座標」，取代「不管字級縮多小、位置都是編譯期固定常數」的舊做法——這樣縮字後置中點不會偏移，字級縮小時欄位間距也自動變寬，一併解決了 Base 變體先前「只能剛好清邊框」的取捨疑慮。回歸見 `Tablet_OneDeadName_DumpsCenteredOverlay`、`Tablet_DebugGrid_ForRealComplaintScenario_DumpsCalibrationPdf`、`Tablet_Base_ThreeDeadNames_DumpsThreeColumnOverlay`
- **⚠️ 2026-07-05 猜錯「置中」的方向，多繞了一輪**：使用者驗收後指出「只有一位時，亡者位置沒在故靈位正中間」，第一次改法猜成「Y 座標也要置中」——把文字區塊整體垂直置中在「故」下緣～「靈」上緣的空隙裡（`Top = 故下緣 + (空隙高度 − 文字高度) / 2`）。使用者立刻糾正：**「還是不對，要在故的正下方」**——原意其實是「水平方向在故／靈位共用的中心線上」，垂直方向應該**緊接在故正下方**，不是漂浮在故跟靈位中間的空白處。改回 `Top = 故下緣量測值 7.5946`（等於保留原本貼著故下緣起排的邏輯，只補上水平置中；跟改版前的舊值 `7.5825` 幾乎相同）。`GroupFontPt` 的 avail 收緊到「故～靈位空隙 5.8674cm − 0.1 安全邊界」這項改動沒有被推翻，繼續保留（避免長名字縮字上限超出「靈」字上緣）。
- **要點**：使用者說「在 A／B 中間」時，不要預設是「A、B 之間空隙的正中央」（2D 完全置中）——很可能是「跟 A／B 共用的某條軸線對齊」（這裡是水平中心線），另一個維度另有更明確的錨點（這裡是「緊接在 A 正下方」）。不確定哪種意思時，與其自行猜一個「聽起來更完整」的版本（2D 置中聽起來比「靠一邊」更像「置中」），不如先用疊圖產出目前的理解、請使用者一次確認，省掉來回猜測的成本。
- **✅ 結案（2026-07-04）**：使用者確認薦牌目前列印結果 OK——「樣板照片是否 100% 對應客戶實際牌位座」的最後疑慮由實際驗收解除。`debugGrid` / `debugOverlay` 工具保留，日後換牌位座樣式或再有對位客訴時沿用同一套「疊圖量測 → 修正 → 實體驗收」流程
- **要點**：牌位座 / 印章位 / 任何「紙本插入實體外殼」的列印品，座標正確 ≠ 對位正確；PDF 頁面邊界只保證「我們畫的東西彼此不重疊、不超出紙張」，保證不了「紙張放進外殼後，畫的東西落在外殼開窗範圍內」——**有實體樣板照片可量測時，可以先修正「量測後仍明顯超出邊界」這種高把握的落差，但最終仍要有一次實機測試收斂**，不能只憑照片就宣告解決（本案最終即由使用者實際驗收才結案）

### QuestPDF `page.Content()` 內的座標會被 `page.Margin(...)` 硬裁切，`page.Background()` 才是「整張紙」座標系（2026-07-05）
- **症狀**：`debugOverlay` 開發工具在 `TabletTemplate.OneOne`（上下各 2cm Page Margin 的特例變體）疊圖時，樣板照片只填滿內容區（21.5cm），上下各留一截明顯空白——使用者反映「template 變比較小，上下也跟右有留白」，兩次都不滿意「已經改用量測值/這是 QuestPDF 限制」的解釋，要求「參考三位亡者（無 margin 變體）的疊圖方式再確認」
- **踩過的坑**：想把疊圖用負值 `TranslateY(-2cm)` 往上位移、蓋滿整張紙（含 margin 區域），但這是在 `page.Content().Layers(...)` 底下操作——**QuestPDF 的 `page.Content()` 座標系統會被 `page.Margin(...)` 硬裁切，超出內容區邊界的內容（即使用負值位移試圖畫到 margin 區域）會整層直接消失**，不是縮小或警告，是完全不渲染。用「疊圖版 PDF 位元組數 vs 不疊圖版」比對可以抓到這種「整層被裁掉」的靜默失敗（兩者位元組數完全相同）
- **正解**：QuestPDF 另外提供 `page.Background(...)`，是畫在「整張實體紙」的座標系統，**不受 `page.Content()` 的 Margin 影響**。把疊圖從 `page.Content()` 內的 `Layer` 移到 `page.Background().Image(...).FitUnproportionally()`，`Margin` 區域也能正常顯示。副作用：`page.Content()` 裡原本的 `layers.PrimaryLayer().Background("#FFFFFF")` 白底會蓋掉 `page.Background()` 疊的圖，`debugOverlay=true` 時要改用 `Colors.Transparent`（仍要保留 `PrimaryLayer()` 呼叫本身，用來維持 `Layers` 容器的尺寸）
- **要點**：遇到「這是第三方套件的技術限制、改不了」這種結論時要小心——很可能只是「我試的這條路走不通」，不代表沒有其他路徑。這次的訊號是「同一個套件裡，沒有 Margin 的變體疊圖完全正常」——如果真的是套件的硬限制，理論上不管有沒有 Margin 都會遇到同樣問題；只有「有 Margin 的那個變體」出問題，代表問題出在 Margin 這個變因本身，值得針對這個變因再查一次 API，而不是直接放棄
- **後續追加（同日）：QuestPDF 的 Margin 裁切行為依「元素類型」而定，不是統一規則**：疊圖修好後，使用者馬上發現 OneOne 變體的 Number／陽上／亡者三處文字 Y 軸位置都不對——因為這三處分別跟「沒有 Margin」的其他變體（TwoOne/UnderscoreOne/OneTwo/One）共用同一個座標常數，只有 OneOne 有 2cm Margin，導致印出來的實體位置比其他變體多低 2cm。這個錯位其實從一開始就存在，只是被舊版疊圖工具（只顯示裁切後的內容區）巧合遮住，蓋滿整張紙後才第一次真正被看見。修法：三處都改成 `content-Y = 原始值 - (OneOne ? 2.0 : 0.0)`。**原本擔心負值 Y 會像先前的 Image 疊圖一樣被整層裁掉，但實測文字（`Text` fluent API）完全不會被裁**——同一個 `page.Content()` 座標系統，`Image` 疊圖踩到裁切、`Text` 卻沒事。**要點**：不要把「A 元素在 page.Content() 裡超出 Margin 會被裁」這個觀察，直接類推到「所有元素都會被裁」；不同 fluent API（`Image` vs `Text`）對超出邊界內容的處理可能不一樣，換元素類型時要重新實測，不能只憑同一頁學到的規則。

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
- **安裝包是 x64-only，裝到 32 位元 Windows 會報「不是正確的 Win32 應用程式」**：`electron-builder.yml` `win.target.arch` 只有 `[x64]`，後端 sidecar 也只 publish `win-x64`；32 位元 OS 的載入器連 PE header 都解不了，這訊息是 Windows 對「執行檔架構不合」的通用錯誤，**與檔案有沒有正確複製、有沒有跑正式 NSIS 安裝檔無關**。**2026-07-01 決策**：暫不建 x86（32 位元）版安裝包（工程量不小：後端要多一條 `win-x86` publish、electron-builder 要加 `ia32` target、還要另外準備 32 位元版 ASP.NET Core Runtime / VC++ Redistributable 安裝檔，且 SkiaSharp/QuestPDF 原生庫是否有 win-x86 版尚未確認）；先擱置，改建議該 client 換 64 位元機器（32 位元 Windows 已停產多年，.NET 10 對 x86 支援也弱）。

- **預繳配號的計數器一律 `nextNo = 固定號 + 1`（含往回設）— 刻意對齊舊系統的邊界 bug**：舊 `LoadPrepayForm` 每處理一個固定編號後一律把計數器設為 `固定號 + 1`（[LoadPrepayForm.cs:132/136](../reference/old/Ceremony/LoadPrepayForm.cs#L132)）。當固定號**小於**當前計數器（僅在「目標年/法會已有既存資料 `MAX(Number)>0`、且該固定信眾的固定號落在既存範圍內」才發生）時，舊系統會把計數器「往回設」，理論上可能把既存編號重新當成 gap 配給非固定信眾 → 產生重號。這是舊系統的 latent bug，但**正常年初對空法會載入（`MAX=0`）不會觸發**，且新舊輸出在該常見情境完全一致。`PrepayNumberAllocator` **刻意保留 `nextNo = n + 1`（不用 `Math.Max`）** 以完全對齊舊輸出（回歸鎖 `PrepayNumberAllocatorTests.LegacyBackwardSet_*`）。若未來要「修掉」這個 latent bug，需與業務確認能否偏離舊行為，屆時改回 `Math.Max` 並更新該測試。
- **「插入並順移」順移既有列可用單句 set-based UPDATE，因為 `(Year,Cat,Type,Number)` 無 unique index**：`SignupRepository.InsertWithShiftAsync` 用 `UPDATE dbo.Signups SET Number = Number + 1 WHERE ... AND Number >= @N` 一句把插入點其後全部往上推。因為 DB 層**沒有** `(Year,CeremonyCategoryID,SignupType,Number)` 唯一索引（見 [database-design.md](design/database-design.md)），set-based UPDATE 不會撞唯一約束、不需「由大到小逐列移」。**但**若未來替該組合加了 unique index，這句會在更新中途撞唯一鍵——屆時要改成由大到小逐列 UPDATE 或 `SET Number = -Number` 兩階段。並發安全靠同交易的 `sp_getapplock`（resource `signup-number:{year}:{cat}:{type}`，**與預繳載入共用命名空間**）+ `UPDLOCK/HOLDLOCK`。插入模式**刻意不做編號重複檢查**（插入位置本就佔用，那正是要順移的對象）。
- **預繳載入的並行安全靠 UPDLOCK/HOLDLOCK + `sp_getapplock`，不是靠 idempotency**：idempotency（比對已存在 BelieverID）只擋「同信眾重複建立」，擋不了「兩個並發載入配到同一個 Number」。真正防重號的是把「讀 `MAX(Number) WITH (UPDLOCK, HOLDLOCK)` → 配號 → insert」收在**單一 transaction**（範圍鎖持有到 commit，連一般報名的並發插入也擋），外加 `sp_getapplock` 序列化同組載入。若把讀 MAX 移出交易或拿掉鎖 hint，並發下就會重號——[PrepayRepository.InsertPrepayBatchAsync](../backend/src/Ceremony.Infrastructure/Repositories/PrepayRepository.cs) 早期版本曾犯此錯（讀 MAX 在交易外、與 insert 分離），已修正。

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
