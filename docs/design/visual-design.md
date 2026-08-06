---
title: Visual Design
purpose: 視覺設計與介面編排規範，**對齊原 WinForms 版型**；定義版型、字型、色彩、按鈕、表單樣式
applicable_when: 要設計新頁面、要決定元件樣式、要對齊舊版面、要驗收 UI 編排
related_agents:
  - visual-design-architect
  - frontend-architect
related_docs:
  - frontend-design.md
  - ../blueprints/auth-and-admin.md
  - ../blueprints/believer-management.md
  - ../blueprints/signup-management.md
keywords: [visual, ui, design, layout, 版型, 樣式, 編排, WinForms, 一致性, Claude配色, 暖米色, 珊瑚橘, 對比度, a11y, WCAG]
last_updated: 2026-08-06 (DataGrid 配色規範新增「截斷 tooltip」段（沿用選單同一套 --c-surface + --c-border + 陰影，max-width 420px 可折行，不做深底反白；截斷的 `…` 需靠 `.vgrid-td-text` 才生效）。先前 2026-08-02 (「列印對話框」段改寫為「列印預覽視窗」：自建對話框整組移除，改為 Chromium PDF 檢視器視窗 + Windows 原生列印對話框〔外觀不歸我們管，也不該再加控制項——webContents.print 會把工具列一起印進去〕。見 blueprints/print-channel-electron.md。先前 2026-07-31 (新增「列印對話框」元件規格段：內建 PDF 預覽的兩欄版面〔minmax(0,1fr) 300px、iframe #toolbar=0、紙張永遠唯讀〕、preview-only 模式隱藏印表機欄位、無預覽時的 .no-preview 窄版退化與必須說明原因。同日先前左側選單字級改 20px：新增「側欄選單字級」段〔nav-label 硬寫 20px 不開 token、圖示 size 20→24、`.nav-item` 圖示欄寬 24→28px〕，並把報名維護 vgrid 與 dialog 內文兩處「對齊 nav-label 字級」敘述改為記錄歷史來由、明註 2026-07-31 起脫鉤不跟進。同日先前搜尋面板「編號」改為起 ~ 迄兩格 `.num-stepper`（中間 `～`），與批次列印面板同形態。同日先前報名維護「切到其他功能再回來被重置」客訴：ShowAll 段改寫為「不落磁碟、但活在當次執行期間」（存 `SignupSearchState.showAll` 記憶體 singleton），並補「搜尋條件任何變動即寫回快照、不必先按搜尋」。同日先前報名維護 toolbar RWD 改寫：斷點由 `@media` 視窗寬改為 `@container` 量 toolbar 容器寬，分 >1100 / ≤1100 / ≤700 三層（≤700 一層依使用者指定：批次列印與動作**維持同一列**，靠壓縮起迄欄換空間）——修掉窄寬時批次列印與動作區各自 100% 寬、列印/新增報名鈕被拉成滿版的客訴，同時修掉預設 1280px 視窗下三 panel 排不下、搜尋鈕壓到「備註」的既有問題。同日先前報名表單版面客訴第四輪：NewSignupForm 段版面圖改繪〔**部分反轉 07-29 的「全部無外框」**〕——地址拆「寄件地址」「文牒地址」兩個 fieldset 且框內欄位標題全拿掉改 placeholder、「同寄件地址」移進文牒框內靠右、往生/陽上名單各自恢復外框與 legend〔解掉「填字後失去標示」取捨〕、基本資料改序 堂號→姓名→電話→員工類型→固定編號 且前三欄與地址框逐像素等寬；目前無外框的只剩「基本資料」與「編號·費用·備註·預繳」兩區。先前 2026-07-29 (報名維護清單客訴三項：DataGrid 段新增「欄位預設寬度」抓法〔n 字 ≈ 17n+13px〕並把「法會」由 100px 改 64px＝三個字；「ShowAll 持久化」改為**不持久化**〔每次開頁不勾，欄寬持久化不動〕；搜尋 pane「全部」勾選時的淡化/停用範圍由「整個條件區」收斂為「年份／法會／類型＋範圍」四者。同日先前報名表單版面客訴第三輪：Button 表新增 `.btn-wide`〔min-width 112px＝兩字鈕自然寬 ×2，只給送出/結果確認鈕〕；MessageBox 段新增「結果型提示可再放大」〔ConfirmDialog `emphasis`：訊息 20px + 確認鈕加寬，僅「新增報名成功」〕；NewSignupForm 段補新版版面圖〔基本資料改全寬單列、按鈕列靠左、右側五個區塊全部拿掉 fieldset 外框與 legend——只剩「法會資料」保留外框〕。同日先前新增「互動元素 +1px 級距」段：另開 --font-size-*-plus 平行 token，只給輸入框/按鈕/清單列用，標題維持基礎級距不加大〔使用者指定「標題不用」〕；同步更新 DataGrid 段與 vgrid 規格的字級標註。先前 2026-07-28：新增「進度 Overlay」元件規格〔批次列印用：置中卡片、大百分比、8px 進度條、i/N 計數、取消鈕、a11y、不可 backdrop 關閉的理由〕，並附 Overlay z-index 層級表 form-overlay 900 / confirm 1000 / progress 1100；同日先前 Form Overlay 互動加「只能用 × 關」例外〔dismissible=false，報名維護新增/編輯 overlay 客訴〕；同日先前confirm/alert dialog 內文字級放大到 --font-size-md＝側欄選單同級（客訴「報名成功提示字太小」，改共用 .confirm-body）；同日 form-overlay 底部 actions footer 可用 showActions=false 關掉〔報名表單按鈕列改放備註下方〕；先前 2026-07-27：報名維護搜尋 pane 的 col 1 checkbox 欄改為 全部／範圍／顯示完整表格 三層（「全部」插在「範圍」上方，其餘各下移一列、維持三列不加高）＋「全部」勾選時條件區 .all-mode 淡化 opacity .5；同日先前 Form Overlay 補「寬度」規則：內容含寬表格要用 [width] 給 panel 定寬（限制內層表單無效、會讓 actions 落單）；同日新增「表單控件 disabled 樣式」段：.field 無條件設 background/color 會蓋掉瀏覽器預設 disabled 外觀，全域補 :disabled 灰底＋not-allowed，文字用 --c-text-secondary 保持可讀；同日 Form Overlay 互動加例外：有跨路由草稿保護的表單〔新增報名純新增模式〕關閉時不跳「未儲存的變更」確認；2026-07-21：報名維護 UI 客訴四項：搜尋/列印按鈕補 align-self:stretch 真正撐滿列高（客訴按鈕太矮，根因為 grid align-items:center）、編號＋批次起迄加 .num-stepper ▲▼ ±1、編輯表單預繳民國年移到下一行置於預繳法會前；2026-07-18：--c-dead-name-bg 輸入框例外擴及 believer-edit-form（客訴）；2026-07-17：「清單/資料格配色規範」定案為全站唯一權威（DataGrid 段改寫，廢棄舊斑馬紋敘述）；.data-table.dense 補直向格線對齊 vgrid；報名維護 list 字級改 --font-size-md 對齊左側欄；--c-dead-name-bg 改用 --c-primary-soft；--c-row-selected 改深為 #E9C79C 拉開層次))))
---

## 設計原則

1. **介面編排對齊原 WinForms**：欄位順序、Tab 鍵順序、按鈕位置、區塊分割必須與舊系統一致（依 Designer.cs 量測值）
2. **不破壞使用者肌肉記憶**：按鈕文字、錯誤訊息、操作流程 verbatim 保留
3. **桌面感受優於 web 感受**：固定大小視窗、密集資訊、鍵盤導航優先
4. **可訪問性 (a11y) 加分**：對比 ≥ AA、ARIA label 完整、可純鍵盤完成所有操作

## 字型

| 用途 | 字型（優先序） |
|---|---|
| UI 通用 | `'Microsoft JhengHei', '微軟正黑體', 'Noto Sans TC', sans-serif` |
| 報表 / 列印 | `'BiauKai', '標楷體', 'DFKai-SB', serif` |
| 等寬（log/code） | `'Cascadia Code', 'Consolas', monospace` |

字級（CSS 變數，**2026-07-04 決策：在 2026-07-02 +1px 基礎上再 +1px**）：

```css
--font-size-xs: 13px;    /* 小標注 (例：(不須填符號，例：0987654321)) */
--font-size-sm: 14px;    /* 表單欄位 */
--font-size-base: 15px;  /* 主要內容 */
--font-size-md: 16px;    /* 按鈕、強調 */
--font-size-lg: 18px;    /* 區塊標題 */
--font-size-xl: 22px;    /* 頁面標題 */
```

對應舊 WinForms 9pt~12pt（96 DPI 下 9pt ≈ 12px）；**再 +1px 理由**：2026-07-02 已加過 1px，使用者仍反映「文字太小、顏色不清楚」，複測發現主要瓶頸其實是對比度（見下方色彩段），但字級本身也再拉近一階觀感。單一來源：[frontend/src/styles.scss](../../../frontend/src/styles.scss)，改此處全站生效，無需逐頁調整。

### 互動元素 +1px 級距（2026-07-29 決策）

使用者要求「輸入欄位、搜尋結果 list、按鈕再大 1px，**標題不用**」。因為基礎級距是標題與內文共用的，直接調 `--font-size-*` 會連標題一起變大，故**另開一組 `*-plus` 平行 token**，只給互動元素用：

```css
--font-size-xs-plus: 14px;   /* = xs + 1：.btn-sm、清單內 meta 小字 */
--font-size-sm-plus: 15px;   /* = sm + 1：.data-table / .vgrid-header / .vgrid-row */
--font-size-base-plus: 16px; /* = base + 1：.field input/select/textarea、.dense-controls 控件、.btn */
--font-size-md-plus: 17px;   /* = md + 1：報名維護 vgrid、登入頁輸入框與主按鈕、報表 mode-tab */
```

**規則**：
- **用 `*-plus`**：所有可輸入控件（input / select / textarea）、所有按鈕、所有清單/表格的**列與欄頭**
- **維持基礎 token**：h1/h2/h3、`legend`、欄位 label（`.field > span`、`.lbl`）、副標、提示文字、badge/tag——即使它們在同一張卡片裡
- 兩組 token **必須同步**：日後調整任一基礎級距，對應的 `*-plus` 要一起 +1，不可只改一邊

清單列高不動（vgrid `ROW_HEIGHT = 26px`、信眾搜尋結果 `$result-row-h = 25px`）：17px 字仍落在 `padding 4px×2` 後的內容高度內，量測後無裁切。

**已知例外（未跟上 token）**：登入頁品牌標題（34px/28px/24px，[login-page.scss](../../../frontend/src/app/features/login/login-page.scss)）與各處純裝飾用大型 icon glyph（如 `.close-btn` 24px、空狀態插圖 48px）刻意不用字級 token，因為它們是品牌/圖示尺寸而非內容字級，見「登入頁設計」段說明。

### 側欄選單字級（2026-07-31 決策）

使用者要求「左側選單的字體改 20px」。`.nav-label` 由 `--font-size-md`（16px）改為**硬寫 20px**，圖示 `app-icon [size]` 同步 20 → 24（1.25 倍取常見級距），`.nav-item` 圖示欄寬 24px → 28px 避免 24px 圖示貼邊。檔案：[shell-layout.scss](../../../frontend/src/app/core/layout/shell-layout/shell-layout.scss)、[shell-layout.html](../../../frontend/src/app/core/layout/shell-layout/shell-layout.html)。

**為何不開 token**：20px 落在 lg(18) 與 xl(22) 之間，新增一個級距只為單一元件使用，反而讓 token 表變雜；側欄是全站唯一的固定導覽，字級由使用者直接指定，不需要跟內容字級連動。**代價**：日後全站 +1px 時側欄不會跟著動，要手動改這一處。

**連帶影響**：側欄字級原本是兩處「對齊基準」——報名維護 vgrid 與 dialog 內文都曾寫「與 nav-label 同級」。本次調整後兩者**刻意不跟進**（維持 17px / 16px），該敘述已改為記錄歷史來由，不再是同步約束。

## 色彩（Claude 配色 — 暖米/珊瑚橘）

> **2026-05-26 決策**：放棄原 WinForms 灰藍配色，全面採用 Claude 品牌配色（暖米色背景 + 珊瑚橘主色），保留 WinForms 的**版型編排**但更新視覺語言。理由：(1) 客戶指定 (2) 暖色系比冷灰更符合宗教場域氛圍 (3) 與舊系統視覺差異化，避免使用者誤以為「沒變」。

| token | 色碼 | 用途 |
|---|---|---|
| `--c-bg` | `#FAF9F5` | 視窗背景（暖米色） |
| `--c-bg-darker` | `#F0EBE0` | 側邊欄 / panel header（深一階米色） |
| `--c-surface` | `#FFFFFF` | 卡片 / panel 內容區 |
| `--c-border` | `#D9D2C2` | 主框線（暖灰）；**已知問題**：對白底對比僅 1.51:1，遠低於 WCAG 非文字元件建議的 3:1，欄位/grid 分隔線偏弱（見下方「已知待處理」） |
| `--c-border-soft` | `#E8E2D3` | 次要框線 / 分隔 |
| `--c-text-primary` | `#2C2A26` | 主文字（深暖黑） |
| `--c-text-secondary` | `#6E685C` | 次要文字（暖灰）（**2026-07-04 改深**：原 `#7A7466` 對主背景/grid 偶數列對比僅 4.41:1 / 4.38:1，未達 WCAG AA 一般文字門檻 4.5:1，且此 token 遍布全站 hint/meta/breadcrumb/legend，是「文字顏色不清楚」抱怨的主因；改深後對比 5.2-5.5:1） |
| `--c-text-disabled` | `#B3AC9C` | 禁用文字（僅用於 disabled 控制項/placeholder，WCAG 對此類元件無強制對比要求，維持不動） |
| `--c-primary` | `#CC785C` | 連結、icon、邊框、選中態等只需 3:1（UI 元件/大字）門檻的場合（Claude 珊瑚橘） |
| `--c-primary-hover` | `#B86847` | `--c-primary` 用途的 hover 加深 |
| `--c-primary-soft` | `#F5E5DC` | 「新增報名」按鈕軟珊瑚底 + 選取列 |
| `--c-primary-strong` | `#B35738` | **2026-07-04 新增**：實心按鈕背景（白字），取代 `.btn-primary`/`.login-btn` 原本的 `--c-primary`——白字疊在 `#CC785C` 上對比僅 3.28:1，未達 WCAG AA 一般文字門檻 4.5:1；`--c-primary-strong` 對白字對比 4.84:1 |
| `--c-primary-strong-hover` | `#9C4B31` | `--c-primary-strong` 按鈕 hover 加深（對白字對比 6.06:1） |
| `--c-danger` | `#C84A3A` | 刪除 / 錯誤（深紅橘） |
| `--c-warning` | `#E5A53D` | 警告（暖琥珀） |
| `--c-success` | `#6B8E5A` | 成功（暖綠） |
| `--c-dead-name-bg` | `var(--c-primary-soft)` (#F5E5DC) | 往生名欄位 highlight（**2026-07-17 使用者指定**：改跟左側選單 active 背景同色，直接引用 token 保持連動。歷程：原 `#EFDCC4` 太淺 → 2026-07-02 改深 `#E3B274` → 2026-07-17 調淺 `#E9C79C` → 同日使用者再指定用 `--c-primary-soft`。**例外（2026-07-17 使用者指定；2026-07-18 客訴擴及 believer-edit-form）**：signup-edit-form 與 believer-edit-form「往生名單」**輸入框**皆不套此底色——對齊舊系統（往生/陽上 textbox 皆無 BackColor）；列表/搜尋結果的往生「欄」底色不受影響） |
| `--c-row-selected` | `#E9C79C` | DataGrid 選取列（**2026-07-17 改深**：往生欄改用 `--c-primary-soft` 後與原選取色同值，使用者指定選取列改深一階橘拉開層次） |
| `--c-row-alt` | `#FAF8F2` | DataGrid 偶數列 |

> 「新增報名」按鈕舊系統用 light blue 強調，新版改用 `--c-primary-soft`（軟珊瑚）保持視覺重點，符合 Claude 配色。

> **元件選色規則（2026-07-04 明確化）**：`--c-primary` 只用在「非實心填色」場合（連結、icon、邊框、tab 底線等），這些屬於 WCAG 的 UI 元件/大字級，3:1 門檻即可；任何「實心底色 + 白字」的按鈕一律改用 `--c-primary-strong`，因為一般文字門檻是 4.5:1，`--c-primary` 本身對白字只有 3.28:1 不夠。

### 已知待處理（未在本次修復範圍）

- **`--c-border` 對比僅 1.51:1**：要達到 WCAG 非文字元件建議的 3:1，需把色值加深到 `#A4936D` 等級，但這會讓全站欄位框線/grid 分隔線的視覺重量明顯變重，屬於較大的視覺性格改動，本次（2026-07-04）先不動，留待下次視覺設計討論再決定是否要做、要做到多深。
- **DPI 縮放 100/125/150% 切換**：本節「響應式（DPI / 縮放）」長期寫著要提供這個功能，但目前程式碼完全沒有實作。使用者若期待這個選項卻找不到，也可能造成「文字太小」的感受。是否要做（需要全站改用 rem + 根字級切換機制）待後續排入 backlog 再評估。

## 版面尺寸與間距

| token | 值 | 對應舊系統 |
|---|---|---|
| `--space-xs` | 4px | – |
| `--space-sm` | 8px | – |
| `--space-md` | 12px | Designer.cs 慣用 panel padding |
| `--space-lg` | 16px | – |
| `--space-xl` | 24px | – |
| `--control-height` | 28px | ComboBox / TextBox 高度 ~28-29 |
| `--button-height` | 40px | 主要按鈕（btnConfirm 40） |
| `--button-large-height` | 99px | btnSearch (BelieverForm) / btnNextStep 等大按鈕 |

## 視窗尺寸（與舊系統對齊）

| Form | 舊尺寸 | 新版策略 |
|---|---|---|
| LoginForm | 284 × 274 | 桌面 modal，固定 360×320（高 DPI 微調） |
| MainForm | 235 × 344 | 桌面 docked sidebar（非獨立視窗），仍保留 6 按鈕版面感 |
| AdminsForm | 664 × 511 | 兩欄式（左 grid + 右編輯區）固定比例 |
| BelieverForm | 1064 × 796（min 1080×835） | 全螢幕，三區（搜尋 / grid / 編輯） |
| SignupForm | 980 × 961 | 全螢幕，四面板（filter / 列印 / 操作 / grid） |
| NewSignupForm | 848 × 643 | modal 全螢幕（兩步驟 wizard） |
| EditSignupForm | 673 × 493 | modal |
| LoadPrepayForm | 337 × 259 | 固定小窗 modal |
| CeremonyCategoryForm | – | 兩欄式（TreeView + 編輯區） |

> 新版 Electron 主視窗預設 1280×800，內部頁面用 CSS Grid / Flex 動態佈局，但**控件相對位置與比例**對齊舊 Designer.cs。

## 元件規格

### Button

| 變體 | 用途 | 樣式重點 |
|---|---|---|
| `primary` | 確認 / 儲存 / 搜尋 | 背景 `--c-primary`、白字、`--button-height` |
| `primary-soft` | MainForm「新增報名」強調 | 背景 `--c-primary-soft`、深字 |
| `secondary` | 取消 / 上一步 / 清除 | 白底框線、深字 |
| `danger` | 刪除 | 文字紅 `--c-danger`，hover 紅底白字 |
| `large` | btnSearch / btnNextStep | 高 99px、寬 ≥ 110 |
| `.btn-wide`（**2026-07-29**） | 要好按的主要動作鈕 | `min-width: 112px`；疊加在 `.btn` 上，不改高度/配色 |

**`.btn-wide` 的來源與判準（2026-07-29 使用者指定）**：報名表單的「確認」與「新增報名成功」提示的「確定」都要**寬度加倍**。兩字鈕的自然寬 = 12(padding) + 2×16(字) + 12 = 56px，故 `min-width` 取 112px。用 `min-width` 而非固定 `width`，字多的按鈕仍會自然撐開不截字。只給「一次操作的終點鈕」用（送出、結果確認），一般次要鈕維持預設寬度——全部加寬就等於沒有強調。

### TextBox / ComboBox / DateInput

- 高度 `--control-height`
- 框線 1px `--c-border`，focus 時 `--c-primary` + 1px ring
- 錯誤態：紅色框線 + 下方 11px 紅字訊息

### DataGrid — 清單/資料格配色規範（**2026-07-17 定案，全站唯一權威**）

> 使用者指定：**所有清單（vgrid 與 table）配色必須一致，之後新增任何清單都參照本表**。
> 起因：新增報名「信眾搜尋結果表」與報名維護 vgrid 曾各自留有差異（結果表缺直向格線、
> 表頭底線用錯 token、選取色曾用 `--c-primary-soft`）——已全部統一如下。

| 元素 | Token / 值 | 備註 |
|---|---|---|
| 表頭背景 | `--c-bg-darker` | 文字 `--c-text-primary`、font-weight 600 |
| 表頭底線＋表頭直線 | `--c-border` | 直向格線每欄都有 |
| 資料列橫線＋直線 | `--c-border-soft` | 直向格線每格都有（**清單看起來有格線是規範的一部分**） |
| 往生欄背景 | `--c-dead-name-bg` | 其**右框線**改 `--c-text-disabled`（比一般格線深，分隔往生區） |
| 選取列 | `--c-row-selected` | |
| 選取列 × 往生欄 | `color-mix(in srgb, var(--c-row-selected) 60%, var(--c-dead-name-bg))` | |
| 列 hover | **無 hover 變色**（2026-07-17 使用者指定） | 所有清單一律不做 row hover（vgrid 本來就無）；可點選列以 `cursor: pointer` 表示。`.data-table.dense` 已顯式蓋掉基底 `.data-table` 的 row-alt hover |
| 列高 | vgrid 26px（padding 4px 6px）；緊湊 table 25px（padding 2px 6px） | 不做奇偶列斑馬紋（舊 mockup 的 `#FAFAFA` 條紋已廢棄） |
| 隱藏欄位 | CSS `display: none`，由 column-toggle 控制 | |

**截斷 tooltip（2026-08-06 新增，對齊舊 `DataGridView.ShowCellToolTips`）**：文字被欄寬截斷的儲存格（與表頭欄名）hover 500ms 顯示完整內容，沒截斷不顯示。外觀沿用選單同一套：`--c-surface` 底 + `--c-border` 框 + `0 4px 14px rgba(44,42,38,.16)` 陰影、圓角 3px、padding 3px 6px、`--font-size-sm-plus`；max-width 420px 且可折行（備註/地址常常很長）。**不做深底反白配色**——全站沒有深色浮層，tooltip 也不開這個先例。截斷本身以 `…`（`text-overflow: ellipsis`）表示，這需要儲存格文字包在 `.vgrid-td-text` 內才會生效（見 [frontend-design.md DataGrid 規格](frontend-design.md#datagrid-規格)）。

**實作載體（兩套共用同一組 token，改 token 兩邊自動連動）**：
- 虛擬捲動 vgrid：`styles.scss` `.vgrid-header-clip / .vgrid-th / .vgrid-row / .vgrid-td`（報名維護、信眾維護）
- 一般 table：`styles.scss` `.data-table.dense`（新增報名信眾搜尋結果）——2026-07-17 已補直向格線/表頭底線/往生欄右框線與 vgrid 一一對應
- 字級例外：報名維護頁 vgrid 為 `--font-size-md-plus`（2026-07-17 使用者指定對齊左側欄，page-scoped；2026-07-29 隨全站互動元素 +1px 進位）；其餘清單為 `--font-size-sm-plus`

### Tree（CeremonyCategoryForm）

- 根節點：「法會維護」
- Level 1：展開圖示 chevron
- Level 2：不可再展開
- 右鍵 context menu（依層級顯示不同項目）

### Icon（**2026-05-28 決策**）

- **統一用 inline SVG**，不混用 emoji / Unicode 字元符號（避免不同系統字型造成大小不一）
- 共用元件：[`shared/icon/icon.component.ts`](../../frontend/src/app/shared/icon/icon.component.ts) — `<app-icon [name]="..." [size]="20" />`
- 規格：24×24 viewBox / stroke-based / `stroke-width: 1.75` / `currentColor`（跟隨父層文字色）
- active / hover 時 icon 隨文字一起轉主色，**不再為 icon 單獨刻顏色規則**
- 加新 icon：在 [icon.component.ts](../../frontend/src/app/shared/icon/icon.component.ts) `ICONS` map 補 SVG path，並擴 `IconName` union type
- 已收錄：`believer / plus / search / download / category / printer / settings / home`

### UI 文字 vs 程式識別（**2026-05-28 決策**）

- **介面只顯示中文 label**，**不顯示舊 WinForms 的英文 form 名稱**（`BelieverForm` / `SignupForm` 等）
- 程式內仍保留 form 對照（commit message、blueprint、debug log）；只是不曝露在使用者畫面
- **Why**：英文 form name 對使用者是雜訊；維護期讓開發者透過 doc / code 對應即可
- **How to apply**：sidebar nav、dashboard 入口磚、breadcrumb 等所有對使用者的元件都遵守

### Dialog（CustomDialogForm 等價）

- 標題列：背景 `--c-bg`、置左
- 內容區：白底
- 底部按鈕區：右對齊，主按鈕在最右

### Snackbar / MessageBox（CustomMessageForm 等價）

- 短期提示：bottom-center snackbar，3 秒自動消失
- 阻斷型：modal dialog（OK / Yes-No）
- **文字 verbatim**：「新增信眾成功！」「刪除成功！」「請輸入姓名」等
- **結果型提示可再放大（2026-07-29）**：`ConfirmDialogConfig.emphasis = true` → 訊息 **20px** + 確認鈕套 `.btn-wide`。目前唯一使用者是「編號X，新增報名成功」（編號要一眼看到）。一般二選一確認框維持 `--font-size-md`（2026-07-28 已全站放大過一次），**不要**把 20px 直接寫進 `.confirm-body`

### 列印預覽視窗（**2026-08-02：自建列印對話框已移除**）

新系統不再有自建的列印對話框。按「列印」之後是**可見的 Chromium PDF 檢視器視窗**
（`BrowserWindow({ plugins:true })`，標題「列印預覽 — 請按工具列的列印鈕」），
使用者按檢視器工具列的 🖨 就會落到 **Windows 原生列印對話框**——印表機、份數、紙張、方向、
頁面範圍全在那裡選。這是舊系統 `PrintPreviewDialog → PrintDialog` 的等價物。

```
┌───────────────────────────────────────────────┐
│ 列印預覽 — 請按工具列的列印鈕                    │
├───────────────────────────────────────────────┤
│  ⌄ 1 / 128    ─ ＋      ⤓  🖨  ⋮               │ ← Chromium PDF 工具列（原生，非我們畫的）
│ ┌───────────────────────────────────────────┐ │
│ │                                           │ │
│ │              資料卡 PDF                    │ │
│ │                                           │ │
│ └───────────────────────────────────────────┘ │
└───────────────────────────────────────────────┘
```

**設計含意**：
- 這個視窗的外觀**不歸我們管**，也不該再往上加控制項——`webContents.print` 會把工具列一起印進去
- 批次列印期間仍會顯示我們自己的 `progress-overlay`（那是**渲染**進度，不是列印進度）
- 報表預覽頁（`/reports/preview`）的 iframe 預覽維持不變，它是另一個東西（頁內預覽）

理由與完整契約見 [print-channel-electron.md](../blueprints/print-channel-electron.md)。

### 進度 Overlay（**2026-07-28 新增**，批次列印用）

阻擋畫面的置中進度卡片，顯示真實的「第 i / 共 N 筆」與百分比，並可取消。
元件：[shared/progress-overlay/](../../frontend/src/app/shared/progress-overlay/)，
行為與 API 見 [frontend-design.md](frontend-design.md)。

**Overlay z-index 層級表（全站唯一權威）**

| 層 | z-index | 用途 | 為什麼在這一層 |
|---|---|---|---|
| `.overlay-backdrop`（form-overlay） | 900 | 新增/編輯彈窗 | 底層工作面板 |
| `.confirm-backdrop`（confirm-dialog） | 1000 | 確認 / 警示對話框 | 要能蓋住編輯彈窗（未儲存確認） |
| `.progress-backdrop`（progress-overlay） | **1100** | 進行中的長時間工作 | 進行中的工作不該被任何東西蓋住 |

**版面**（由上而下，卡片 `width: min(420px, 92vw)`、`padding: var(--space-xl) var(--space-lg)`、置中）

| 元素 | 樣式 |
|---|---|
| backdrop | `rgba(44, 42, 38, 0.42)`，`fadeIn 120ms` |
| 卡片 | `--c-surface` + 1px `--c-border` + radius 6px + `0 12px 40px rgba(44,42,38,.22)`，`pop 140ms` |
| 標題 | `--font-size-lg`，600 |
| 副標（報表名稱） | `--font-size-sm`，`--c-text-secondary` |
| **百分比數字** | `--font-size-xl`，600，`--c-primary-strong`，`tabular-nums` |
| 進度條 | 高 8px、radius 4px；track `--c-border-soft`、fill `--c-primary-strong`；`transition: width 200ms ease-out` |
| 計數「12 / 28 筆」 | `--font-size-sm`，`--c-text-secondary`，`tabular-nums` |
| 狀態字（合併 PDF…／下載中…／取消中…） | `--font-size-sm`，`--c-text-secondary` |
| 取消鈕 | 全域 `.btn`（非 primary）；下載階段隱藏 |

**行為與 a11y**

- **不可點 backdrop 關閉**：這裡擋的是長時間工作，誤觸中斷的代價比關不掉高。只能按「取消」或 Esc。
- 進度條加 `transition` 是為了補平 250ms 輪詢的跳格感（見 [performance.md](performance.md)）。
- 卡片 `role="dialog" aria-modal="true"`；進度條 `role="progressbar"` + `aria-valuenow/min/max`；
  計數列 `aria-live="polite"`。
- 數字欄位一律 `font-variant-numeric: tabular-nums`，避免位數變化時左右跳動。

## 表單區塊（與舊 WinForms 對齊）

### 表單控件 disabled 樣式（**2026-07-27**）

`.field` 內的 `input / select / textarea` 因為無條件設了 `background: var(--c-surface)` + `color: var(--c-text-primary)`，
會把**瀏覽器預設的 disabled 外觀整個蓋掉**——結果 disabled 欄位看起來與可輸入欄位一模一樣（報名表單「編號」欄客訴由此而來）。
全域補：

```scss
.field input:disabled, .field select:disabled, .field textarea:disabled {
  background: var(--c-bg-darker);      // #F0EBE0，一眼看得出「這格現在不能改」
  color: var(--c-text-secondary);      // 不用 --c-text-disabled：欄位可能已有值，要保持可讀
  cursor: not-allowed;
}
```

文字刻意**不用** `--c-text-disabled`（#B3AC9C）——那與 `--c-bg-darker` 對比僅約 1.6:1，欄位已有值時會看不清楚；
「不能改」由底色 + `not-allowed` 游標表達即可。


### Nav 中文標籤對照（**2026-05-28 命名決策**）

| 路由 | UI label | 對應舊 Form | 備註 |
|---|---|---|---|
| `/believers` | 信眾維護 | BelieverForm | – |
| `/signups/new` | 新增報名 | NewSignupForm | – |
| `/signups` | **報名維護** | SignupForm | 原為「報名查詢」，**改為「報名維護」** — 列表也含編輯 / 刪除 / 匯出，不只查詢 |
| `/prepay` | 載入預繳 | LoadPrepayForm | – |
| `/backup` | 資料備份 | MainForm（btnBackup） | icon `database`；nav 順序在「載入預繳」與「法會類型」之間（對齊舊 MainForm 按鈕順序） |
| `/categories` | 法會類型 | CeremonyCategoryForm | – |
| `/reports/preview` | 列印預覽 | (新增) | – |
| `/admins` | 管理者 | AdminsForm | – |

### 信眾維護頁面（`/believers`，**2026-05-29 對齊舊 dgvBelievers 全欄位 + 右鍵選單**）

舊 BelieverForm 是 split-view（左 DataGrid + 右編輯區）；新版為 **single-column + 全欄位虛擬捲動 grid**，編輯走 form-overlay（見「Form Overlay」段）：

```
┌───────────────────────────────────────────┐
│ 頁標題                       [+ 新增信眾] │
├───────────────────────────────────────────┤
│ [姓名][電話][堂號][陽上][往生][清除][搜尋]│  ← flex wrap
├───────────────────────────────────────────┤
│ 共 N 筆                                    │
│ ┌ vgrid header（sticky，22 欄）─────────┐ │  ← 橫向捲動，header 與 body 同步
│ │ cdk-virtual-scroll-viewport（v+h scroll）│ │  ← flex:1 填滿至距底 12px
│ └────────────────────────────────────────┘ │
└───────────────────────────────────────────┘

右鍵任一列 或 點列尾 ⋮ → context menu「編輯 / 刪除」
```

- **欄位 = 舊 `dgvBelievers` 可見欄位 1:1**（header / width / 順序皆抽自 [BelieverForm.Designer.cs](../../reference/old/Ceremony/BelieverForm.Designer.cs)，定義集中於 [believer-columns.ts](../../frontend/src/app/features/believers/believer-columns.ts)）：
  員工 / 堂號 / 姓名 / 聯絡電話 / 寄件城市·區域·地址 / 文牒城市·區域·地址 / 往生1·2·3·3-1·5·6 / 陽上1·2·3·3-1·5·6 + 列尾 ⋮ 操作欄
- 往生欄底色 `--c-dead-name-bg`（沿用全域 `.vgrid-td.dead`）
- **填滿視窗、距底 12px**：`:host{height:100%}` → `.page` flex column → `.results-card` flex:1 → `.vgrid-zone`/viewport flex:1（shell `.content` padding-bottom 12px）
- 不做欄寬持久化 / 多選（信眾維護無批次需求）；如需參考完整 vgrid + 欄寬持久化見報名維護頁面
- **不再用** `.hide-sm/md/lg` RWD 隱欄與 side-sheet（2026-05-28 舊設計已被本次取代）

### BelieverForm（[believer-management blueprint](../blueprints/believer-management.md)）

```
┌─ Search panel ─────────────────────┐ ┌─ Edit panel (335 寬) ─┐
│ 姓名 [    ] 陽上 [    ] 搜尋 [   ]   │ │ 堂號 [  ] 姓名 [  ]    │
│ 電話 [    ] 往生 [    ]             │ │ 員工 [▼] 預繳固定編號  │
│ 堂號 [    ]                         │ │ ─── 寄件地址 ───      │
└──────────────────────────────────────┘ │ 縣市[▼] 區域[▼]       │
┌─ DataGrid (587 寬) ────────────────┐ │ 詳細地址 [ _________ ] │
│ 員工 堂號 姓名 ... 往生1..6 陽上1..6  │ │ ─── 文牒地址 ───      │
│                                    │ │ □ 同寄件地址          │
│                                    │ │ ...                   │
└──────────────────────────────────────┘ │ ─── 往生 × 6 ───       │
                                          │ ─── 陽上 × 6 ───       │
                                          │ [取消] [確認]          │
                                          └────────────────────────┘
```

### 報名維護頁面（`/signups`，**2026-05-28.c 緊湊版：對齊舊 SignupForm 三 panel**）

舊 SignupForm 上方為 **3 panel 並排** 在同一橫條（高 127px）：搜尋 (615px) / 批次列印 (203px) / 動作 (126px)。新版照樣**並排**而非 stack；單列高約 110px：

```
┌──────────────────────────────────────────────────────────────────────────┐
│ 報名維護                                                                  │
├──────────────────────────────────────────────────────────────────────────┤
│ ┌─ 搜尋 (flex 1) ─────────────────────────────────────┐┌─ 列印 ─┐┌動作─┐ │
│ │ ☑全部       年份[___]   ☑姓名 ☑陽上 ☑往生 ☑電話  ┃ ││[起]~[迄]││+新增 │ │
│ │ ☑範圍   法會[ ▼ ]  關鍵字[__________]  ☑固定編號  ┃搜││[類型▼]  ││✎修改 │ │
│ │ ☑顯完整   類型[ ▼ ]  編號[__]   [匯出 Excel]      ┃尋││  [列印] │└─────┘ │
│ └────────────────────────────────────────────────────┘└────────┘         │
├──────────────────────────────────────────────────────────────────────────┤
│ 結果 N 筆          已選 K 筆 [取消選取] [對選取項目 ⋮]                     │
├──────────────────────────────────────────────────────────────────────────┤
│ ┌─ DataGrid (27 default / 32 with ☑顯完整, 41 cols total)──────────────┐ │
│ │ ☐ 年份 法會 類型 編號 [費用 員工] 姓名 備註 [堂號] 往1 往2 往3 往3-1   │ │
│ │   往5 [往6] 陽1 陽2 陽3 陽3-1 陽5 [陽6] 預繳年份 預繳法會 聯絡電話     │ │
│ │   寄件城市 寄件區域 寄件地址 文牒城市 文牒區域 文牒地址 編輯者 編輯日期│ │
│ │   [⋮]                                                                  │ │
│ └────────────────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────────┘
```

#### 搜尋 pane 內部 grid（對齊 plSearch 615×127 三列五欄）

```
Row 1: ☑全部              [年份][__]      ☑姓名 ☑陽上 ☑往生 ☑電話   ┐
Row 2: ☑範圍           [法會][ ▼ ]      [關鍵字___________]  ☑固定編號 │ [搜尋]
Row 3: ☑顯完整         [類型][ ▼ ]      [編號][__]   [匯出 Excel]      ┘
```

- **col 1 = checkbox 欄，由上而下 `全部` / `範圍` / `顯示完整表格`**（2026-07-27）。「全部」依需求插在「範圍」上方，其餘兩個各往下一列；沿用既有三列 grid，**面板高度不變**（不新增第 4 列，避免壓縮下方 grid 可視列數）。代價：`範圍` 不再與 `年份` 同列相鄰（它控制的是 `Year >=` 或 `Year =`），改由 checkbox 欄的語意分組承接
- **「全部」勾選時**（2026-07-29 收斂）：只有 `年份` / `法會` / `類型`（＋年份的修飾條件 `範圍`）disabled（原生變灰）＋ `.all-mode` 讓這四者的 label `opacity: .5`；關鍵字、範圍 5 項、編號、固定編號、`全部`、`顯示完整表格` 皆維持可用不淡化——全部＝解除三個範圍限制後**繼續搜尋**，不是凍結整個面板

- 高度 110px，row gap 4px，column gap 8px
- input/select 高度 28px（`--control-height`），font 12px
- 「搜尋」按鈕 col-span row 1–3（縱向高鈕，對齊舊 btnSearch 75×99）。**2026-07-21 修正**：`.search-grid` 為 `align-items: center`，會讓按鈕只以自然高置中而非撐滿三列（客訴「按鈕太矮」根因），故 `.search-btn` 單獨補 `align-self: stretch` 才真正縱向填滿；`.print-btn` 同理
- 「匯出 Excel」按 inline 嵌在 row 3（對齊舊 btnExportExcel 在 plSearch 內，**不**獨立成按鈕）
- 「編號」欄用 `.num-stepper`（**2026-07-21**）：右側 ▲▼ 做 ±1，對齊舊 `nudSearchNumber` NumericUpDown。**2026-07-31 起為起 ~ 迄兩格**（中間 `～`，與右側「批次列印」面板同一種輸入形態）

#### 列印 pane 內部 grid（對齊 plPrint 203×127）

```
Row 1: [起__] ～ [迄__]              ┐
Row 2: [報表類型 ▼]                  │ [列印]
                                     ┘
```

- 「列印」按鈕 col-span row 1–2（縱向高鈕，對齊舊 btnPrint 75×63；**2026-07-21** 補 `align-self: stretch` 才真正填滿兩列，同搜尋鈕）
- 起 / 迄兩欄用 `.num-stepper`（**2026-07-21**）：各自右側 ▲▼ 做 ±1；grid 欄寬因加入按鈕由 70px 放寬為 86px

#### 動作 pane 內部 stack（對齊 plControl 126×127）

```
[+ 新增報名]
[✎ 修改報名]   ← 僅單選啟用（對應舊 btnEdit；右鍵 menu 仍是主入口）
```

#### DataGrid 欄位對齊舊 SignupForm.Designer.cs

**永遠隱藏（10 internal）**：SignupID, PrepayCeremonyCategoryID, BelieverID, CeremonyCategoryID, CeremonySort, SignupType, MailZipcode, TextZipcode, IsFixedNumber + CeremonyTitle.Id

**預設顯示（27 欄）**：年份 / 法會 / 類型 / 編號 / 姓名 / 備註 / 往生1, 2, 3, 3-1, 5 / 陽上1, 2, 3, 3-1, 5 / 預繳年份 / 預繳法會 / 聯絡電話 / 寄件城市, 區域, 地址 / 文牒城市, 區域, 地址 / 編輯者 / 編輯日期

**☑顯示完整表格後加 5 欄（→ 32 欄）**：費用 / 員工 / 堂號 / 往生6 / 陽上6
（對齊 [SignupForm.cs:782-797](../../reference/old/Ceremony/SignupForm.cs#L782-L797) `cbShowAll_CheckedChanged` 控制 ColFee / ColEmployee / ColHallName / ColLivingNameSix / ColDeadNameSix）

**列順序**：年份 → 法會 → 類型 → 編號 → [費用 → 員工] → 姓名 → 備註 → [堂號] → 往生 6 欄 → 陽上 6 欄 → 預繳 → 電話 → 寄件 3 → 文牒 3 → 編輯者 → 編輯日期 → 列尾 ⋮

**樣式**：
- font-size `--font-size-md-plus`（**僅報名維護 list**：2026-07-17 使用者要求對齊當時的左側欄 nav-label 字級，在 `signup-list-page.scss` 覆蓋全域 `.vgrid-row`/`.vgrid-header` 的 `--font-size-sm-plus`；其他 vgrid 頁（如信眾維護）為 sm-plus。2026-07-29 兩者同步 +1px。**2026-07-31 起與側欄脫鉤**：nav-label 改 20px，本頁維持 md-plus 不跟進）
- 列高 ≈ 26px (`padding: 4px 6px`)
- 往生欄背景 `--c-dead-name-bg`（= `--c-primary-soft`，2026-07-17 使用者指定跟左側選單 active 同色，變更歷程與 trade-off 見色彩 token 表）
- 選取列覆蓋層 `--c-row-selected` (#E9C79C)；選取列且往生欄走 mix
- 表頭 sticky top（捲動時欄名不動）
- 寬度 `width: max-content; min-width: 100%`（內容驅動寬度，溢位走 horizontal scroll）
- **欄位預設寬度**定義在 [signup-columns.ts](../../frontend/src/app/features/signups/signup-columns.ts) `SIGNUP_COLUMNS[].width`。抓法：CJK 字寬 ≈ 字級（17px）→ n 字 ≈ 17n + 12(padding) + 1(框線)。`法會` 2026-07-29 由 100px 改 **64px＝三個字**（使用者指定：法會名稱前三字已足以辨識，100px 白佔橫向空間；不夠看可拖寬，欄寬會記住）
- **自繪垂直捲軸（2026-07-21）**：右緣 14px `.vscroll` 軌道 + `.vscroll-thumb`（`--c-text-disabled`，hover 轉 `--c-text-secondary`），隱藏原生垂直捲軸、水平捲軸保留（自訂上色）。緣由是要支援「捲軸右鍵子選單」——原生捲軸右鍵攔不到，見 [frontend-design.md](frontend-design.md)、[gotchas.md](../gotchas.md)

**RWD（2026-07-31 改寫）**：斷點量的是 **toolbar 容器寬**（`container-type: inline-size` + `@container`），不是視窗寬。理由：可用寬 = 視窗 − 側欄（220px／收合 64px）− `.content` padding，用 `@media` 會量錯——Electron 預設 1280px 視窗其實只有 1028px 可用，而側欄一收合多出 156px 媒體查詢卻完全無感。

| 容器寬 | 版面 |
|---|---|
| > 1100px | 3 panel 並排（搜尋吃剩餘寬、批次列印／動作維持自然寬） |
| ≤ 1100px | 搜尋獨佔第一列；批次列印＋動作並排填滿第二列（批次列印吃剩餘寬、卡片右緣與結果表格切齊，`justify-content: start` 讓內部控制項靠左、列印鈕**不**跟著撐大） |
| ≤ 700px | **批次列印＋動作仍維持同一列**（使用者指定，2026-07-31）——靠把起迄欄改 `minmax(52px, 86px)` 可壓縮來換空間，而不是把動作擠到下一列；搜尋條件的勾選列（姓名/陽上/往生/電話/備註）換行、列高改 `minmax(control-height, auto)`、編號 stepper 96→72px、「匯出 Excel」鎖 `white-space: nowrap` |

- 1100 這個值是量出來的：3 panel 並排時搜尋 grid 在容器 < 1088px 就會撐破 pane（搜尋鈕壓到「備註」勾選框）
- **flex 斷行看的是 `flex-basis` 不是 min-content**：要讓兩塊維持同列，tier 3 的 `.print-pane` basis 必須跟著調小（240px），否則會提前換行。240 + 動作 ~154 + gap 8 ≈ 容器 402px 才斷行（≈ 視窗 620px 以下）
- 容器 < ~520px（≈ 視窗 740px 以下）搜尋 grid 六欄仍會溢出，此時走 `.content` 的 horizontal scroll，不再另設斷點（桌面 App 啟動即最大化，屬極端情境）
- DataGrid 仍 horizontal scroll
- **禁止**把 `container-type` 設到 `.page` / `:host`：見 [gotchas.md](../gotchas.md)（會變成 fixed 子孫的 containing block，overlay 遮罩會縮在內容區）

#### DataGrid 實作（**2026-05-28.d virtual scroll + 欄寬持久化**）

舊 `<table>` 結構改成 **div-grid + CDK Virtual Scroll**：

```
<div class="vgrid-scroll-wrap">              ← 水平捲動容器
  <div class="vgrid-stack" [width]=totalW>   ← 內層 stack，寬度 = Σ 欄寬
    <div class="vgrid-header" [grid-template-columns]=widths>  ← sticky thead
      <div class="vgrid-th">...                                ← 含 resize handle
    </div>
    <cdk-virtual-scroll-viewport itemSize=26 height=600>       ← 垂直虛擬捲動
      <div *cdkVirtualFor="let item of results()"
           class="vgrid-row"
           [grid-template-columns]=widths>
        <div class="vgrid-td">...</div>
      </div>
    </cdk-virtual-scroll-viewport>
  </div>
</div>
```

**為什麼不用 `<table>`**：CDK virtual scroll 把可視範圍外的列卸載（DOM 只渲染 ~25 列），`<table>` 結構不能切斷 `<tr>` 序列；用 div-grid 才能搭配 virtual scroll。`role="table/row/cell"` 補 a11y 語義。

**欄寬持久化**（localStorage key `ceremony.signupList.colWidths`）：
- 每欄 header 右緣有 6px 拖把（`.vgrid-resize`），pointer-drag 改變該欄寬度
- 寬度 clamp 32–600px；存到 signal `columnWidths: Record<columnId, number>`
- effect 自動 sync localStorage；reload 後沿用上次寬度
- 重設按鈕（results-header）→ `columnWidths.set({})` 還原所有預設

**ShowAll 不落磁碟、但活在當次執行期間**（2026-07-29 定調不持久化，原本存 localStorage key `ceremony.signupList.showAll`；**2026-07-31 補**）：
- ☑顯示完整表格 **每次開軟體一律不勾**——原本記到 localStorage，勾過一次之後每次開軟體都是 32 欄的完整表格；日常用的是 27 欄版（欄寬持久化不受影響，仍保留）
- 但**同一次執行期間切到其他功能再回報名維護要保持原樣**（2026-07-31 客訴：回來就被取消勾選）→ 狀態存在記憶體 singleton `SignupSearchState.showAll`，關掉 App 即消失
- 搜尋條件（含「範圍」）同理：**任何條件變動就寫回快照**，不必先按搜尋——見 [frontend-design.md「搜尋條件的跨路由快照」](frontend-design.md)

**Virtual scroll 參數**：
- `itemSize=26`（每列固定 26px 高，CDK 用此值算 translate offset）
- `height=600px`（viewport 固定高，超出走垂直捲動）
- 整列 `style.height.px="26"` 強制固定，避免 cell 內容 wrap 撐破列高造成 virtual 計算偏移
- `cell { white-space: nowrap; overflow: hidden; text-overflow: ellipsis }` — 備註欄超長省略

#### Grid Context Menu（cmsSignups 等價，**2026-05-28 補規格**）

對應舊 [SignupForm.Designer.cs:236-313](../../reference/old/Ceremony/SignupForm.Designer.cs#L236-L313) 9 個 `ToolStripMenuItem`。詳細業務語意見 [signup-management blueprint 右鍵段](../blueprints/signup-management.md#grid-context-menucmssignups-等價新版重現)。

觸發方式（任一）：
- **右鍵點擊任一列**（desktop）— 自動選中該列再開選單（對齊 `dgvSignups_RowHeaderMouseClick` 舊行為）
- **列尾「⋮」kebab button**（touch / a11y）— 開同一選單
- **鍵盤 `Menu` 鍵 / `Shift+F10`**（focus 在某列時）
- **長按 800ms**（touch）

選單版型：

```
┌────────────────────┐
│ 代入新增           │ ← 單選 only，否則 grey
│ 修改資料           │ ← 單選 only
│ ────────────────── │
│ 列印資料卡         │
│ 列印收據           │
│ 列印薦牌           │
│ 列印文牒           │
│ 列印普桌           │ ← 僅 signupType filter == 4 才 enable
│ ────────────────── │
│ 刪除資料           │ ← danger color
│ 瀏覽歷程           │ ← 單選 only
└────────────────────┘
寬度：≥ 160px / 字級 14px / item 高 32px
分隔線：1px `--c-border` × 2 條
```

色彩規則：
- 一般 item：`--c-text` 文字 / 透明背景 / hover `--c-bg-soft`
- danger（刪除）：`--c-danger` 文字 / hover `--c-danger-soft` 背景
- disabled：`opacity: 0.4` + `cursor: not-allowed` + tooltip 說明原因（例：「請先選擇 1 筆」、「僅普桌類型 (4) 可列印」）

#### 多選列規格（**新增 vs 舊系統**）

舊 WinForms `DataGridView` 已支援多選但全靠 ctrl/shift；新版補上：

- **列首 checkbox**（24px 寬欄）— 第一欄
- **header checkbox** — 全選 / 全不選 / indeterminate（部分選）
- **點列任意位置** = 選中該列（單選；shift = 範圍；cmd/ctrl = 加入或移除）
- **狀態列**（grid 上方）顯示「結果 N 筆 / 已選 K 筆」+「取消選取」+「對選取項目 ▼」按鈕（同 context menu 內容）
- **選取狀態跨頁面保留**：使用者切到 `/signups/:id/edit` 再回來，選取重置（無持久化需求，避免邊界 case）

#### 批次列印面板（btnPrint_Click 等價）

獨立於 grid 選取（即使沒選任何列也可印），輸入：
- 起 / 迄編號（int，可避 4 顯示但 DB 仍存實值；後端 endpoint 接 int）
- reportType dropdown：資料卡 / 收據 / 薦牌 / 文牒 / 普桌

行為：
- 點「列印批次」→ 呼叫 `POST /api/v1/reports/batch`（body 額外帶 filter 區當前 year / ceremonyCategoryId / signupType，沿用現況）
- 回 PDF blob → 新分頁開啟 / 或彈出 `<iframe>` 預覽 + 下載
- 普桌：強制 signupType=4（即使 filter 是別的，warning 提示）

驗證訊息（verbatim）：
- numberEnd < numberStart → 400「編號錯誤」
- reportType 空 → 400「報表類型錯誤」
- 區間查無資料 → 404「查無符合條件的報名資料」

### NewSignupForm 兩步驟

Step1（左側 175 寬）：年份 / 法會 / 類型 / 下一步
Step2（右側 637 寬）：信眾搜尋 + 編輯區（含所有欄位）

**新版對應版面（2026-07-31 客訴第四輪後的現況）**：

```
┌ 法會資料 ┐┌─────────────── 右側主體 ─────────────────────────┐
│ 民國年   ││ 信眾（搜尋列 + 結果表，編輯模式為摘要卡）           │
│ 法會分類 │├──────────────────────┬───────────────────────────┤
│ 報名類型 ││ 堂號 姓名 聯絡電話     │ 員工類型  ☐固定編號         │
├─────────┤├──────────────────────┼───────────────────────────┤
│ 重複警示 ││┌ 寄件地址 ──────────┐│ 編號 / 費用 / 備註 / 預繳    │
│（有才顯示）│││ [城市▼][區域▼][郵號]││                           │
│         ││││ [地址_____________]││                           │
│         ││└────────────────────┘│                           │
│         ││┌ 文牒地址 ──────────┐│                           │
│         ││││        ☐ 同寄件地址││                           │
│         ││││ [城市▼][區域▼][郵號]││                           │
│         ││││ [地址_____________]││                           │
│         ││└────────────────────┘│                           │
│         ││┌ 往生名單 ──────────┐│                           │
│         ││└────────────────────┘│                           │
│         ││┌ 陽上名單 ──────────┐│                           │
│         ││└────────────────────┘│ [列印資料卡][取消][ 確認 ] ←靠左 │
└─────────┘└──────────────────────┴───────────────────────────┘
   ↑有外框      ↑三欄總寬＝下方地址框寬（同構巢狀，非寫死欄寬）
```

- 基本資料改序為 **堂號 → 姓名 → 電話 → 員工類型 → 固定編號**，且前三欄與下方地址框**逐像素等寬**（走與 `.form-cols` 相同的巢狀達成，不寫死欄寬）
- **地址拆成「寄件地址」「文牒地址」兩個 fieldset**，框內**欄位標題全部拿掉**改 placeholder——分段靠 legend 講就夠，每欄再標一次「寄件/文牒」是噪音；「同寄件地址」勾選移進文牒框內最上方、靠右
- **往生名單 / 陽上名單各自有外框與 legend**（解掉 07-29 那輪「填字後失去標示」的取捨）
- 目前**無外框**的只剩「基本資料」與「編號·費用·備註·預繳」兩區（`.bare-block`）
- 按鈕列靠左、「確認」套 `.btn-wide`
- 細節與理由見 [frontend-design.md「報名表單版面（2026-07-31 客訴第四輪）」段](frontend-design.md)

### Form Overlay（編輯彈窗，**2026-05-28.e 取代 Side Sheet 成為 create/edit 唯一 pattern**）

全系統 CRUD 的「新增 / 編輯」一律走 **置中 full-screen overlay**（不再用 side sheet / split-view / inline card）。共用 `<app-form-overlay>` shell，內含 backdrop + 置中 panel + header + body + footer actions。

```
┌─────────────────────────────────────────────────┐
│ Backdrop (rgba 42%)                             │
│                                                 │
│      ┌──────────────────────────────────┐       │
│      │ 標題                       [×]   │       │
│      ├──────────────────────────────────┤       │
│      │                                  │       │
│      │  表單內容（可捲）                  │       │
│      │  - 寬高 content-adaptive          │       │
│      │  - max 92vw × 92vh                │       │
│      │                                  │       │
│      ├──────────────────────────────────┤       │
│      │              [取消] [確認]        │       │
│      └──────────────────────────────────┘       │
│                                                 │
└─────────────────────────────────────────────────┘
```

**Panel 尺寸**：
- `min-width: min(420px, 92vw)`
- `max-width: 92vw`
- `max-height: 92vh`
- height 與 width 都 content-adaptive：簡單 form（2 欄）panel 小、複雜 form（25 欄）panel 大；都保留 4vh 邊距與背景列表可見

**動畫**：
- backdrop `@keyframes fadeIn` 120ms
- panel `@keyframes pop` 140ms（translateY(8px) → 0、opacity 0 → 1）

**寬度（2026-07-27）**：panel 預設 content-adaptive（`min-width: min(420px, 92vw)` / `max-width: 92vw`），但內容只要有寬表格就會被撐到 92vw——報名維護的新增/編輯 overlay 被 19 欄的信眾搜尋結果表撐滿，客訴「彈跳視窗太寬」。此時用 `<app-form-overlay width="1100px">` 給 panel 定寬（該值＝對應整頁版本 `.page` 的 `max-width`，讓 overlay 與整頁看起來一樣寬），寬表格自行橫向捲動；**限制 overlay 內層的表單元件無效**（panel 照樣被撐寬，底部 actions 會落單在右下角）。

**互動**：
- 點 backdrop、按 ESC、點 × button → 觸發 `tryClose()`
- **例外：`[dismissible]="false"` 只能用 × 關（2026-07-28 客訴）**——報名維護的新增/編輯報名 overlay，使用者指定「要點 X 或取消才會關起來」；backdrop click 與 Esc 兩個入口直接 early-return，× 與表單自己的取消鈕仍走 `tryClose()`（dirty 判斷不變）。適用時機＝表單長、誤關代價高
- form dirty 時 → `ConfirmDialogService.ask({ title: '未儲存的變更', message: '...', danger: true })`
- form 不 dirty → 直接關閉
- **例外：有草稿保護的表單不跳確認（2026-07-27）**——「新增報名」純新增模式的未完成內容會存成跨路由草稿、下次開啟自動帶回（見 [frontend-design.md「未完成表單的跨路由草稿」](frontend-design.md)），資料不會不見，故 host 把 `[dirty]` 綁成 `editFormDirty() && overlayGuardsDirty()` 直接關閉；其餘模式（編輯/代入新增/插入）維持確認

**底部 actions footer 可關掉（2026-07-28）**：`[showActions]="false"` 時不渲染 `.overlay-actions`——用在「按鈕列由內層表單自己排版」的場合（報名表單把 列印資料卡/取消/確認 移到備註下方）。留著會是一條只有 padding + 上框線的空灰帶。

**全域 class**（[styles.scss](../../frontend/src/styles.scss)）：
- `.overlay-backdrop` / `.overlay-panel` / `.overlay-header` / `.overlay-body` / `.overlay-actions`（可選，見上）/ `.overlay-close-btn`
- `@keyframes fadeIn` / `@keyframes pop`

**API**（[shared/form-overlay/form-overlay.component.ts](../../frontend/src/app/shared/form-overlay/form-overlay.component.ts)）：
```typescript
@Component({ selector: 'app-form-overlay' })
class FormOverlayComponent {
  readonly title = input.required<string>();
  readonly dirty = input<boolean>(false);
  readonly width = input<string | null>(null);
  readonly showActions = input<boolean>(true);
  readonly dismissible = input<boolean>(true); // false＝backdrop / Esc 不關
  readonly close = output<void>();
}
```

對齊本規範的 feature：[信眾](../../frontend/src/app/features/believers/) / [報名](../../frontend/src/app/features/signups/) / [法會分類](../../frontend/src/app/features/categories/) / [管理者](../../frontend/src/app/features/admins/) 共 4 個。

### Side Sheet（編輯抽屜，**已 deprecated 2026-05-28.e**）

舊規範：CRUD 頁面用右側滑入抽屜（560px 寬）。已被 Form Overlay 取代。`.sheet-*` 全域 class 暫保留以防其他用途，新功能請使用 Form Overlay。

### 資料備份頁面（`/backup`，**2026-05-29 新增**）

對應舊 MainForm「資料備份」按鈕。單一動作頁：一顆「開始備份」按鈕 → `ConfirmDialogService` 確認 → 執行中按鈕 disabled。

- **成功**：彈出**單一「確定」按鈕的結果 dialog**（沿用 ConfirmDialog 的 `hideCancel` 變體，非另造元件），顯示 fileName / fullPath / sizeBytes。
- **失敗**：dialog 顯示後端 verbatim 中文錯誤訊息（透過 `ApiError`）。
- **pattern 要點**：通知型「結果視窗」一律走 ConfirmDialog `hideCancel: true`（單 OK），不要再各自做 toast / alert，維持全系統 dialog 一致。
- **dialog 內文字級（2026-07-28）**：`.confirm-body` ＝ `--font-size-md`（訂定當時與側欄選單 `.nav-label` 同級，起因是使用者客訴「報名成功的提示字太小」）。dialog 是要人停下來讀的文字，不該太小；此為**全站共用**設定，改的是共用元件而非單一呼叫端。**2026-07-31 起與側欄脫鉤**：nav-label 改 20px，`.confirm-body` 維持 md 不跟進。

### 列印預覽頁面（`/reports/preview`，**2026-05-28 重新設計**）

舊系統用 ReportViewer 子視窗，新版用「文件預覽器」風格，**垂直堆疊**而非左右分欄：

```
┌────────────────────────────────────────────────┐
│ 頁標題                          [← 返回]       │
├────────────────────────────────────────────────┤
│ ┃ 單筆列印 ┃ 批次列印  ← tab 切換             │
│ 緊湊水平表單一條（max 7 個欄位）+ 送出按鈕    │
├────────────────────────────────────────────────┤
│ Toolbar: 檔名 [筆數badge] [新分頁][下載][關閉] │
│ ┌────────────────────────────────────────────┐ │
│ │   PDF iframe（滿寬，填滿至距底 12px）       │ │
│ └────────────────────────────────────────────┘ │
└────────────────────────────────────────────────┘
```

**為何不做兩欄並排**：左欄表單會被裁切 / 右欄 iframe 自然寬常溢出 grid cell；垂直堆疊一次解決寬度競爭。

**對應規格**：
- mode tabs：active 底線 = `--c-primary`，文字色同步
- 表單列：水平 flex，欄位帶 `min-width` 但允許 wrap；submit 按鈕固定在最右
- 預覽工具列：檔名 ellipsis、`max-width: 360px`；右側三個按鈕（**新分頁開啟** / **下載** / **關閉**）
- **預覽區填滿視窗、距底 12px**（2026-05-29）：`:host{height:100%}` → `.page` flex column → `.preview` `flex:1; min-height:0` → `.pdf-frame` `height:100%`（取代原 iframe 固定 720px / `.preview` `min-height:600px`）；對齊報名維護/信眾維護的填滿模式（shell `.content` padding-bottom 12px）
- 空狀態：📄 + 「尚未產生 PDF」+ 提示文字
- 路由：`/reports/preview` 與 `/reports/preview/:type` 都進同一元件，`:type` 預填 mode tab

## 列印版面（保留）

詳見 [printing-reports blueprint](../blueprints/printing-reports.md)。摘要：

| 報表 | 紙張 | 方向 | 字型 | 字級 |
|---|---|---|---|---|
| 資料卡 | 21 × 14.8cm（A5 橫） | Portrait | 標楷體 | 0.6-1cm |
| 收據 | 21 × 29.7cm（A4） | Portrait | 標楷體 | 雙聯設計 |
| 薦牌 | 11.5 × 25.4cm（牌位） | Portrait | 標楷體 | 大字 |
| 文牒 | 36.5 × 26.2cm（超寬） | Landscape | 標楷體 | 含垂直地址圖 |
| 普桌 | 21 × 29.6cm（A4） | Portrait | 標楷體 | 2cm 大字 |

邊界全部 0cm（滿版）— 新版需測試實體印表機 0.5cm 不可印區是否切到內容。

## 登入頁設計（品牌頁，不對齊 WinForms）

登入頁是**唯一刻意脫離 WinForms 版型**的畫面，定位為品牌門面（管理員每天第一眼），需專業、莊重、有寺院品牌感。

- **版面**：單欄垂直置中 — 品牌圓窗 → 寺名標題 → 登入卡 → 版本號。短螢幕（`max-height:640px`）改頂齊；窄螢幕（`max-width:480px`）縮放。
- **Signature 元素「廟門圓窗」**：三層同心圓（純 CSS `radial-gradient`），外圈陶土光暈 → 中圈半透明奶油環 → 核心陶土圓置中 Logo（`/logo.png`，存於 `frontend/public/`）。象徵圓滿，取代通用 SaaS 分割版。
- **Logo 處理**：核心圓內 Logo 用 `mix-blend-mode: luminosity` 融入陶土色，作為「視覺意象」而非主辨識；辨識由下方「寶覺寺」標楷體標題承擔（若要 Logo 全彩可移除該 blend mode）。
- **排版**：寺名「寶覺寺」用 `--font-print`（標楷體）34px、`letter-spacing:0.12em`、`font-weight:normal`（楷體筆畫已足夠份量）；副標「法會報名系統」用 `--font-ui` 寬字距 0.22em。
- **登入卡 / 控制項**：登入頁控制項比 dense admin 大一階 — input 38px、按鈕 44px（系統標準 28/32px），focus ring 用 `--c-primary` 3px 外光暈。
- **配色**：僅用既有 design tokens（陶土 `--c-primary` 系 + 米白 `--c-bg`），未新增色票。
- **動畫**：差序入場（圓 60ms → 標題 160ms → 卡 220ms → 版本 360ms），尊重 `prefers-reduced-motion`。
- **稽核例外**：登入頁 SCSS 含豐富裝飾，`angular.json` 的 `anyComponentStyle` budget 由 4kB 調升至 6kB。
- **wiring 不動**：reactive form / `auth.login()` / 導向 `/` / `errorMessage` signal / `submitting` 狀態全保留，僅換 HTML+SCSS。

檔案：[../../frontend/src/app/features/login/](../../frontend/src/app/features/login/)（`login-page.html` / `.scss`，`.ts` 邏輯未變）。

## 鍵盤 / a11y

- 所有按鈕、選單、欄位可純鍵盤操作
- Tab 順序對齊舊 Designer.cs 的 TabIndex
- AdminsForm 舊版 Enter→Tab 行為**改為標準 Enter=submit**（新版預設），但保留設定切換
- Esc 關閉 dialog
- F5 重新整理當前清單
- Ctrl+N / Ctrl+P / Ctrl+S 等捷徑

## 響應式（DPI / 縮放）

- 預設 100%，提供 100% / 125% / 150% 切換
- 所有尺寸用 rem，根字級隨縮放調整
- 全 layout 用 CSS Grid / Flex，避免絕對定位
- 大表單在 100% 下保證在 1080p 顯示完整

## 驗收標準

- [ ] 每個對齊舊 Form 的頁面，與舊系統並排比較，欄位位置誤差 ≤ 8px
- [ ] 所有按鈕文字、錯誤訊息文字 verbatim
- [ ] 主要操作流程（登入 → 報名 → 列印）步驟數與舊版相同
- [ ] 鍵盤 Tab 順序與舊 Designer.cs 一致
- [ ] 列印版面與舊 RDLC 並排，欄位位置誤差 ≤ 0.2cm
