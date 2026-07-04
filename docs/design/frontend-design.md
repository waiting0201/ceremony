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
keywords: [frontend, 前端, Electron, Angular, Vue, WinForms, 桌面, layout, signal, NgRx, context-menu, 右鍵, 多選, version, 版本]
last_updated: 2026-07-04 (signup-edit-form 改雙欄密集排版節省高度，避免整頁垂直捲動)
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
  - 工具列三按鈕：**新分頁開啟**（`window.open`）/ **下載**（`<a download>`） / **關閉**（清掉 blob URL + 回空狀態）
- **空狀態**：📄 + 「尚未產生 PDF」+「請從上方選擇報表類型並送出」
- **另存 PDF**：目前用瀏覽器下載；上 Electron 後再切 `dialog.showSaveDialog`
- **批次列印（多筆）**：後端合併 PDF 一次回傳（含 `X-Signup-Count` header），前端載入大 PDF
- **快速列印**：對應舊 PrintPreviewDialog 直接送印 — 走 Electron 系統印表機 API（Electron 階段才接）
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
- [signup-list-page](../../frontend/src/app/features/signups/signup-list-page.ts) → `<app-form-overlay>` 包 `<app-signup-edit-form>`（2026-05-29：`signup-edit-form` 單頁**欄位編排對齊舊 NewSignupForm**：法會資料→信眾→基本資料→地址→名單→編號/費用→備註/預繳；地址用城市→區域連動下拉，資料源 `ZipcodeApi`（`GET /zipcodes/cities`、`?city=`）+ 同寄件地址 checkbox；2026-06-23：新增模式法會分類**依當月自動帶季別 root**（`util/ceremony-season.ts` + `applySeasonDefault()`，可編輯預設，1-4春季/5-8中元/9-12秋季）；見 [signup-management.md 新增段](../blueprints/signup-management.md)；**2026-07-04：改雙欄密集排版節省高度**——`.form-cols` 左欄（法會資料→信眾→基本資料）／右欄（地址→陽上/往生名單→編號/費用/備註/預繳合併一個 fieldset），邏輯順序不變、僅視覺分欄，DOM 順序＝tab 順序（左欄再右欄）；地址寄件/文牒並排半欄、陽上/往生名單並排；重複報名警示文案縮短以免窄欄換行。實測（Playwright + `sa@system.local` 帳號）：1366×768 以上（含選信眾+重複報名警示的常見情境）**完全無垂直捲動**；僅 1280×720 以下極小視窗、且有重複報名警示時會有 ~20px 輕微捲動）
- [signup-list-page 右鍵「在此前插入」](../../frontend/src/app/features/signups/signup-list-page.ts)（**2026-07-04 新增**）：報名維護列表 row context menu 加「在此前插入」（`actionInsertBefore`，icon `insert-above`），開 `signup-edit-form` 插入模式——`insertAt` input 帶目標群組 + 插入位置編號，套用後鎖定年/法會/類型 + `keepNumber`、預填 `customNumber`，`submit()` 改呼叫 `SignupApi.insertShift()`（`POST /signups/insert-shift`，後續編號 +1 順移）。見 [signup-management.md](../blueprints/signup-management.md)「插入並順移」、[post-signups-insert-shift.md](../blueprints/api-endpoints/post-signups-insert-shift.md)
- [believers-page](../../frontend/src/app/features/believers/believers-page.ts) → `<app-form-overlay>` 包 `<app-believer-edit-form>`
- [prepay-page](../../frontend/src/app/features/prepay/prepay-page.ts)（**2026-07-04 UI 對齊舊 LoadPrepayForm**）：法會下拉**只列根法會**（`ParentID==null` 依 Sort，不攤平子法會）；年份改**受限下拉**（來源=本年往前 5 年、目標=本年+明年，用 `[ngValue]` 保留 number 型別）；信眾分組標籤用舊詞序（一般非員工／一般地藏殿員工／郵撥大殿員工／郵撥非員工，見 `util/prepay-groups.ts`）；載入前加 `confirm("是否載入…?")` 二次確認。結果改用 KPI 卡（loaded/skipped/固定/非固定/延展/補號）為刻意保留的增強，不退回舊 MessageBox。見 [prepay-loading.md](../blueprints/prepay-loading.md)
- [categories-page](../../frontend/src/app/features/categories/categories-page.ts) → `<app-form-overlay>` 包 `<app-category-edit-form>`
- [admins-page](../../frontend/src/app/features/admins/admins-page.ts) → `<app-form-overlay>` 包 `<app-admin-edit-form>`

