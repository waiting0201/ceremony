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
keywords: [visual, ui, design, layout, 版型, 樣式, 編排, WinForms, 一致性, Claude配色, 暖米色, 珊瑚橘]
last_updated: 2026-07-02 (往生欄底色 --c-dead-name-bg 改深，避免跟 hover/選取色混淆)
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

字級（CSS 變數，**2026-07-02 決策：全面 +1px**）：

```css
--font-size-xs: 12px;    /* 小標注 (例：(不須填符號，例：0987654321)) */
--font-size-sm: 13px;    /* 表單欄位 */
--font-size-base: 14px;  /* 主要內容 */
--font-size-md: 15px;    /* 按鈕、強調 */
--font-size-lg: 17px;    /* 區塊標題 */
--font-size-xl: 21px;    /* 頁面標題 */
```

對應舊 WinForms 9pt~12pt（96 DPI 下 9pt ≈ 12px）；**+1px 理由**：實測比對舊系統後，使用者反映新系統文字整體偏小，故全部字級一律加 1px 拉近觀感（單一來源：[frontend/src/styles.scss](../../../frontend/src/styles.scss)，改此處全站生效，無需逐頁調整）。

## 色彩（Claude 配色 — 暖米/珊瑚橘）

> **2026-05-26 決策**：放棄原 WinForms 灰藍配色，全面採用 Claude 品牌配色（暖米色背景 + 珊瑚橘主色），保留 WinForms 的**版型編排**但更新視覺語言。理由：(1) 客戶指定 (2) 暖色系比冷灰更符合宗教場域氛圍 (3) 與舊系統視覺差異化，避免使用者誤以為「沒變」。

| token | 色碼 | 用途 |
|---|---|---|
| `--c-bg` | `#FAF9F5` | 視窗背景（暖米色） |
| `--c-bg-darker` | `#F0EBE0` | 側邊欄 / panel header（深一階米色） |
| `--c-surface` | `#FFFFFF` | 卡片 / panel 內容區 |
| `--c-border` | `#D9D2C2` | 主框線（暖灰） |
| `--c-border-soft` | `#E8E2D3` | 次要框線 / 分隔 |
| `--c-text-primary` | `#2C2A26` | 主文字（深暖黑） |
| `--c-text-secondary` | `#7A7466` | 次要文字（暖灰） |
| `--c-text-disabled` | `#B3AC9C` | 禁用文字 |
| `--c-primary` | `#CC785C` | 主動作按鈕（Claude 珊瑚橘） |
| `--c-primary-hover` | `#B86847` | hover 加深 |
| `--c-primary-soft` | `#F5E5DC` | 「新增報名」按鈕軟珊瑚底 + 選取列 |
| `--c-danger` | `#C84A3A` | 刪除 / 錯誤（深紅橘） |
| `--c-warning` | `#E5A53D` | 警告（暖琥珀） |
| `--c-success` | `#6B8E5A` | 成功（暖綠） |
| `--c-dead-name-bg` | `#E3B274` | 往生名欄位 highlight（**2026-07-02 改深**：原 `#EFDCC4` 跟 `--c-primary-soft`/`--c-row-alt` 這類淺米色系太接近，在 grid hover 時幾乎分不清，改用更深/更飽和的琥珀棕以確保任何狀態下都能一眼認出往生欄） |
| `--c-row-selected` | `#F5E5DC` | DataGrid 選取列 |
| `--c-row-alt` | `#FAF8F2` | DataGrid 偶數列 |

> 「新增報名」按鈕舊系統用 light blue 強調，新版改用 `--c-primary-soft`（軟珊瑚）保持視覺重點，符合 Claude 配色。

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

### TextBox / ComboBox / DateInput

- 高度 `--control-height`
- 框線 1px `--c-border`，focus 時 `--c-primary` + 1px ring
- 錯誤態：紅色框線 + 下方 11px 紅字訊息

