---
title: Frontend Design
purpose: 法會報名系統重構版的前端架構：Electron + Angular 17 桌面應用，介面編排對齊原 WinForms
applicable_when: 要新增/修改前端元件、要決定路由、要處理 state、要對齊 WinForms 版型、要實作列印預覽
related_agents:
  - frontend-architect
related_docs:
  - visual-design.md
  - frontend-coding-style.md
  - api-design.md
  - ../blueprints/printing-reports.md
keywords: [frontend, 前端, Electron, Angular, Vue, WinForms, 桌面, layout, signal, NgRx, context-menu, 右鍵, 多選, version, 版本, num-stepper, 捲軸, scrollbar, NumericUpDown, 草稿, draft, 未儲存]
last_updated: 2026-07-31 (`.num-stepper` 段補記：報名維護搜尋「編號」與批次列印起迄皆為起~迄兩格，搜尋欄外層 `.number-field` 用 flex 均分欄寬不寫死。同日先前新增「搜尋條件的跨路由快照」段：`form.valueChanges` 無條件寫回 singleton（原本只在 `search()` 存 → 沒按搜尋的條件變動全丟）、純 UI signal（`showAll`）要另開一格且用 toggle 直接同步而非 effect；DataGrid「顯隱欄位 toggle 不落 localStorage」補註「不持久化≠元件銷毀就丟」。同日先前報名表單客訴第四輪補第 4 項：員工類型下拉寬度＝右欄「費用」欄寬——`.grid.basic-side` 改用與費用所在 `.grid.three` 同一組 `repeat(3, minmax(0,1fr))`、勾選框吃 `grid-column: 2 / -1`，並補 760px 降兩欄、480px 還原 `grid-column: auto` 兩個斷點。同日先前新增「送印路徑」段：core/print/PrintService 成為唯一列印入口，Electron 走自建 print-dialog + 主行程送印、瀏覽器退回 openPdfInNewTab；BatchPrintService.run 新增 takeFile 選項（預設 true 行為不變）；報表預覽頁工具列加「列印」鈕並註記 iframe 依賴 plugins:true。同日先前RWD 策略補「媒體查詢 vs 容器查詢」規則：內容排不下的斷點一律改用 `@container`（可用寬 = 視窗 − 側欄 − padding，`@media` 量不到側欄收合），首個案例為報名維護 toolbar。同日先前報名表單版面客訴第四輪〔**部分反轉 07-29 的「拿掉全部外框」**〕：地址拆成「寄件地址」「文牒地址」兩個 `fieldset.block` 且框內欄位標題全拿掉改 placeholder、「同寄件地址」勾選移進文牒框內最上方；往生/陽上名單各自改回 fieldset+legend〔解掉 07-29 記錄的「填字後失去標示」取捨〕；基本資料改序為 堂號→姓名→電話→員工類型→固定編號，前三欄以「與 `.form-cols` 同構」的巢狀達成與地址框逐像素等寬〔`.grid.basic-row` 移除、新增 `.grid.basic-side`〕。同日「同寄件地址」觸發條件放寬為「城市/區域/地址三者全空才擋」〔刻意偏離舊系統，兩張表單同步〕。信眾表單同步套用地址框與放寬條件。見「報名表單版面（2026-07-31 客訴第四輪）」段；先前 2026-07-29 (報名維護清單客訴三項改 DataGrid 規格兩條：顯隱欄位 toggle 偏好**不再落 localStorage**〔每次進頁回預設，欄寬持久化不動〕；「全部」條件旁路 toggle 收斂為「只解除年份/法會/類型（＋範圍）」——其餘條件仍生效仍可搜尋、`.all-mode` 只淡化對應 label、停用清單不得與 scope*→關鍵字的連動相交；另法會欄預設寬 100→64px（見 visual-design.md）。同日先前報名表單版面客訴第三輪：基本資料排成一整列並上提為 `.form-cols` 上方全寬列〔右欄「編號/費用/備註/預繳」隨之下移〕、按鈕列改靠左、**五個區塊（基本資料/地址/往生名單/陽上名單/編號·費用·備註·預繳）全部拿掉 fieldset 外框與 legend 改 `.bare-block`，只剩「法會資料」保留外框**〔名單兩塊填字後失去往生/陽上標示，屬已知取捨〕、「確認」鈕套新的全域 `.btn-wide`〔min-width 112px＝兩字鈕自然寬 ×2〕；`ConfirmDialogConfig` 新增 `emphasis` 旗標〔訊息 20px + 確認鈕加寬〕並用於「新增報名成功」——只給結果型提示，不再動全站 dialog 字級。見「報名表單版面」與「ConfirmDialog 強調變體」兩段；先前 2026-07-28 (新增 `<app-progress-overlay>` 共用 shell 與 `BatchPrintService`：批次列印改 job 模型後，三個批次入口（編號區間/多選/列印預覽頁）會顯示置中進度 overlay，含真實 i/N 百分比與取消鈕；記錄 CDK setInput 必須傳新物件、取消時不自動關 overlay 的理由；`ReportApi.batch()` 移除。同日先前 form-overlay 新增 dismissible input（false＝backdrop click / Esc 不再關閉，只剩 × 與 host 自己的取消鈕；報名維護新增/編輯 overlay 指定 false，其餘三個 feature 不變）；同日先前新增「Enter 就送出的開關是那顆隱藏 submit 按鈕」段（報名表單改為 Enter 不送出：移除隱藏鈕＋ngSubmit＋form 層攔 Enter 放行 textarea；其他三個 edit-form 不跟進）；同日先前版面客訴第二輪：地址段「同寄件地址」改夾在寄件/文牒之間、文牒郵遞區號回到區域右邊、文牒地址加寬；按鈕列（列印資料卡/取消/確認）移到備註下方——signup-edit-form 新增 form-actions 投影 slot、form-overlay 新增 showActions input 收掉底部 footer、路由頁 .form-actions 移除；同日先前四項：信眾搜尋框字級對齊地址欄〔.search-input 不在 .field 內、只繼承 font-family，字級落回 UA 預設 13.33px〕、往生/陽上名單由右欄移到左欄地址下方、「同寄件地址」移到文牒郵遞區號正上方〔第一列第三欄，郵遞區號下移到文牒地址同列，不多佔列高〕、備註 rows=1→4；ng build 綠，待實機複驗；先前 2026-07-27 (Grid Context Menu Pattern 新增「shift 範圍選取」段：錨點語意（不隨 shift 移動、以錨點當下選取為基準重算故可縮小範圍）＋兩個坑（列首 checkbox 必須綁 click 不能綁 change 否則拿不到 shiftKey、shift 點列要在 mousedown preventDefault 擋文字反白）；同日先前 DataGrid 規格新增「全部」條件旁路 toggle 規範（停用而非清空條件、buildQuery 單點短路讓搜尋/匯出一致、.all-mode 淡化 label、離開模式要還原既有連動規則）；同日先前 form-overlay 新增 width input（panel 定寬；內容含寬表格時必給，否則 panel 被撐到 92vw——限制內層表單無效且會讓底部 actions 落單）；同日全域 .field 補 input/select/textarea :disabled 樣式（原本無條件設 background/color 蓋掉瀏覽器預設 disabled 外觀，disabled 看起來跟可輸入一樣）；同日新增報名版面：法會資料改左側直立窄欄（.form-shell 兩欄 grid，回舊 plStep1）、信眾搜尋結果高度縮為表頭+3 列；同日新增「.field 外自刻 form control 要補齊 color/background」段（UA fieldtext 純黑導致名單欄字偏黑客訴）；同日新增「未完成表單的跨路由草稿」段：SignupDraftState root singleton，僅新增報名、僅記憶體、靜默還原，純新增模式關閉 overlay 不再跳未儲存確認；2026-07-21 起：(新增共用 .num-stepper 數字微調控件〔input+▲▼ ±1，對齊舊 NumericUpDown〕；報名維護清單新增垂直捲軸右鍵子選單〔捲動到這裡/頂端/底部/上下頁/上下捲〕對齊舊 WinForms 原生捲軸選單；2026-07-18：believer-edit-form 比照 signup-edit-form 改版：地址寄件上/文牒下、名單往生上/陽上下無底色；兩表單名單 legend 拿掉「最多 6 位」字樣))))
---

## 已落地骨架（2026-05-28 更新）

- 位置：[../../frontend/](../../frontend/)
- 版本：Angular CLI 21.1.4 + Angular 21.1（**zoneless 預設**）+ TypeScript 5.9 + SCSS + Vitest
- 已裝套件：`@angular/material` ^21.2.12 (rose primary / orange tertiary palette, density -2)、`@ngrx/signals` ^21.1.0、`@angular/animations` ^21.1.0
- **9 條 feature 路由全部串接 backend**（PlaceholderPage 已退役為 fallback）：
  - `/login` → 真打 `POST /api/v1/auth/login`，顯示 backend verbatim 錯誤訊息
  - `/`（dashboard）+ `/admins` / `/believers` / `/categories` / `/prepay` / `/signups` (+ `/new`、`/:id/edit`、`/:id/logs`) / `/reports/preview/:type?` 全部接 typed API service
- **HTTP 基礎建設**：
  - [src/environments/](../../frontend/src/environments/) — dev `http://localhost:5050/api/v1`、prod `/api/v1`
  - [core/http/api-error.ts](../../frontend/src/app/core/http/api-error.ts) — `ApiError` 包裝 backend `{errorCode, message, traceId}` payload
  - [core/http/auth.interceptor.ts](../../frontend/src/app/core/http/auth.interceptor.ts) — 對 apiBaseUrl 開頭 request 注入 Bearer；401 → clearSession + /login redirect
- **AuthStore**（signalStore）：login 真打 API、token + user 持久化在 `localStorage` key `ceremony.auth.v1`，logout 先打 `/auth/logout` 撤銷再清本地
- **typed API 層**（[core/api/](../../frontend/src/app/core/api/)）：6 個 domain × 對應 backend Application contract 1:1（不額外抽 base class，每個 service 用 `inject(HttpClient)` + `firstValueFrom`）
- **共用 utility**（[shared/](../../frontend/src/app/shared/)）：`avoidFour` pipe、`flattenCategories`、`SIGNUP_TYPES`、`PREPAY_GROUPS`、`currentTaiwanYear()`、**`<app-icon>`**（inline SVG，見 [visual-design.md Icon 段](visual-design.md#icon-2026-05-28-決策)）
- **全域樣式**：[src/styles.scss](../../frontend/src/styles.scss) 內含 18 個 `--c-*` token + `.btn` / `.btn-primary` / `.btn-danger` / `.btn-sm` / `.alert` / `.hint` / `.data-table` / `.field` / `.card` / `.toolbar` utility class（避免每頁撞 4kB SCSS budget）
- 路由：`provideRouter(routes, withHashLocation())` 為 Electron 預備；`/login` 獨立、其餘走 `ShellLayout` + `authGuard`
- 跑：`cd frontend && npm start` → http://localhost:4200/

下階段：依 status.md backlog（列印精修 / 客戶驗收 / Electron 包裝）推進；前端骨架可立即跑全流程 smoke。

## 實作順序（**2026-05-26 決策，2026-05-28 補充 sidecar pattern**）

1. **先做 Angular SPA（瀏覽器版）**：所有 feature 在純 web 環境下開發完成（用 `ng serve`），可直接用 Chrome / Edge 跑完整流程
2. **整個系統可運作後，最後一階段才包 Electron**：把已完整的 Angular build + **後端 .NET API self-contained exe** 用 electron-builder 包成單一 Windows 安裝檔
3. **僅打包 Windows 版本**（見 [infrastructure.md](infrastructure.md)）；桌面 icon 沿用舊系統 .ico

> 理由：(1) Electron 只是「桌面殼」，業務邏輯與 UI 都在 Angular 層；先 Angular 可加速開發、簡化除錯（無 IPC 噪訊） (2) 列印、檔案儲存等少數需要 native 能力的功能先用 web API 替代（PDF 下載 / window.print），上 Electron 後再以 IPC 取代 (3) 客戶只用 Windows，跨平台需求是 0

### Sidecar 部署模型（**2026-05-28 決策**）

最終打包後，installer 內含**三層**：
- **Electron main**：UI shell + 子進程管理
- **Angular SPA**：renderer 載入的靜態檔
- **Ceremony.Api.exe**：.NET self-contained sidecar；隨 Electron 啟動 / 關閉

DB 仍是**獨立主機**（既有 MSSQL Server），sidecar API 透過 LAN 連線。每台 client 一個 .exe 含自己的 API instance，所有 client 共用同一個 DB。**安全認證採方案 C**（純文字 config.json 存 user profile），詳見 [infrastructure.md 部署型態](infrastructure.md#部署型態2026-05-28-改為-sidecar-架構)。

對前端的影響：
- **Dev 模式**：`apiBaseUrl` 從 [environment.ts](../../frontend/src/environments/environment.ts) 寫死取（`http://localhost:5050/api/v1`）
- **Prod 模式**：API port 動態指派，Electron main 啟動後透過 query string `?apiBase=...` 傳給 renderer，[main.ts](../../frontend/src/main.ts) 啟動時讀後覆寫 `environment.apiBaseUrl`

## 技術選型

| 面向 | 選擇 | 理由 |
|---|---|---|
| Shell | **Electron 30**（**僅 Windows**，**含 .NET sidecar API**） | 桌面感受 + 沿用舊 icon；installer 同時包 API exe |
| Framework | **Angular 18+**（Standalone + **Signal-first**） | 強型別 + Signal 反應式；對複雜表單與大量 reactive 狀態最契合 |
| UI Kit | **Angular Material + Custom Theme** | DataGrid、Tree、Dialog 內建；可調樣式對齊 WinForms |
| 狀態管理 | **Signals + signalStore (NgRx Signals)** | 全 Signal-first，少用 RxJS |
| Routing | Angular Router（hash mode for Electron） | – |
| HTTP | HttpClient + Interceptors（auth / error） | – |
| Form | Reactive Forms + signal-based form values | Reactive Form 仍用於驗證骨架，值同步出 signal |
| i18n | @angular/localize | – |
| Date | Day.js + 自製民國年 helper | 與 TaiwanCalendar 對應 |
| 列印預覽 | **PDF.js + iframe** | 後端產 PDF，前端嵌入預覽 |
| 主視窗管理 | electron-window-state | 記憶大小/位置 |
| 打包 | electron-builder（**NSIS only**）+ `extraResources` 引入 .NET sidecar exe | 僅 Windows；無 mac / Linux target |

> **Signal-first 是硬性要求**：本專案全面採用 Angular Signals API（`signal`, `computed`, `effect`, `input()`, `output()`, `model()`, `resource()`, `linkedSignal()`），不用 RxJS Subject / BehaviorSubject 管狀態。只在需要 stream 操作（debounce、throttle、merge）才用 RxJS。

## 桌面結構

```
electron/
├── main.ts                # Electron main：spawn .NET sidecar + 載 Angular renderer
├── preload.ts             # contextBridge 暴露 IPC
├── sidecar.ts             # 子進程管理（spawn / health check / kill on quit）
├── config.ts              # 讀寫 %APPDATA%/Ceremony/config.json
└── ipc/
    ├── setup.ts           # 首次啟動設定頁的 IPC（測連線 / 存 config）
    ├── backup.ts          # 觸發後端備份 API + 原生對話框
    ├── print.ts           # 系統列印 / PDF 儲存對話
    └── window.ts          # 子視窗管理

resources/                 # electron-builder 打包時填入
└── api/
    └── Ceremony.Api.exe   # .NET self-contained，runtime spawn 為子進程

renderer/                  # Angular app
├── app/
│   ├── core/
│   │   ├── auth/          # 登入、token、guards（對應舊 Global.cs）
│   │   ├── http/          # interceptors
│   │   ├── electron/      # IPC client (renderer-side)
│   │   └── layout/        # ShellLayout (對應 MainForm)
│   ├── features/
│   │   ├── login/         # → LoginForm
│   │   ├── admins/        # → AdminsForm
│   │   ├── believers/     # → BelieverForm
│   │   ├── signups/
│   │   │   ├── list/      # → SignupForm
│   │   │   ├── create/    # → NewSignupForm (兩步驟)
│   │   │   ├── edit/      # → EditSignupForm
│   │   │   └── logs/      # → SignupLogForm
│   │   ├── prepay/        # → LoadPrepayForm
│   │   ├── categories/    # → CeremonyCategoryForm
│   │   └── reports/       # 列印預覽 + 匯出
│   ├── shared/
│   │   ├── address-picker/        # 縣市/區/門牌組合元件（重用率高）
│   │   ├── name-list-input/       # 6 格名單輸入元件
│   │   ├── number-display/        # 避 4 顯示 pipe
│   │   ├── data-grid/             # 包裝 mat-table，提供 column-toggle、context menu
│   │   ├── dialog/                # 對應 CustomDialogForm
│   │   └── message/               # 對應 CustomMessageForm
│   └── models/            # 共享 DTO interface
└── assets/
    ├── fonts/             # BiauKai/標楷體 + 微軟正黑體（embed）
    └── images/            # 普桌背景圖等
```

## 版型對齊原則（**介面編排要一致**）

詳細規格見 [visual-design.md](visual-design.md)；前端執行重點：

1. **窗格位置/比例 1:1 對應 WinForms**
   - 例：SignupForm 為「上中下三段」=> 上方 filter bar + 中段 grid + 下方 status bar
   - BelieverForm 為「左 grid + 右編輯區」=> CSS Grid 兩欄
2. **控件順序與 Tab 鍵順序**：與舊 Designer.cs 的 TabIndex 完全一致（提取至 design tokens）
3. **按鈕文字 verbatim**：「確認」「取消」「新增」「修改」「刪除」「搜尋」「下一步」「匯出Excel」等不可改字
4. **驗證錯誤訊息 verbatim**：所有 alert 文字保留繁體中文原樣
5. **避 4 規則延伸至 UI**：欄位 header 顯示「3-1」非「4」（陽上 3-1、往生 3-1）
6. **快捷鍵**：
   - Enter 在舊 AdminsForm 等於 Tab — 新版改為標準 Enter=submit，但保留設定切換
   - F5 = 重新整理、Esc = 取消、Ctrl+N = 新增、Ctrl+P = 列印

## 路由

| Path | Component | 對應舊 Form |
|---|---|---|
| `/login` | LoginPage | LoginForm |
| `/` | ShellLayout + DashboardPage | MainForm |
| `/admins` | AdminsListPage | AdminsForm |
| `/believers` | BelieversPage | BelieverForm |
| `/signups` | SignupListPage | SignupForm |
| `/signups/new` | SignupCreatePage | NewSignupForm |
| `/signups/:id/edit` | SignupEditPage | EditSignupForm |
| `/signups/:id/logs` | SignupLogsPage | SignupLogForm |
| `/prepay` | PrepayLoadPage | LoadPrepayForm |
| `/categories` | CategoryTreePage | CeremonyCategoryForm |
| `/backup` | BackupPage | MainForm（btnBackup） |
| `/reports/preview/:type` | ReportPreviewPage | (列印預覽) |

> Sidebar nav 順序（對齊舊 MainForm 按鈕順序）：信眾維護 → 新增報名 → 報名維護 → 載入預繳 → **資料備份** → 法會類型 → 管理者 → 列印預覽。`資料備份`（icon `database`）位於「載入預繳」與「法會類型」之間。

Guards：
- `AuthGuard` 保護所有頁面（`/login` 例外）
- `UnsavedChangesGuard` 在 SignupCreate/Edit 離開前確認

## 狀態管理（Signal-first）

全專案統一用 Angular Signals + 衍生：

| 場景 | 用法 |
|---|---|
| 單元件 state | `signal()` / `computed()` |
| 元件 input | `input()` / `input.required()` |
| 元件 output | `output()` |
| 雙向綁定 | `model()` |
| Effect | `effect()` 取代 `ngOnChanges` |
| 非同步資源 | `resource()` / `rxResource()` 取代 `Subject + subscribe` |
| 連動 derived state | `linkedSignal()` |
| 跨頁面共享 | **signalStore**（@ngrx/signals）— 取代傳統 NgRx Store |

對應舊 `Global.cs`：

```typescript
// core/auth/auth.store.ts
import { signalStore, withState, withComputed, withMethods } from '@ngrx/signals';

export const AuthStore = signalStore(
  { providedIn: 'root' },
  withState<{ user: AuthUser | null }>({ user: null }),
  withComputed(({ user }) => ({
    isLoggedIn: computed(() => user() !== null),
    adminId: computed(() => user()?.id ?? null),
    username: computed(() => user()?.username ?? null),
  })),
  withMethods((store, api = inject(AuthApi)) => ({
    async login(credentials: LoginRequest) {
      const result = await api.login(credentials);
      patchState(store, { user: result.user });
    },
    logout() {
      patchState(store, { user: null });
    },
  })),
);
```

### Signup 搜尋 store 範例

```typescript
export const SignupSearchStore = signalStore(
  withState<SignupSearchState>({
    year: getCurrentTaiwanYear(),
    isScope: false,
    ceremonyId: null,
    signupType: -1,
    key: '',
    scopeName: true,
    scopeLivingName: false,
    scopeDeadName: false,
    scopePhone: false,
    page: 1,
    pageSize: 50,
    results: [],
    total: 0,
    loading: false,
  }),
  withComputed(({ scopeName, scopeLivingName, scopeDeadName, scopePhone, results, total, page, pageSize }) => ({
    canSearchByKey: computed(() =>
      scopeName() || scopeLivingName() || scopeDeadName() || scopePhone()),
    hasResults: computed(() => total() > 0),
    pageCount: computed(() => Math.ceil(total() / pageSize())),
  })),
  withMethods((store, api = inject(SignupApi)) => ({
    async search() {
      patchState(store, { loading: true });
      try {
        const result = await api.search(buildQuery(store));
        patchState(store, { results: result.items, total: result.total, loading: false });
      } catch (e) {
        patchState(store, { loading: false });
        throw e;
      }
    },
    setPage(page: number) {
      patchState(store, { page });
      this.search();
    },
  })),
);
```

元件用：
```typescript
@Component({
  template: `
    <div *ngIf="!store.loading()">
      共 {{ store.total() }} 筆
      <button (click)="store.search()">搜尋</button>
    </div>
    <ng-container *ngIf="store.loading()">搜尋中，請稍後...</ng-container>
  `,
})
export class SignupListComponent {
  readonly store = inject(SignupSearchStore);
}
```

### 避免反模式

- ❌ `BehaviorSubject<T>` 管狀態
- ❌ `Observable + async pipe + ngOnInit subscribe`
- ❌ `@Input() set` getter/setter 監聽變化
- ❌ `ngOnChanges` 反應變化（用 `effect()`）
- ❌ `ChangeDetectorRef.markForCheck()`（Signal 自動觸發）

## 表單策略

舊 WinForms 用 Validating event + MessageBox 即時彈窗；新版採 Reactive Form 骨架 + Signal 同步：

- `FormGroup` 用於驗證 + 結構，但表單值用 `toSignal(form.valueChanges)` 轉 signal
- 驗證錯誤訊息顯示在欄位下方（紅字）
- 提交時若有 error → focus 第一個錯誤欄位
- 保留**送出後彈出成功訊息**的舊體感（snackbar，文字 verbatim：「新增信眾成功！」）

```typescript
@Component({...})
export class NewSignupComponent {
  readonly form = inject(FormBuilder).group({
    year: [getCurrentTaiwanYear(), [Validators.required, taiwanYearValidator()]],
    name: ['', Validators.required],
    phone: ['', phoneValidator()],
    // ...
  });

  // 表單值 → signal
  readonly formValue = toSignal(this.form.valueChanges, { initialValue: this.form.value });

  // 衍生（自動依 signal 更新）
  readonly canSubmit = computed(() => this.form.valid && !this.submitting());
  readonly submitting = signal(false);

  // effect 監聽 year 變化 → 自動載 ceremony list
  constructor() {
    effect(() => {
      const year = this.formValue().year;
      if (year) this.loadCeremonies(year);
    });
  }
}
```

關鍵 validator（與舊 regex 對齊）：
- `taiwanYearValidator`：`^1[0-9]{2}$`
- `phoneValidator`：`^0[0-9]*$`
- `positiveIntValidator`：`^[1-9][0-9]*$`
- `feeValidator`：`^[0-9]*$`
- `notInPast(currentYear)`

## 列印預覽 / 匯出（**2026-05-28 重新設計**）

- **頁面 layout 走垂直堆疊**（不做左右分欄），詳見 [visual-design.md「列印預覽頁面」](visual-design.md#列印預覽頁面reportspreview2026-05-28-重新設計)
  - 上：mode tabs（單筆 / 批次）+ 緊湊水平表單列
  - 下：滿寬 PDF 預覽（toolbar + iframe 720px 高）
  - **避坑紀錄**：曾嘗試左右分欄 + sticky preview，遇到 (a) `1fr` 欄位被 iframe 撐爆覆蓋表單 (b) sticky + 100vh 高度造成「蓋住左欄」視覺；改垂直堆疊一次解決
- **預覽**：後端產生 PDF bytes → 前端用瀏覽器內建 PDF viewer 嵌入 `<iframe>`（暫未引入 PDF.js 套件；如需註解 / 縮放功能再加）
  - 工具列四按鈕：**列印**（`PrintService.printBlob`）/ **新分頁開啟**（`window.open`）/ **下載**（`<a download>`） / **關閉**（清掉 blob URL + 回空狀態）
  - ⚠️ iframe 預覽依賴 Chromium 內建 PDF viewer → packaged Electron 必須 `webPreferences.plugins: true`，否則整片空白（見 [gotchas.md](../gotchas.md)）
- **空狀態**：📄 + 「尚未產生 PDF」+「請從上方選擇報表類型並送出」
- **另存 PDF**：目前用瀏覽器下載；上 Electron 後再切 `dialog.showSaveDialog`
- **批次列印（多筆）**：後端合併 PDF 一次回傳（含 `X-Signup-Count` header），前端載入大 PDF

### 送印路徑（**2026-07-31 改為 Electron 列印通道**）

`core/print/print.service.ts` 是**唯一列印入口**，四個呼叫點（右鍵單筆 / 右鍵多筆 / 編號批次 / 新增後列印）
與報表預覽頁都走它。內部以 `isElectron()` 分流：

| 環境 | 行為 |
|---|---|
| Electron | `shared/print-dialog/` 自建對話框（選印表機 / 份數 / 縮放，紙張唯讀）→ IPC 交給主行程用指定紙張、邊界 0、100% 送印 |
| 瀏覽器（`ng serve` / 單元測試） | 退回既有 `openPdfInNewTab`，**行為與改版前完全相同** |

- 為什麼不用系統列印對話框：Electron `print({silent:false})` 帶不進預設值（見 [gotchas.md](../gotchas.md)）。
- `BatchPrintService.run()` 新增 `takeFile?: boolean`（**預設 `true`**，既有行為不變）。Electron 路徑傳 `false`：
  進度 overlay 與取消仍在前端，但成品由主行程串流取檔（`/file` 是 one-shot，兩邊都取會失敗；大檔走 IPC 會 OOM）。
- 完整契約見 [print-channel-electron.md](../blueprints/print-channel-electron.md)。
- 列印格式對話（PDF / 預覽）：使用 shared/dialog 元件呈現兩個 radio + 確認 / 取消（**目前已捨棄**，API 統一回 PDF，前端 iframe 處理；保留欄位給未來如需區分 watermark）

舊 19 個 RDLC 模板**不直接搬**，由後端 QuestPDF 重畫；版面驗收見 [printing-reports blueprint](../blueprints/printing-reports.md)。

## CRUD 頁面排版模式（**2026-05-28.e 全系統統一改 Form Overlay**）

所有「新增 / 編輯」表單一律走 **single-column 列表 + full-screen overlay**：

```
[頁標題 + 主要動作鈕]
[搜尋條件列（flex wrap）]
[結果列表 / 表格 ── 滿寬]
                    ↓ 點「新增 / 編輯」
[全螢幕 overlay：backdrop + 置中 panel，panel 寬高 content-adaptive]
```

對齊本規範的 feature：
- [signup-list-page](../../frontend/src/app/features/signups/signup-list-page.ts) → `<app-form-overlay>` 包 `<app-signup-edit-form>`（2026-05-29：`signup-edit-form` 單頁**欄位編排對齊舊 NewSignupForm**：法會資料→信眾→基本資料→地址→名單→編號/費用→備註/預繳；地址用城市→區域連動下拉，資料源 `ZipcodeApi`（`GET /zipcodes/cities`、`?city=`）+ 同寄件地址 checkbox；2026-06-23：新增模式法會分類**依當月自動帶季別 root**（`util/ceremony-season.ts` + `applySeasonDefault()`，可編輯預設，1-4春季/5-8中元/9-12秋季）；見 [signup-management.md 新增段](../blueprints/signup-management.md)；**2026-07-04：改雙欄密集排版節省高度**——`.form-cols` 左欄（法會資料→信眾→基本資料）／右欄（地址→陽上/往生名單→編號/費用/備註/預繳合併一個 fieldset），邏輯順序不變、僅視覺分欄，DOM 順序＝tab 順序（左欄再右欄）；地址寄件/文牒並排半欄、陽上/往生名單並排；重複報名警示文案縮短以免窄欄換行。實測（Playwright + `sa@system.local` 帳號）：1366×768 以上（含選信眾+重複報名警示的常見情境）**完全無垂直捲動**；僅 1280×720 以下極小視窗、且有重複報名警示時會有 ~20px 輕微捲動；**2026-07-17：對齊舊系統四項改版（使用者指定）**——(1) 信眾搜尋由 modal picker 改**常駐 in-form**：搜尋列+結果表格直接放表單頂部全寬 fieldset（對齊舊 txtQ+dgvBelievers 常駐面板），選定後列表保留、選定列高亮、可隨時再點改選；結果表內捲＋**靜默截斷最多 render 前 200 列**（模糊字命中 2 萬+ 列會卡死 DOM；「符合 N 筆」截斷提示與「未選信眾」提示 2026-07-17 三輪後使用者指定拿掉）；編輯模式只顯示信眾摘要卡不顯示搜尋。(2) 地址改回**寄件上/文牒下**上下堆疊（對齊舊 Designer Y=222/311），「同寄件地址」checkbox 併入文牒地址同列省一列高。(3) 名單改**往生上/陽上下**上下堆疊（對齊舊 Designer Y=401/517），且**往生輸入框不再套 `--c-dead-name-bg` 底色**（舊系統兩組皆無 BackColor；搜尋結果表格與各列表頁的往生「欄」底色不受影響）。(4) 未選信眾送出→前端先 `POST /believers` 自動建新信眾再 `POST /signups`（believerId 解除 required；對齊舊 btnConfirm 自動建信眾，見 [signup-management.md](../blueprints/signup-management.md)）。**同日第二輪（使用者追加）**：(5) 結果列表配色/列高對齊報名維護 grid——沿用全域 `.data-table.dense` token（選取列 `--c-row-selected`、往生欄/選取混色與 vgrid 同值，Playwright computed-color 比對逐項相同），cell padding 2px 6px（列高 25px ≈ vgrid 26px）、表格 `max-height:140px`（**2026-07-27 縮為表頭+4 列＝`25px × 5`（同日先訂 3 列後改 4），並把 cell `line-height` 固定成 `25px - padding×2` 讓列高可算**）；(6) **法會資料 fieldset 提到表單最上方**（全寬，高於信眾搜尋；**2026-07-27 再改為表單左側直立窄欄 176px**，回到舊 `plStep1` 175×470 面板的版面——`.form-shell` 兩欄 grid，左法會資料/右主體，≤900px 收回上下堆疊），為補回全寬區塊增加的高度：表單內 gap/padding 全面收緊（6px 系）、備註 textarea rows=1、費用/預繳年/預繳法會併一列三欄。**同日第三/四輪（使用者追加）**：(7) 清單框線統一入全站規範（見 [visual-design.md「清單/資料格配色規範」](visual-design.md)，`.data-table.dense` 補直向格線與 vgrid 一一對應）；(8) **無 row hover 變色**（vgrid 本無 hover，dense 顯式蓋掉基底 `.data-table` 的 row-alt hover）；(9) 移除「未選信眾…自動建立」與「符合 N 筆僅顯示前 200」兩行提示（自動建信眾與 200 列截斷**行為保留**，只拿掉文字）。實測（Playwright，1366×768）：空表單**完全無垂直捲動**（582=582）；展開常駐結果列表＋重複警示時 overlay 內可捲動（常駐列表固有代價，使用者指定要列表直接顯示）；**2026-07-28 版面客訴四項**：(a) 信眾搜尋框 `.search-input` 補 `font-size: var(--font-size-base)` 對齊地址輸入框——它不在 `.field` 內，原本只繼承 `font-family`，字級落回 UA 預設 13.33px 才偏小（同 2026-07-21 名單/結果表的老坑）；(b) 往生/陽上名單兩個 fieldset 由右欄搬到**左欄「地址」下方**（左欄＝基本資料→地址→往生→陽上，右欄只剩「編號/費用/備註/預繳」）；(c)「同寄件地址」由文牒地址同列移到文牒**郵遞區號正上方**＝文牒 grid 第一列第三欄，郵遞區號改與文牒地址同列（`.span-2` + `.zip`），零額外列高；(d) 備註 textarea `rows=1`→`rows=4`（全域 `.field textarea` 是 `height:auto; min-height:60px`，rows 直接生效））
- [signup-list-page 右鍵「在此前插入」](../../frontend/src/app/features/signups/signup-list-page.ts)（**2026-07-04 新增**）：報名維護列表 row context menu 加「在此前插入」（`actionInsertBefore`，icon `insert-above`），開 `signup-edit-form` 插入模式——`insertAt` input 帶目標群組 + 插入位置編號，套用後鎖定年/法會/類型 + `keepNumber`、預填 `customNumber`，`submit()` 改呼叫 `SignupApi.insertShift()`（`POST /signups/insert-shift`，後續編號 +1 順移）。見 [signup-management.md](../blueprints/signup-management.md)「插入並順移」、[post-signups-insert-shift.md](../blueprints/api-endpoints/post-signups-insert-shift.md)
- [believers-page](../../frontend/src/app/features/believers/believers-page.ts) → `<app-form-overlay>` 包 `<app-believer-edit-form>`（**2026-07-04：改雙欄密集排版節省高度**，比照 signup-edit-form 的 `.form-cols` 手法——左欄「基本資料（類型/姓名/堂號/電話/固定編號）→ 地址」／右欄「名單」；邏輯欄位與驗證不變、僅視覺分欄，DOM 順序＝tab 順序（左欄再右欄）。實測（Playwright + `sa@system.local`）：1280×720／1366×768／1440×900／1920×1080 新增信眾 overlay **完全無垂直/水平捲動**（panel 高固定 ~490px）。**2026-07-18 客訴改版（對齊 signup-edit-form 2026-07-17 版）**：(1) 地址改**寄件上/文牒下**上下堆疊（`.addr-cols` 由並排半欄改單欄）；(2) 名單改**往生上/陽上下**，且**往生輸入框不再套 `--c-dead-name-bg` 底色**（列表頁往生「欄」底色不受影響）；(3) 陽上/往生名單 legend 拿掉「（最多 6 位）」字樣——**signup-edit-form 同步拿掉**，6 格上限行為不變只移除文字）
- [prepay-page](../../frontend/src/app/features/prepay/prepay-page.ts)（**2026-07-04 UI 對齊舊 LoadPrepayForm**）：法會下拉**只列根法會**（`ParentID==null` 依 Sort，不攤平子法會）；年份改**受限下拉**（來源=本年往前 5 年、目標=本年+明年，用 `[ngValue]` 保留 number 型別）；信眾分組標籤用舊詞序（一般非員工／一般地藏殿員工／郵撥大殿員工／郵撥非員工，見 `util/prepay-groups.ts`）；載入前加 `confirm("是否載入…?")` 二次確認。結果改用 KPI 卡（loaded/skipped/固定/非固定/延展/補號）為刻意保留的增強，不退回舊 MessageBox。見 [prepay-loading.md](../blueprints/prepay-loading.md)
- [categories-page](../../frontend/src/app/features/categories/categories-page.ts) → `<app-form-overlay>` 包 `<app-category-edit-form>`
- [admins-page](../../frontend/src/app/features/admins/admins-page.ts) → `<app-form-overlay>` 包 `<app-admin-edit-form>`

理由：
- **視覺一致**：四個 feature 用同一 shell（標題列、× 關閉、ESC、backdrop click；報名維護以 `[dismissible]="false"` 關掉後兩者），使用者只需學一套互動
- **空間有彈性**：`min-width: min(420px, 92vw); max-width: 92vw; max-height: 92vh`，2 欄 form 自動縮成小窗、25 欄 form 自動撐大
- **列表狀態保留**：開 overlay 時列表不切換、不重 mount、不重打 API；存檔成功後 inline refresh
- **route 仍可 deep link**：[signup-edit-page](../../frontend/src/app/features/signups/signup-edit-page.ts) 保留 `/signups/new`、`/signups/:id/edit` 作獨立頁面 fallback（內部仍重用 `<app-signup-edit-form>`）

### `<app-form-overlay>` 共用 shell

API（[shared/form-overlay/form-overlay.component.ts](../../frontend/src/app/shared/form-overlay/form-overlay.component.ts)）：

```typescript
@Component({ selector: 'app-form-overlay', ... })
export class FormOverlayComponent {
  readonly title = input.required<string>();
  readonly dirty = input<boolean>(false);   // 關閉前是否需要「未儲存變更」確認
  readonly width = input<string | null>(null); // panel 寬度（如 '900px'）；不給＝content-adaptive
  readonly showActions = input<boolean>(true); // 是否顯示 panel 底部 actions footer
  readonly dismissible = input<boolean>(true); // false＝backdrop / Esc 不關，只剩 × 與表單自己的取消
  readonly close = output<void>();          // ESC / × / backdrop click 三路皆觸發（dismissible=false 時只剩 ×）
}
```

**`dismissible=false` 何時用（2026-07-28）**：表單長、誤關代價高的場合——報名維護的「新增報名 / 編輯報名」overlay（使用者指定：**只有點 × 或取消才關**）。輸入中手滑點到 panel 以外、或按 Esc 想取消輸入法組字，都會整張表單收掉；新增報名雖有跨路由草稿保護（見下方段落），編輯模式沒有。實作是 backdrop click 與 `document:keydown.escape` 兩個入口各自 early-return，**不改 `tryClose()`**，× 與 host 的取消鈕仍走同一條路（dirty 時照樣跳確認）。

**`showActions=false` 何時用（2026-07-28）**：按鈕列改由內層表單自己排版時關掉 footer——報名表單把「列印資料卡/取消/確認」移到**備註下方**（使用者指定），host 改用 `<ng-container form-actions>` 投影進 `app-signup-edit-form` 的 `.form-actions-slot`。footer 若留著會是一條只有 padding + 上框線的空灰帶（不能靠 `:empty` 賭 Angular 的註解錨點/空白節點，故走顯式 input）。**按鈕仍由 host 提供**（handler 與 disabled 條件都在 host，投影內容沿用 host 的 template context 與樣式封裝），表單只出借位置。路由頁 `/signups/new` 走同一個 slot（原本頁面最下方的 `.form-actions` 已移除）。

### 報名表單版面（2026-07-29 客訴第三輪）

承 2026-07-04 的 `.form-cols` 雙欄與 2026-07-28 的按鈕內移，使用者再指定四項（`signup-edit-form` + 共用樣式）：

1. **基本資料排成一整列、且移出左欄改為 `.form-cols` 上方的全寬列**。五欄擠在半寬左欄會被壓爛，故整塊上提；副作用即使用者要的「編號/費用/備註/預繳往下推」——右欄從基本資料下方才開始。左欄現為 地址 → 往生 → 陽上。（欄序與寬度於 2026-07-31 再調，見下一節。）
2. **按鈕列（列印資料卡/取消/確認）改靠左**：`.form-actions-slot` 由 `justify-content: flex-end` 改 `flex-start`，並補 `padding-left: var(--space-sm)` 讓最左的按鈕**切齊正上方的「備註」欄左緣**（使用者指定）——備註在 `.bare-block` 內、左緣＝區塊的 `padding-left`，而按鈕列是該區塊的兄弟節點不吃那份 padding，不補就會凸出 8px。
3. **五個區塊全部拿掉外框與 legend**（先是地址，同日追加 基本資料 / 往生名單 / 陽上名單 / 編號·費用·備註·預繳）：`<fieldset class="block"><legend>X</legend>` 一律改為 `<div class="bare-block">`（`padding: 2px var(--space-sm) 4px`——左右沿用 `.block` 值讓欄位左緣仍與有外框的區塊對齊，上下留白補上框線消失後的分段感）。
   ⚠ **此項已於 2026-07-31 部分反轉**：地址與兩組名單依使用者指定改回有框（見下一節），只有「基本資料」與「編號·費用·備註·預繳」仍是 `.bare-block`。
4. **「確認」鈕加寬一倍**：新增全域 `.btn-wide { min-width: 112px }`（兩字鈕自然寬 ≈ 12+2×16+12 = 56px，×2）。用 `min-width` 不用 `width`，字多的按鈕仍能自然撐開。兩個 host（`signup-edit-page.html`、`signup-list-page.html` 的投影 slot）各自加 class——按鈕由 host 提供，表單元件的 scoped 樣式吃不到投影內容，故走全域 utility 而非 `.form-actions-slot` 內的後代選擇器。

同批還有「新增報名成功」提示的字級/按鈕（見上方 ConfirmDialog `emphasis` 段）。`ng build` 綠、`ng test` 28 passed；**版面未實機複驗**。

### 報名表單版面（2026-07-31 客訴第四輪）

上一輪把外框全拿掉之後，使用者回報的其實是「**框要回來、欄位標題要拿掉**」——分段資訊該由框的 legend 承擔，每個欄位再標一次「寄件/文牒」反而是噪音。四項版面調整（`signup-edit-form`，其中 1、2 同步套到 `believer-edit-form`）：

1. **地址拆成「寄件地址」「文牒地址」兩個 `fieldset.block`，框內欄位標題全部拿掉改 placeholder**。城市/區域下拉沿用既有空值選項（「請選擇城市」「請選擇區域」）當提示，郵遞區號 `placeholder="郵遞區號"`（原為 `—`）、地址欄 `placeholder="寄件地址" / "文牒地址"`。`.field` 是 grid，少掉 `<span>` 只剩控件，不會破版。**「同寄件地址」勾選框移進文牒框內最上方**（靠右對齊沿用 2026-07-28 的決策）——它決定的是文牒段的內容，放進文牒框比夾在兩段之間更貼語意；`.same-mail-row` 的 `margin-top` 改 `margin-bottom`。兩個 fieldset 之間的間距由 `.col` 的 grid gap 提供，`.addr-text { margin-top }` 移除。
2. **往生名單 / 陽上名單各自改回 `fieldset.block` + legend**（用詞對齊信眾表單）。這正好解掉上一輪記錄的已知取捨——填入姓名後不再看不出哪組是往生。
3. **基本資料改序為 堂號 → 姓名 → 電話 → 員工類型 → 固定編號，且前三欄的總寬度＝下方地址框寬度**。作法是**與 `.form-cols` 同構**而非寫死欄寬：基本資料那列改用 `.form-cols > .col > .bare-block > .grid`，左半放 `.grid.three`（堂號/姓名/電話）、右半放新的 `.grid.basic-side`（員工類型 + 固定編號勾選框）。左半與下方地址框走完全相同的巢狀，寬度自然逐像素相等，日後改 gap / padding 也不會走鐘。原 `.grid.basic-row` 移除。
   > 寫死欄寬（例如硬湊 `repeat(3, X)`）在這裡是錯解：地址框寬度是 `(容器寬 − gap) / 2` 再扣區塊 padding，任何一個 token 改動都會讓兩者對不齊。
4. **員工類型下拉寬度＝下方右欄「費用」欄寬**（同日後續指定）。同樣走「同構」而非寫死：費用位在 `.form-cols > .col > .bare-block > .grid.three` 的一格，`.grid.basic-side` 就改用同一組 `repeat(3, minmax(0, 1fr))`，員工類型佔第一格 → 兩者逐像素相等；固定編號勾選框改 `grid-column: 2 / -1` 吃剩下兩格（原本的 `auto` 讓下拉去撐剩餘寬度，必然對不齊）。斷點同步：760px 時 `.basic-side` 跟著 `.three` 降成兩欄；480px 單欄時要把 `.field-check` 的 `grid-column` 還原成 `auto`，否則 `2 / -1` 會撐出隱含欄。

`ng build` 0 warning、`ng test` 31 passed（新增 3 案回歸鎖，見 [signup-management.md](../blueprints/signup-management.md)）；**版面未實機複驗**。

**`width` 何時要給（2026-07-27）**：panel 預設「有多寬長多寬，上限 92vw」，內容只要有寬表格就會把整個視窗撐滿（報名維護的新增/編輯 overlay 被 19 欄的信眾搜尋結果表撐到 92vw → 客訴「彈跳視窗太寬」）。這種情況給 panel 一個定寬（報名維護用 `width="1100px"`，刻意與 `/signups/new` 路由頁 `.page { max-width: 1100px }` 同值，讓 overlay 與整頁版本一樣寬），寬表格改在自己的 `overflow: auto` 容器內橫向捲動。
**不要改成限制 overlay 內的表單元件**：panel 仍會被表格撐寬，結果表單縮了、panel 沒縮，底部 actions 會落單在右下角。

template 樣板：
```html
<app-form-overlay [title]="..." [dirty]="dirty()" (close)="onClose()">
  <app-*-edit-form ... (saved)="onSaved()" (dirtyChange)="onDirtyChange($event)" />
  <ng-container overlay-actions>
    <button class="btn" (click)="onClose()">取消</button>
    <button class="btn btn-primary" (click)="onSubmit()">確認</button>
  </ng-container>
</app-form-overlay>
```

### `<app-progress-overlay>` 共用 shell（**2026-07-28**）

阻擋畫面的置中進度 overlay，目前用於批次列印。與 `ConfirmDialogService` 同一套 CDK Overlay pattern
（`overlay.create({hasBackdrop:false, scrollStrategy: block(), positionStrategy: global()})` + `ComponentPortal`
+ `setInput` + `dispose`），元件為 inline template/styles 的 standalone `OnPush` 元件。
視覺規格（z-index 層級、色票、尺寸）見 [visual-design.md「進度 Overlay」](visual-design.md)。

```typescript
// shared/progress-overlay/progress-overlay.service.ts
open(config: ProgressOverlayConfig): ProgressOverlayHandle
// ProgressOverlayConfig: { title, detail?, total, completed, note?, cancelable?, cancelLabel? }
// ProgressOverlayHandle: { update(patch), canceled: Promise<void>, close() }
```

兩個關鍵設計：

- **`update()` 必須傳新物件**：`ref.setInput('config', {...current, ...patch})`。就地改 mutable 物件
  在 OnPush 下不會觸發更新（見 [gotchas.md](../gotchas.md)）。
- **按下取消時 service 不自動關閉 overlay**：它先把自己切成 `cancelable:false` + `note:'取消中…'`
  並 resolve `canceled`，由呼叫端真的把後端工作停掉之後才 `close()`。否則會出現「畫面關了但伺服器
  還在燒 CPU」。也刻意**不做 backdrop 點擊關閉**——這裡擋的是長時間工作，誤觸中斷的代價比關不掉高。

### `BatchPrintService`（批次列印 orchestration，**2026-07-28**）

[core/reports/batch-print.service.ts](../../frontend/src/app/core/reports/batch-print.service.ts)：
`run(req, {title?, detail?}) → Promise<ReportPdf | null>`（`null` ＝使用者取消；丟 `ApiError` ＝真失敗）。
內部流程：`createBatchJob` → 開 overlay → 每 250ms 輪詢（`await sleep()` 串接的 `while`，
**不用 `setInterval`** 以免請求疊加）→ `completed` 就取檔、`canceled` 回 null、`failed` 丟 `ApiError`。
筆數跑滿但狀態仍 running 時顯示「合併 PDF…」（伺服端正在 merge），如此不必在 API 多加 `phase` 欄位。

放 `core/` 而非某個 feature：signups 與 reports 兩個 feature 都用它，放任一邊會造成跨 feature 相依；
放 `shared/` 又不符「shared ＝無業務語意的 UI 元件/工具」的現況慣例。

三個呼叫點（`signup-list-page` 的 `printSelected` / `printBatch`、`reports-preview-page` 的
`generateBatch`）各自的 `printing()` / `loading()` signal 與 try/catch **保留**（仍驅動按鈕 disabled
與「列印中…」文案），只是把 API 呼叫換成 `batchPrint.run(...)` 並多一行 `if (!resp) return;`。
**單筆列印不走這條**（很快，不需要進度條）。`ReportApi.batch()` 已移除（後端同步版仍保留，見
[api-design.md](api-design.md)）。

### 「Enter 就送出」的開關是那顆隱藏 submit 按鈕（**2026-07-28**）

各 edit-form 的 `<form>` 底部都有一顆 `<button type="submit" hidden>`——它不是裝飾，是 HTML **隱含送出**的必要條件：表單內有 submit 按鈕時，任一單行輸入框按 Enter 就會觸發 `submit` → `(ngSubmit)`。要關掉 Enter 送出就是拿掉那顆按鈕（`(ngSubmit)` 一併移除，別留永遠不觸發的 handler）。

按鈕列若**投影在 `<form>` 內**（報名表單 2026-07-28 起如此），還要多一道：焦點停在「確認」上按 Enter 會直接啟動原生 button（keydown 的預設動作＝click），故在 form 上攔 Enter：

```typescript
protected onFormKeydown(event: KeyboardEvent): void {
  if (event.key !== 'Enter') return;
  if ((event.target as HTMLElement | null)?.tagName === 'TEXTAREA') return; // 備註要能換行
  event.preventDefault();
}
```

**目前只有 signup-edit-form 這樣做**（使用者指定「確認只能用滑鼠點」）；admins / believers / categories 維持隱含送出，勿無差別套用。詳見 [gotchas.md](../gotchas.md)。

### `.field` 外自刻的 form control 要補齊 color/background（**2026-07-27**）

全域只有 `.field input, select, textarea` 設 `color: var(--c-text-primary)` / `background: var(--c-surface)`。在 `.field` 之外自刻樣式的輸入框（如 `.names .name-grid input`）會落回 UA 樣式表的 `color: fieldtext`（純黑）與 `background: field`——**那是直接設在元素上的宣告，不會繼承 body**，所以只補 `font-size` 會出現「這區的字比較黑」的客訴。自刻時請整組對齊：height / padding / border / radius / font-family / font-size / **color / background**。詳見 [gotchas.md](../gotchas.md)。

### 搜尋條件的跨路由快照（**2026-07-27 起**，2026-07-31 補齊）

清單頁的搜尋條件 / 結果 / 選取列存在 root singleton [`SignupSearchState`](../../frontend/src/app/features/signups/signup-search-state.ts)，元件 `ngOnInit()` 還原。**只存記憶體**（同草稿，不落 localStorage）：離開列表頁——不論是進 edit / logs，還是從側欄切到別的功能——回來都保持原樣，重開 App 才回預設。

**兩個原本會漏掉的破口（2026-07-31 客訴「切走再回來，勾的『範圍』和『顯示完整表格』被取消」）**：

- **快照只在 `search()` 存 → 沒按搜尋的條件變動全丟**。使用者常是「勾了條件、還沒按搜尋就切去別的功能」，回來看到的是**上次搜尋當下**的條件而不是離開前的畫面。修法：`bindFormSnapshot()` 訂閱 `form.valueChanges` 無條件寫回 `state.form`，結果/總數/選取仍只在 `search()` 一併存。⚠ 前提是**還原路徑上的 `patchValue` / `enable` / `disable` 一律帶 `emitEvent: false`**（本頁三處都已是），否則還原會反寫覆蓋自己
- **純 UI 狀態不在表單裡就不會被存**。`showAll`（顯示完整表格）是 signal 不是 form control，快照介面自然漏掉它 → 另開 `state.showAll` 一格，在 `toggleShowAll()` 直接同步（刻意不用 effect：effect 要等變更偵測才跑，測試裡不 `detectChanges` 就抓不到）

一般原則：**元件的「還原範圍」要以使用者看得到的畫面為準**，不是以「送給後端的查詢」為準——凡是使用者手動撥過的開關（含純顯示用的），都要進 singleton。

### 未完成表單的跨路由草稿（**2026-07-27**）

overlay 的 backdrop 是 `position: fixed; inset: 0`（蓋住側欄），所以「表單開著時切到其他功能頁」在實際操作上一定會先關掉 overlay、銷毀表單元件。若該表單填到一半，內容就沒了。

**目前只有「新增報名」做草稿**（使用者客訴驅動，其他 feature 未要求，勿無差別套用）：

```typescript
@Injectable({ providedIn: 'root' })   // features/signups/signup-draft-state.ts
export class SignupDraftState {
  readonly draft = signal<SignupDraft | null>(null);
  save(d: SignupDraft): void; clear(): void;
}
```

- **存**：`destroyRef.onDestroy(() => this.saveDraft())`，條件為「純新增模式 **且** `form.dirty`」（乾淨表單不覆蓋既有草稿）。⚠ `patchValue` **不會**標髒——凡是「程式填值但算使用者實質輸入」的路徑（如 `pickBeliever` 選信眾）都要自行 `markAsDirty()`，否則會被這個條件靜靜擋掉
- **還原**：`ngOnInit()`（inputs 此時已就緒，早於 `loadCategories()` 回來的 `applySeasonDefault()`，而後者「已有值就不覆蓋」，故不會蓋掉草稿的法會）→ `patchValue` + 重跑 `applyAddress` 連動把城市/區域下拉選項補回來 → `markAsDirty()` + `dirtyChange.emit(true)` 讓 host 狀態一致
- **只存記憶體**（比照 `SignupSearchState`，不落 localStorage）：等同舊 WinForm「Form 還開著」，重開系統即消失
- **靜默還原**：不顯示提示列，清空走既有「取消」鈕
- **只做純新增模式**：帶 `signupId` / `fromSignupId` / `insertAt` 的模式各有自己的資料來源，還原草稿會互相打架
- **草稿模式關閉 overlay 不跳 dirty 確認**：`[dirty]="editFormDirty() && overlayGuardsDirty()"`——資料會保留，攔人與實際行為矛盾；其餘模式維持原確認
- 詳細行為與取捨見 [signup-management.md 新增段](../blueprints/signup-management.md)；回歸測試 `signup-edit-form.draft.spec.ts`（vitest，`npx ng test --watch=false`）

### ConfirmDialog 單按鈕（result dialog）變體（**2026-05-29**）

`ConfirmDialogConfig` 加 `hideCancel?: boolean`；設為 `true` 時 `confirm-dialog.component.ts` 隱藏「取消」按鈕，整個 dialog 退化為**單一「確定」結果視窗**（純通知，無二選一）。首個使用者：`backup-page` 備份成功後顯示 fileName / fullPath / size 的結果 dialog（沿用既有 ConfirmDialog 不另造元件）。

### ConfirmDialog 強調變體 `emphasis`（**2026-07-29**）

`ConfirmDialogConfig` 加 `emphasis?: boolean`：訊息字級 **20px**（`.confirm-dialog.is-emphasis .confirm-body`，蓋掉預設的 `--font-size-md`）+ 確認鈕套全域 `.btn-wide`（寬度加倍）。使用者指定，首個使用者：報名表單「新增報名成功（編號X）」——那行編號是使用者接著要抄到單據上的資訊，要一眼看到、確定鈕要好按。

**為什麼是旗標而不是直接改 `.confirm-body`**：2026-07-28 已因使用者要求把**全站** dialog 字級提到 `--font-size-md`；這次只有「結果提示」要再大，一般二選一確認框維持原字級，故走 per-call 旗標。判準：只給「成敗結果」型提示用，不要順手套到所有 dialog。

### Feature edit form 共用 pattern

每個 feature 一個 `*-edit-form` standalone component：
- inputs：目標物件或 ID（如 `believer: BelieverListItem | null`）
- outputs：`saved` / `dirtyChange`
- 公開 `submit()` 方法給 overlay action 觸發
- 內部負責 form 建構、validation、API 呼叫、錯誤訊息顯示

list page 透過 `@ViewChild` 抓 form ref，overlay 的「確認」按鈕呼叫 `formRef.submit()`；form `(saved)` 觸發後 list page 關 overlay + reload。

### 已 deprecate 的 pattern

下列 pattern **不再使用**（未來新增 feature 不要走這些）：
- ❌ Route navigation 做 edit page（除非作 deep link fallback）
- ❌ Side sheet（`.sheet-*`，仍保留 global class 以防其他用途）
- ❌ Split layout 左列表右編輯
- ❌ Inline expandable card

共用 class：[styles.scss](../../frontend/src/styles.scss) 提供 `.overlay-*`（form overlay）、`.dense-controls`、`.vgrid-*`（virtual grid）、`.pane`、`.kebab-btn`、`.data-table.dense`、`.num-stepper`（數字微調控件）等 reusable primitive。

`.num-stepper`（**2026-07-21 新增**）：`<input appNumericInput>` 右側掛垂直 ▲▼ 兩鈕做 ±1（下限 1），對齊舊系統 `NumericUpDown`（SignupForm `nudSearchNumber`）。用於報名維護「編號」搜尋欄與「批次列印」起迄欄——**2026-07-31 起兩處都是起~迄兩格**（搜尋欄外層 `.number-field` 讓兩個 stepper `flex:1 1 0` 均分欄寬、隨 RWD 斷點一起縮，不寫死寬度）。按鈕 `tabindex="-1"` 不搶 Tab 焦點；實際 ±1 由頁面 `stepNumber(control, delta)` 對 reactive form control `setValue` 完成（不另做 ControlValueAccessor，維持 `appNumericInput` 既有數字清洗/IME 行為）。

實作參考：[believers-page](../../frontend/src/app/features/believers/believers-page.ts)（2026-05-29 起為 vgrid 全欄位清單 + 右鍵 context menu + single-form overlay；欄位定義抽至 [believer-columns.ts](../../frontend/src/app/features/believers/believer-columns.ts) 對齊舊 dgvBelievers）；[signup-list-page](../../frontend/src/app/features/signups/signup-list-page.ts) 最複雜（25 欄 + picker + virtual scroll list + 欄寬持久化 + 多選 + 3 entry points）。

## 響應式策略（RWD）

| 視窗區間 | 策略 |
|---|---|
| ≥ 1200px | 完整桌面布局，所有欄位 / sheet 並列 |
| 1000–1200px | 隱藏非關鍵欄位（`hide-lg`）|
| 700–1000px | 進一步隱藏（`hide-md`）；搜尋條件 wrap |
| < 700px | 再隱（`hide-sm`）；sheet 改滿螢幕 |
| < 600px | sheet 內表單 2→1 欄、名單 3→2 欄 |

斷點靠 **CSS 控制**、**不用 ViewportRuler**；class 名稱遵循 `hide-{breakpoint}` 慣例（在該斷點以下隱藏）。

**媒體查詢 vs 容器查詢（2026-07-31）**：頁面內容區的可用寬 = 視窗 − 側欄（220px／收合 64px）− `.content` padding，所以**只要斷點是為了「內容排不下」而設的，一律用 `@container`**，`@media` 只留給真正跟視窗有關的情境。已改用容器查詢的：報名維護 toolbar（`.toolbar` 自己當 container，斷點 1100 / 700，規格見 [visual-design.md](visual-design.md)）。

> container 只能設在「不含 fixed 定位子孫」的元素上——`container-type` 會讓元素成為 fixed 子孫的 containing block，設在 `.page`／`:host` 會讓 `app-form-overlay` 的 `.overlay-backdrop` 縮在內容區內。詳見 [gotchas.md](../gotchas.md)。

## 側邊選單 active 規則

- `NavItem` interface 含 `exact?: boolean`；template 用 `[routerLinkActiveOptions]="{ exact: item.exact ?? false }"`
- **預設 `false`**（prefix 比對）— 例：`/reports/preview/datacard` 仍會點亮「列印預覽」
- **`/signups` + `/signups/new` 雙雙開 `exact: true`** — 避免在「新增報名」頁時「報名維護」一起被點亮（兩個是並列、不是父子）
- `/signups/:id/edit` 與 `/signups/:id/logs` 走 prefix 比對被排除（編輯與歷程是獨立 context，sidebar 不點亮任一項）

## 側邊選單收合（**2026-06-29 決策**）

側邊選單可收合成「圖示列」以擴大內容區。

- **行為**：`ShellLayout` 的 brand 區右側放收合鈕（`chevron-left` 圖示），點擊在「完整（220px）↔ 圖示列（64px）」間切換；收合時只留圖示、隱藏文字標籤/品牌副標/使用者名稱/版本號
- **狀態持久化**：`collapsed` signal，寫入 localStorage key `ceremony.sidebar.collapsed`（`'1'`/`'0'`），跨會話沿用；localStorage 不可用時 try/catch 靜默降級為不記憶
- **提示**：收合時各 `nav-item` 與登出鈕用原生 `title` 屬性 hover 顯示文字（不另做 tooltip 元件）
- **寬度**：`.shell` 用 `grid-template-columns`，收合切換 `--sidebar-collapsed-width: 64px`；圖示鈕 `chevron-left` 在收合態 `rotate(180deg)` 指向展開方向

## 軟體版本顯示（**2026-06-02 決策**）

介面需顯示軟體版本，方便客戶回報問題時對版。

- **單一來源**：`frontend/package.json` 的 `version`。`environment.ts` / `environment.prod.ts` 以 `import { version } from '../../package.json'` 自動帶入，UI 顯示 `v${version}`，因此版號永遠與 package.json 連動，**不可在 environment 內寫死字串**（2026-06-18 修正：原本寫死 `v2.0.0` 導致 bump package.json 後 UI 不更新）。版本規範見 [conventions.md](../conventions.md)「軟體版本規範」（SemVer，起始 `v2.0.0`）
- **顯示位置**：
  - `ShellLayout` sidebar 頁尾（登出鈕下方，`.version`）— 登入後全系統可見
  - `LoginPage` 卡片底部（`.version`）— 登入前可見
- **樣式**：次要文字、置中、`font-size-xs`，不搶視覺重點
- **發版時**：只 bump `frontend/package.json` 的 `version`,UI 自動連動；另同步 [status.md](../status.md) Current Version
## DataGrid 規格

舊 DataGridView 重點功能在新版 `<app-data-grid>` 元件：

- **Server-side 分頁**：強制；單頁 50 筆，max 200（搭配 [performance.md](performance.md)）
- **Virtual scrolling**：用 `cdk-virtual-scroll-viewport`；單頁載入仍開虛擬以應付未來成長
- **顯隱欄位 toggle**（對應 cbShowAll）：傳入欄定義含 `defaultVisible: boolean`，頂部下拉勾選。**偏好不落 localStorage**（2026-07-29 客訴：報名維護的「顯示完整表格」勾過一次就每次開軟體都是完整表格；日常用的是預設欄位版）——欄寬持久化不受影響。**但要活在記憶體 singleton 裡**（2026-07-31 客訴）：切到其他功能再回來仍保持原樣，只有重開 App 才回預設；「不持久化」指的是不落磁碟，不是「元件銷毀就丟掉」
- **「全部」條件旁路 toggle**（2026-07-27 新增、**2026-07-29 收斂**，報名維護搜尋面板）：勾選＝解除「年份 / 法會 / 類型」三個範圍限制（＋年份的修飾條件「範圍」），**其餘條件照常生效、使用者仍可改仍可按搜尋**；被停用的控制項一律 `disable({ emitEvent: false })` 而**非清空**（值仍在 `getRawValue()`，取消勾選即以原條件重查還原）。實作要點：(1) 用 `formControlName` + `valueChanges` 驅動，切換即重查，不需另按搜尋；(2) 查詢建構函式（`buildQuery()`）單點處理旁路（只把那幾個欄位改成 null / -1），讓搜尋與匯出兩條路徑自動一致，**不要**在各呼叫點各自判斷；(3) 停用時原生控制項自己會變灰，但 `<label>` 文字不會 → 額外掛 `.all-mode` class 把**對應的**條件 label 淡化 `opacity: .5`（別整區淡化，否則仍可用的條件看起來像壞掉）；(4) 停用清單要與其他連動規則不相交——關鍵字欄的啟用只由 scope* 決定（`bindScopeKeyToggle`），就別讓全部模式也去碰它，否則離開模式時得補救「沒勾範圍卻能打關鍵字」的殘留狀態
- **多選 + 右鍵 context menu**：對應舊 cmsSignups（單選/多選不同選單）— 詳見下方 Pattern 段
- **欄位背景色**：DeadName 欄位橙色（`#FFE0C0`）
- **欄寬持久化**：localStorage 記憶
- **垂直捲軸右鍵子選單**（**2026-07-21 新增**，對齊舊 WinForms 原生捲軸選單）：〔捲動到這裡 / 頂端 / 底部 / 上一頁 / 下一頁 / 向上捲動 / 向下捲動〕。**必用自繪捲軸**：原生捲軸的 `contextmenu` 事件不會派送給網頁 JS（Windows Chromium 會顯示自己的原生捲軸選單但攔不到、macOS 又是 0 寬懸浮捲軸），故無法在原生捲軸上攔右鍵——詳見 [gotchas.md](../gotchas.md)。作法：`.has-custom-vscroll` 隱藏原生垂直捲軸（`::-webkit-scrollbar { width:0 }`，因此水平 thumb/track 需自行上色）＋自繪 `.vscroll` / `.vscroll-thumb`，支援左鍵拖曳、點軌道翻頁、滾輪、右鍵開選單；thumb 尺寸/位置由 signal（`scrollTop`/`viewportH` + `results().length*rowHeight`）computed，量測用 `ResizeObserver` + viewport `scroll`。捲動沿用 `ContextMenuService` 選單 + `CdkVirtualScrollViewport.scrollToOffset / measureScrollOffset / getViewportSize`。列本身 `(contextmenu)` 走列選單、捲軸走捲動選單，兩者獨立。
- **空狀態**：「無資料，請重新搜尋！」verbatim
- **載入中**：頂部 progress bar +「搜尋中，請稍後...」文字
- **排序**：白名單欄位才可排序（對齊 backend 索引）

### Grid Context Menu Pattern（**2026-05-28 補規格**）

舊 WinForms `ContextMenuStrip` 在新版以 Angular CDK Overlay 重現，抽出 `<app-context-menu>` 共用元件。

#### 觸發來源（多管齊下，a11y + touch 友善）

| 來源 | 事件 | 行為 |
|---|---|---|
| 滑鼠右鍵 | `contextmenu` event on row | `preventDefault()` + 選中該列 + 開選單在游標位置 |
| 列尾 kebab 按鈕 | `click` | 開選單在按鈕下方 |
| 鍵盤 `Menu` / `Shift+F10` | `keydown` (focus 在 row) | 開選單在 row 左下角 |
| 觸控長按 800ms | `touchstart` + timer | 開選單在 touch 位置 |

#### Menu API（建議 interface）

```ts
interface ContextMenuItem<T> {
  id: string;                          // 'edit' / 'print-tablet' / ...
  label: string;                       // '修改資料'
  icon?: IconName;                     // 'pencil' / 'printer' / ...
  danger?: boolean;                    // 紅色（刪除）
  divider?: boolean;                   // 上方加分隔線
  enabledWhen: (ctx: MenuContext<T>) => boolean | { enabled: false; reason: string };
  onClick: (ctx: MenuContext<T>) => void | Promise<void>;
}

interface MenuContext<T> {
  selectedRows: T[];          // 選中的列
  triggerRow: T;              // 觸發右鍵的列（未必在 selectedRows 內）
  filters: Record<string, unknown>;  // 當前 grid filter（給 SignupType=4 判斷用）
}
```

`enabledWhen` 回傳 `{enabled: false, reason}` 時，item disable + tooltip 顯示原因（vs 直接 hide 整個 item），對齊舊系統「灰掉但仍可見」的回饋。

#### 多選與選列同步規則

- **右鍵點未選列** → 自動選中該列、其他取消（對齊舊 `dgvSignups.Rows[e.RowIndex].Selected = true; ClearSelection`），該列同時成為 shift 範圍的錨點
- **右鍵點已選列**（多選之一）→ 保留現有選取
- **列尾 kebab** 不改變選取狀態
- **header checkbox** 三態：none / partial（indeterminate） / all

#### shift 範圍選取（**2026-07-27 補齊**，對齊舊 DataGridView `MultiSelect` 原生行為）

- **一般點擊**＝toggle 該列（不清掉其他選取，checkbox 清單語意），並把該列設為**錨點**
- **shift + 點擊**＝選取「錨點 ~ 本列」整段
- **錨點在 shift 期間不移動**，且範圍以「錨點成立當下的選取集合」為基準**重算**（存 `anchorSelection`）而非疊加到現有選取。兩個效果：連續 shift 可以**縮小**範圍（`1→5` 後再 shift 點 3 得 `1~3`，不是 `1~5`）；錨點之前既有的選取完整保留
- 選取換掉錨點就失效：新搜尋、`clearSelection()`、header 全選/全不選都要清錨點（index 對不上新資料）
- ⚠ **列首 checkbox 必須綁 `(click)` 而非 `(change)`**：`change` 事件不帶 `shiftKey`，走 `change` 的話從 checkbox 點選永遠吃不到 shift（最自然的多選入口反而失效）。改綁 click 後要 `preventDefault()`，讓勾選狀態一律由選取 signal 經 `[checked]` 決定，避免 DOM 自行翻轉造成不同步；並 `stopPropagation()` 免得列的 `(click)` 再處理一次
- ⚠ **shift + 點擊會觸發瀏覽器的文字範圍選取**（整片反白）。在列的 `(mousedown)` 上 `preventDefault()` 擋掉即可，`click` 階段照常選列（`.vgrid-td` 沒有 `user-select: none`，改用 CSS 全域關掉會連帶讓儲存格文字不能複製）
- 回歸鎖：[signup-list-page.spec.ts](../../frontend/src/app/features/signups/signup-list-page.spec.ts)

#### 報名維護 9 項對應（cmsSignups）

| Item | enabledWhen | icon | danger |
|---|---|---|---|
| 代入新增 | `selected.length === 1` | `plus` | – |
| 修改資料 | `selected.length === 1` | `pencil` | – |
| 列印資料卡 | `selected.length >= 1` | `printer` | – |
| 列印收據 | `selected.length >= 1` | `printer` | – |
| 列印薦牌 | `selected.length >= 1` | `printer` | – |
| 列印文牒 | `selected.length >= 1` | `printer` | – |
| 列印普桌 | `selected.length >= 1 && selected.every(r => r.signupType === 4)` | `printer` | – |
| 刪除資料 | `selected.length >= 1` | `trash` | ✅ |
| 瀏覽歷程 | `selected.length === 1` | `history` | – |

#### 列印多筆策略（v1 暫定）

- 單選 → 呼叫單筆 endpoint（5 種）→ 開新分頁 / iframe
- 多選 → 取選列編號的 `min..max` 區間 → 呼叫 `POST /reports/batch`（**警告 dialog 提示「將印出區間內全部 K 筆，含非選取項」**，使用者確認再送）
- v2 規劃：後端加 `signupIds: Guid[]` 入參的精確 batch 列印

#### 確認 dialog（破壞性動作）

- 刪除：「將刪除 N 筆報名資料，**不可復原**，確定？」→ [取消] [確認刪除]
- 多筆列印（含非選取）：「將列印編號 a–b 共 K 筆（含非選取 M 筆），確定？」→ [取消] [確認列印]

#### 鍵盤導航

- `↑` / `↓` 切 item
- `Enter` 觸發
- `Esc` 關閉
- 第一個字母 jump-to（中文用注音 / 英數鍵）— 暫不做

#### 實作來源參考

`@angular/cdk` `OverlayModule` + `Portal` + `cdkConnectedOverlay`；不引入 Material 完整 `MatMenu`（後者過重且樣式 override 困難）。

## 地址選擇器（shared）

WinForms 中 City→Area→Address 兩層下拉複用率高，獨立元件：

```html
<app-address-picker
  [zipcodes]="zipcodes()"
  [(city)]="form.city"
  [(area)]="form.area"
  [(zipcode)]="form.zipcode"
  [(address)]="form.address"
/>
```

「同寄件地址」勾選邏輯：勾選時複製 mail → text（城市 / 區域 / 地址三者一起）；取消時清空 text；**寄件的城市、區域、地址三者全空**時才阻止勾選並提示「請先填寫寄件地址（城市／區域或地址）」。

> 2026-07-31 起放寬（刻意偏離舊系統，使用者指定）：舊版要求「地址文字欄非空」才肯同步，但地址自 2026-07-21 起已非必填，只選了城市與區域是合法狀態，這時同步城市/區域一樣有意義。兩張表單（`signup-edit-form` / `believer-edit-form`）同步放寬。

## 名單輸入元件（陽上 × 6 / 往生 × 6）

WinForms 6 個 TextBox 重複手動寫；新版抽出 `<app-name-list-input>`：

```html
<app-name-list-input
  label="陽上"
  [labels]="['陽上1', '陽上2', '陽上3', '陽上3-1', '陽上5', '陽上6']"
  [formArray]="livingNamesFormArray"
/>
```

> 注意 label 用「3-1」「5」（避 4 規則延伸至 UI）。

## 國際化

短期僅繁中。元件設計時所有字串走 `$localize` / translate pipe，便於日後加多語。

## 程式碼風格

詳見 [frontend-coding-style.md](frontend-coding-style.md)。重點：
- TypeScript strict mode
- 元件用 OnPush change detection + Signals
- 避免雙向綁定 `[(ngModel)]`，改用 reactive form
- 共享 token 與 design system 集中在 `shared/design`

## 風險

1. **Electron 套裝印表機 API 與 Windows 列印對話有落差** — 需早期驗證雙聯收據對位
2. **標楷體在 macOS/Linux 缺字** — 需 bundle 字型或使用 fallback 鏈
3. **大型 DataGrid 載入舊系統 100k+ 紀錄** — Angular Material table 用 virtual scroll；後端必須分頁
4. **舊 WinForms 像素級版型在不同 DPI 變形** — 用 rem + flex/grid，並提供 100%/125%/150% UI scale 切換