理由：
- **視覺一致**：四個 feature 用同一 shell（標題列、× 關閉、ESC、backdrop click），使用者只需學一套互動
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
  readonly close = output<void>();          // ESC / × / backdrop click 三路皆觸發
}
```

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

### ConfirmDialog 單按鈕（result dialog）變體（**2026-05-29**）

`ConfirmDialogConfig` 加 `hideCancel?: boolean`；設為 `true` 時 `confirm-dialog.component.ts` 隱藏「取消」按鈕，整個 dialog 退化為**單一「確定」結果視窗**（純通知，無二選一）。首個使用者：`backup-page` 備份成功後顯示 fileName / fullPath / size 的結果 dialog（沿用既有 ConfirmDialog 不另造元件）。

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

共用 class：[styles.scss](../../frontend/src/styles.scss) 提供 `.overlay-*`（form overlay）、`.dense-controls`、`.vgrid-*`（virtual grid）、`.pane`、`.kebab-btn`、`.data-table.dense` 等 reusable primitive。

實作參考：[believers-page](../../frontend/src/app/features/believers/believers-page.ts)（2026-05-29 起為 vgrid 全欄位清單 + 右鍵 context menu + single-form overlay；欄位定義抽至 [believer-columns.ts](../../frontend/src/app/features/believers/believer-columns.ts) 對齊舊 dgvBelievers）；[signup-list-page](../../frontend/src/app/features/signups/signup-list-page.ts) 最複雜（25 欄 + picker + virtual scroll list + 欄寬持久化 + 多選 + 3 entry points）。

## 響應式策略（RWD）

| 視窗區間 | 策略 |
|---|---|
| ≥ 1200px | 完整桌面布局，所有欄位 / sheet 並列 |
| 1000–1200px | 隱藏非關鍵欄位（`hide-lg`）|
| 700–1000px | 進一步隱藏（`hide-md`）；搜尋條件 wrap |
| < 700px | 再隱（`hide-sm`）；sheet 改滿螢幕 |
| < 600px | sheet 內表單 2→1 欄、名單 3→2 欄 |

斷點靠 CSS media query 控制，**不用 ViewportRuler**；class 名稱遵循 `hide-{breakpoint}` 慣例（在該斷點以下隱藏）。

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
- **顯隱欄位 toggle**（對應 cbShowAll）：傳入欄定義含 `defaultVisible: boolean`，頂部下拉勾選；偏好存 localStorage
- **多選 + 右鍵 context menu**：對應舊 cmsSignups（單選/多選不同選單）— 詳見下方 Pattern 段
- **欄位背景色**：DeadName 欄位橙色（`#FFE0C0`）
- **欄寬持久化**：localStorage 記憶
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

- **右鍵點未選列** → 自動選中該列、其他取消（對齊舊 `dgvSignups.Rows[e.RowIndex].Selected = true; ClearSelection`）
- **右鍵點已選列**（多選之一）→ 保留現有選取
- **列尾 kebab** 不改變選取狀態
- **header checkbox** 三態：none / partial（indeterminate） / all

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

「同寄件地址」勾選邏輯：勾選時複製 mail → text；取消時清空 text；mail 為空時阻止勾選並提示「請先輸入寄件地址」。

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