### DataGrid

- 標題列：背景 `--c-bg`、深字 bold、單邊 1px border
- 列高 24px（對齊 WinForms RowTemplate.Height）
- 偶數列：白；奇數列：`#FAFAFA`
- 選取列：`--c-row-selected`
- 往生 1..5 欄：背景 `--c-dead-name-bg`
- 隱藏欄位：CSS `display: none`，由 column-toggle 控制

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

## 表單區塊（與舊 WinForms 對齊）

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
│ │ ☑啟用       年份[___]   ☑姓名 ☑陽上 ☑往生 ☑電話  ┃ ││[起]~[迄]││+新增 │ │
│ │ ☑顯完整 法會[ ▼ ]  關鍵字[__________]  ☑固定編號  ┃搜││[類型▼]  ││✎修改 │ │
│ │           類型[ ▼ ]  編號[__]   [匯出 Excel]      ┃尋││  [列印] │└─────┘ │
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
Row 1: ☑啟用              [年份][__]      ☑姓名 ☑陽上 ☑往生 ☑電話   ┐
Row 2: ☑顯完整         [法會][ ▼ ]      [關鍵字___________]  ☑固定編號 │ [搜尋]
Row 3:                 [類型][ ▼ ]      [編號][__]   [匯出 Excel]      ┘
```

- 高度 110px，row gap 4px，column gap 8px
- input/select 高度 28px（`--control-height`），font 12px
- 「搜尋」按鈕 col-span row 1–3（縱向高鈕，對齊舊 btnSearch 75×99）
- 「匯出 Excel」按 inline 嵌在 row 3（對齊舊 btnExportExcel 在 plSearch 內，**不**獨立成按鈕）

#### 列印 pane 內部 grid（對齊 plPrint 203×127）

```
Row 1: [起__] ～ [迄__]              ┐
Row 2: [報表類型 ▼]                  │ [列印]
                                     ┘
```

- 「列印」按鈕 col-span row 1–2（縱向高鈕，對齊舊 btnPrint 75×63）

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
- font-size 12px (`--font-size-sm`)
- 列高 ≈ 26px (`padding: 4px 6px`)
- 往生欄背景 `--c-dead-name-bg` (#E3B274)（對齊舊 DataGridView DefaultCellStyle；2026-07-02 從 #EFDCC4 改深，避免跟 hover/選取色系混淆）
- 選取列覆蓋層 `--c-row-selected` (#F5E5DC)；選取列且往生欄走 mix
- 表頭 sticky top（捲動時欄名不動）
- 寬度 `width: max-content; min-width: 100%`（內容驅動寬度，溢位走 horizontal scroll）

**RWD**：
- ≥ 1100px：3 panel 並排
- < 1100px：toolbar 改 stack（單欄堆疊）；DataGrid 仍 horizontal scroll

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

**ShowAll 持久化**（localStorage key `ceremony.signupList.showAll`）：
- ☑顯示完整表格 切換 → effect 寫 localStorage；reload 後沿用上次狀態

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

**互動**：
- 點 backdrop、按 ESC、點 × button → 觸發 `tryClose()`
- form dirty 時 → `ConfirmDialogService.ask({ title: '未儲存的變更', message: '...', danger: true })`
- form 不 dirty → 直接關閉

**全域 class**（[styles.scss](../../frontend/src/styles.scss)）：
- `.overlay-backdrop` / `.overlay-panel` / `.overlay-header` / `.overlay-body` / `.overlay-actions` / `.overlay-close-btn`
- `@keyframes fadeIn` / `@keyframes pop`

**API**（[shared/form-overlay/form-overlay.component.ts](../../frontend/src/app/shared/form-overlay/form-overlay.component.ts)）：
```typescript
@Component({ selector: 'app-form-overlay' })
class FormOverlayComponent {
  readonly title = input.required<string>();
  readonly dirty = input<boolean>(false);
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
