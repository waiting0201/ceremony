# Ceremony - 法會報名系統 (Electron + Angular 21)

## 專案概述
將 .NET Framework WinForms 桌面應用程式遷移至 Electron + Angular 21。
- **來源：** `D:\appsystems\Ceremony` (.NET WinForms + EF6, v1.2.8)
- **資料庫：** SQL Server / Ceremony
  - **正式環境 (遠端)：** Server=192.168.1.151, User=sa, Password=twvsjp0205
  - **開發環境 (本機)：** Server=localhost, Windows Authentication (Trusted_Connection)
- **語言：** 繁體中文 UI

## 技術規範

### 前端
- **框架：** Angular 21 (Standalone Components, Signal-based)
- **CSS：** Tailwind CSS 4 (utility-first，禁止手寫 CSS 檔案，除非 Tailwind 無法實現)
- **UI 元件庫：** Angular Material 21
- **UI 主題：** Light 主題 (白底 bg-white, 淺灰 bg-gray-50/100, Activity Bar indigo-700, 狀態列 indigo-600)
- **狀態管理：** Angular Signals + Services
- **路由：** Lazy-loaded feature routes

### 後端 (Electron 主程序)
- **資料庫驅動：** mssql (tedious)
- **查詢建構器：** Knex.js
- **IPC 通訊：** contextBridge + ipcMain/ipcRenderer (禁止在 renderer 直接存取 DB)
- **報表：** RDLC XML 解析 → HTML absolute positioning + Electron printToPDF

### 報表規範
- 報表尺寸和元素位置必須與原 RDLC **完全一致** (cm 精確對應)
- 字型：標楷體 (font-family: '標楷體', 'DFKai-SB')
- 直書中文：CSS `writing-mode: vertical-rl; text-orientation: upright`
- 列印 margin 設為 0

## 架構規範 (Angular 企業級架構)

### 目錄結構
```
src/app/
├── core/          # 全域 singleton — 僅 root 注入，不可被 feature 互相引用
│   ├── services/  # IPC 封裝、認證、通知、錯誤處理
│   ├── guards/    # 路由守衛
│   ├── interceptors/
│   └── models/    # 全域共用 TS 介面/型別
├── shared/        # 可重用元件/指令/管道 — 無狀態、無副作用
│   ├── components/
│   ├── directives/
│   └── pipes/
├── layout/        # App Shell — 主版面、側邊欄、標題列
└── features/      # 功能模組 — 每個模組獨立 lazy-loaded
    └── <feature>/
        ├── <feature>.component.ts      # 頁面元件
        ├── <feature>.service.ts        # Feature-level data service (呼叫 core/ipc.service)
        └── <feature>.routes.ts         # Feature 路由定義
```

### 架構規則
1. **Core** 只能被 `app.config.ts` 注入，feature 模組透過 DI 使用
2. **Shared** 元件必須是純展示型 (Presentational)，透過 `@Input/@Output` 或 Signals 通訊
3. **Feature** 模組之間禁止直接引用，跨模組溝通透過 Core services
4. 每個 Feature 模組使用 `loadChildren` lazy loading
5. 所有元件使用 **Standalone Components** (不使用 NgModule)
6. 優先使用 **Signals** 而非 RxJS Observable (除非需要複雜的串流操作)
7. 表單使用 **Reactive Forms**

### Tailwind CSS 規則
1. 所有樣式優先使用 Tailwind utility classes
2. Angular Material 元件的客製化透過 Tailwind 的 `@apply` 或 CSS 變數覆蓋
3. 深色主題使用 Tailwind `dark:` variant 或直接定義為預設主題
4. 報表列印樣式 (`report-print.css`) 是唯一允許手寫 CSS 的例外
5. 禁止使用 `style` 屬性 inline CSS (報表模板除外)

### 命名規範
- 檔案：kebab-case (`signup-list.component.ts`)
- 類別：PascalCase (`SignupListComponent`)
- 服務：PascalCase + Service 後綴 (`SignupsService`)
- IPC channel：`domain:action` 格式 (`signups:search`, `auth:login`)
- DB 資料表名稱保持原始命名 (PascalCase 複數: `Believers`, `CeremonyCategorys`)

### IPC 通訊模式
```
Angular Component → Feature Service → Core IpcService → preload (contextBridge) → Main Process IPC Handler → Backend Service → Repository → SQL Server
```

### IResult<T> 統一回傳格式
所有 IPC 回傳使用統一格式：
```typescript
interface IResult<T = void> {
  success: boolean;
  message?: string;
  data?: T;
  error?: string;
}
```

## Electron 主程序結構
```
electron/
├── main.ts          # BrowserWindow 建立
├── preload.ts       # contextBridge API 暴露
├── ipc/             # IPC handlers (每個 domain 一個檔案)
├── db/              # connection.ts (mssql pool) + base.repository.ts (Knex CRUD)
├── models/          # TypeScript 介面 (對應 DB schema)
├── services/        # 業務邏輯層 (BaseService + 各 domain service)
└── reports/         # rdlc-parser.ts + report-engine.ts + templates/
```

## 開發指令
```bash
npm run electron:dev    # 開發模式 (Angular dev server + Electron)
npm run build           # 建置 Angular
npm run electron:build  # 打包 Electron (NSIS 安裝程式)
```

## 資料庫
- init-db.sql 在專案根目錄，包含所有 CREATE TABLE / VIEW / FK
- 6 資料表：Admins, Zipcodes, Believers, CeremonyCategorys, Signups, SignupLogs
- 2 Views：BelieverView, SignupView
- 7 外鍵約束
