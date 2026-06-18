---
title: Infrastructure
purpose: 部署架構、環境變數、CI/CD 流程；桌面 Electron + 本機/區網 ASP.NET Core API + MSSQL
applicable_when: 要規劃部署、要新增環境變數、要設定 CI/CD、要研究觀測
related_agents:
  - backend-engineer
related_docs:
  - backend-design.md
  - database-design.md
  - security.md
keywords: [infrastructure, deployment, ci/cd, electron, ASP.NET Core, MSSQL, monitoring, prereq, sidecar, framework-dependent]
last_updated: 2026-06-18 (打包預設連線 DEFAULT_CONFIG 硬編 + NSIS 安裝目錄 Ceremony + sidecar cwd 修正)
---

## 部署型態（**2026-05-28 改為 Sidecar 架構**）

法會報名系統屬**內網單一寺院使用**，無公雲需求。架構改為「**Electron + .NET sidecar API 同一個 .exe**」+「**集中 MSSQL DB 主機**」：

```
┌─────────────────────────────────────────────────────┐
│ 寺方內網 LAN                                        │
│                                                     │
│  ┌─────────────────────────────┐   ┌──────────────┐│
│  │ 寶覺寺法會報名系統.exe          │   │ DB 主機      ││
│  │ ─────────────────────────── │   │ 固定 IP/主機名 ││
│  │  Electron main (UI shell)   │   │              ││
│  │   ↕ http://localhost:<port> │   │  SQL Server  ││
│  │  Ceremony.Api.exe (sidecar) │───►   Ceremony DB ││
│  │  （隨 Electron 啟動 / 關閉） │1433│              ││
│  └─────────────────────────────┘   └──────────────┘│
│        × N 台 client                                │
│                                                     │
│  ※ 每台 client 跑自己的 API instance，連同一個 DB    │
└─────────────────────────────────────────────────────┘
```

**核心原則**：
- **client exe = Electron + .NET sidecar（framework-dependent .NET 10）**（`dotnet publish -r win-x64 --self-contained false -p:PublishSingleFile=true`，`Ceremony.Api.exe` 實測約 **~64 MB**；不內包 .NET runtime，但 **SkiaSharp / QuestPDF 原生庫**仍隨 exe self-extract，故仍偏大；靠 client 機器上的 .NET 10 ASP.NET Core Runtime 執行）
  - **2026-06-02 決策（取代舊「.NET 8 self-contained」敘述）**：使用者要求開機偵測 .NET 10 runtime → 內包 runtime 會讓偵測失去意義；改 framework-dependent 省去 .NET runtime（~80 MB），但因列印用 SkiaSharp 原生庫，exe 仍 ~64 MB（非 ~10 MB）。缺 runtime 時由 Electron prereq 偵測引導安裝（見下「軟體相依偵測」）。專案所有 csproj 已是 `net10.0`，無需 TFM 遷移。**注意**：publish 會產 `libSkiaSharp.pdb`（~80 MB debug symbols）→ publish 腳本以 `DebugType=none` + `find -name '*.pdb' -delete` 移除，electron-builder extraResources 再以 `!**/*.pdb` 雙重排除，不進 installer。
- **DB 主機獨立**（既有 SQL Server，不動）；client 透過 LAN 連線
- **資料一致性**：所有 client 都連同一個 DB；UPDLOCK、SignupLog、transaction 都在 DB 層處理（後端已實作）

**為何不用 server-side API**（原方案放棄理由）：
- 多一台 Windows Server 需要 IT 維護（systemd / Windows Service / Seq 監控）
- 寺方 IT 資源有限，sidecar 把 API 跟 client 同生命週期管理更簡單
- 升級走 electron-updater 自動推，client 一次更新含 API

**為何不嵌入 DB**（保留集中 MSSQL）：
- MSSQL Server 無法 embed 進 .exe；換 SQLite 要重寫 Dapper SQL（T-SQL 方言不通用）
- 既有資料、stored procedure、備份策略已建立在 MSSQL，沒理由動

**安全取捨（2026-05-28 決定走方案 C）**：
- DB 連線資訊以**純文字 JSON 設定檔**存於 client 的 `%APPDATA%/Ceremony/config.json`
- **不加 DPAPI / Windows Authentication**（IT 簡化優先）
- **風險**：被本機管理員權限的人讀到等於拿到 DB 帳密
- **緩解**：
  - DB 帳號**最小權限**（只能 DML + EXEC backup proc，無 DDL；見 [security.md](security.md) DB 帳號規格）
  - LAN-only（無公網暴露）、SQL Server 開 Windows Firewall 限制來源網段
  - 後續若要升級安全，可改 Windows Authentication（A）或 DPAPI 加密（B），現有架構與此相容

## 環境

| Env | 用途 | 部署位置 |
|---|---|---|
| dev | 開發 | 本機 — MSSQL `(local)` + `dotnet run` + `ng serve`（不打包，便於除錯） |
| staging | 上線前驗證 | 寺方備援機 — 打包後的 Electron + sidecar，連 staging DB |
| prod | 正式 | 寺方各 client PC — 安裝 Electron exe，連 prod DB 主機 |

## 環境變數 / 設定

採 ASP.NET Core 標準 `appsettings.{Env}.json` **三層覆蓋** + 以下兩種覆蓋來源：
- **dev**：`dotnet user-secrets`（位於開發者 home 目錄；不 commit）
- **prod（sidecar 模式）**：**Electron main process 從 `%APPDATA%/Ceremony/config.json` 讀使用者設定，啟動 sidecar 時透過 ENV var 注入**（API 直接吃 `ConnectionStrings__Ceremony` 等標準變數，code 不必改）

> ⚠️ **連線字串環境差異**：dev=`Server=(local);User Id=sa;Password=<dev-password>`、prod=`Server=<由使用者於首次啟動填入>;User Id=sa;Password=<同>`。密碼**永不寫入 repo**——dev 走 `dotnet user-secrets`、prod 由 Electron 從 user config 讀後注入。實際 dev 密碼值參見 user auto-memory `~/.claude/.../memory/db-credentials.md`。

### Sidecar 模式設定流程（prod）

1. 使用者灌完 installer 第一次啟動 → Electron 偵測 `%APPDATA%/Ceremony/config.json` 不存在 → 跳「初次設定」頁
2. 設定頁**以打包預設（`DEFAULT_CONFIG`）預填全部欄位（含密碼）**：DB 主機 `192.168.1.151`、port 1433、DB 名稱 Ceremony、user `sa`、password。使用者單機固定連寺方區網 DB，通常直接按「測試連線」即可（仍可改 IP / 帳密）
3. 按「測試連線」→ Electron 暫存到記憶體 → spawn API（傳 ENV var）→ 打 `/health` → 成功則寫 `config.json`
4. 後續啟動：Electron 讀 `config.json` → spawn API → API 用 ENV var 取代 `appsettings` 預留欄位

> **打包預設連線（2026-06-18 決策）**：`DEFAULT_CONFIG` 定義於 [frontend/electron/config.ts](../../frontend/electron/config.ts)，`main.ts` `getStatus` 在無 `config.json` 時以 `defaults` 欄位（含密碼）回給 `/setup` 預填（[setup-page.ts](../../frontend/src/app/features/setup/setup-page.ts) `prefill()`）。**注意**：此常數含**明文 prod 密碼**，為 CLAUDE.md 規則 11 的**已接受例外**（使用者明確選擇硬編而非 gitignored 種子檔）；一旦 commit 即進 git 歷史。決策與緩解見 [security.md](security.md)「打包預設連線」段。真實密碼值僅存 user auto-memory `db-credentials.md`。

`config.json` 結構（純文字 JSON，**不加密**，方案 C）：
```jsonc
{
  "dbHost": "192.168.1.151",
  "dbPort": 1433,
  "dbName": "Ceremony",
  "dbUser": "ceremony_app",
  "dbPassword": "<plain-text>",
  "apiPort": 0,        // 0 = 動態指派（每次啟動找空閒 port）
  "jwtKey": "<base64>" // 每機隨機 JWT 簽章 key（首次寫入自動產生）；經 Jwt__SigningKey 注入 sidecar
}
```

> `jwtKey` 由 Electron `config.ts` 首次寫檔時以 `crypto.randomBytes(32)` 產生並持久化（每台 client 各自唯一）。同機 API 簽發/驗證一致；避免使用 `appsettings.json` 內的 placeholder 當可預測 key。實作見 [frontend/electron/config.ts](../../frontend/electron/config.ts)。

Electron 啟動 sidecar 時組裝連線字串：
```ts
const connStr = `Server=${cfg.dbHost},${cfg.dbPort};Database=${cfg.dbName};User Id=${cfg.dbUser};Password=${cfg.dbPassword};TrustServerCertificate=true;MultipleActiveResultSets=True`;
spawn(apiExe, [`--urls=http://localhost:${apiPort}`], {
  env: { ...process.env, ConnectionStrings__Ceremony: connStr },
});
```

### 三層覆蓋範例

```jsonc
// appsettings.json（commit）— template，無連線值
{
  "ConnectionStrings": {
    "Ceremony": "Server=__OVERRIDE__;Database=Ceremony;TrustServerCertificate=true;User Id=sa;Password=__OVERRIDE__;MultipleActiveResultSets=True"
  }
}

// appsettings.Development.json（commit）— server 名稱可放，密碼留空待 user-secrets
{
  "ConnectionStrings": {
    "Ceremony": "Server=(local);Database=Ceremony;TrustServerCertificate=true;User Id=sa;MultipleActiveResultSets=True"
  }
}

// dotnet user-secrets（不 commit；位於 ~/.microsoft/usersecrets/<UserSecretsId>/secrets.json）
{
  "ConnectionStrings:Ceremony": "Server=(local);Database=Ceremony;TrustServerCertificate=true;User Id=sa;Password=<dev-pwd>;MultipleActiveResultSets=True"
}

// production（sidecar 模式）：Electron main 從 %APPDATA%/Ceremony/config.json 讀後組 ENV var 傳給 sidecar
// ConnectionStrings__Ceremony=Server=<host>,<port>;Database=Ceremony;User Id=<u>;Password=<p>;TrustServerCertificate=true;MultipleActiveResultSets=True
```

### Secret 管理規則（CRITICAL）

- 任何 `Password=` / `Secret=` / `Key=` 欄位**禁止**出現在 commit 的檔（含 `appsettings.json` / `appsettings.Development.json` / source code / docs）
- **dev**：`dotnet user-secrets set "ConnectionStrings:Ceremony" "Server=(local);..."`
- **prod（sidecar 模式）**：Electron main 從 `%APPDATA%/Ceremony/config.json` 讀後注入 ENV var；**安裝包本身不含密碼**
- **server-side 部署**（若未來改）：systemd `Environment=`、IIS app pool `ConnectionStrings__Ceremony`、Docker `-e`
- **實際密碼值**僅記載於 user auto-memory `~/.claude/projects/-Users-tim-agents-ceremony/memory/db-credentials.md`，**不在本檔**
- 已配置 [repo root .gitignore](../../.gitignore) 規則：`appsettings.Production.json` / `appsettings.*.local.json` / `**/secrets.json` / `**/.env*`
- **`%APPDATA%/Ceremony/config.json` 是使用者本機檔案**（不 commit、不上 update server）；首次啟動由 `/setup`（打包預設預填）寫出後僅存在於該 client 的 user profile 下
- **打包預設連線 `DEFAULT_CONFIG`（2026-06-18）**：硬編於 [config.ts](../../frontend/electron/config.ts)（`dbHost=192.168.1.151` + sa 密碼）；無 `config.json` 時 `getStatus` 以 `defaults` 回給 `/setup` 預填（含密碼），使用者按「測試連線」寫出 config.json。**含明文密碼，為規則 11 已接受例外**，詳見 [security.md](security.md)「打包預設連線」段與 [blueprints/electron-packaging.md](../blueprints/electron-packaging.md)
- **sidecar 啟動須設 `cwd = resources/api`（2026-06-02）**：single-file exe 的 ContentRoot 取自工作目錄；不設則 appsettings.json 不載入 → `Backup:Directory` 等為 null（曾致備份 500 `BACKUP_NOT_CONFIGURED`）。見 [gotchas.md](../gotchas.md)
- **`Backup:Directory = D:\Backup`**：須存在且 SQL Server 服務帳號（`NT Service\MSSQLSERVER`）可寫，否則 BACKUP DATABASE 失敗（同機部署；舊系統已用此路徑）

### Settings keys

| Key | 範例 | 說明 |
|---|---|---|
| `ConnectionStrings:Ceremony` | `Server=(local);Database=Ceremony;User Id=sa;Password=<from-user-secrets>` | DB 連線；dev 走 user-secrets、prod（sidecar）由 Electron 從 `%APPDATA%/Ceremony/config.json` 讀後 ENV var 注入 |
| `Jwt:Issuer` | `https://ceremony.local` | – |
| `Jwt:Audience` | `ceremony-client` | – |
| `Jwt:PrivateKeyPath` | `/secrets/jwt.key` | RS256 私鑰 |
| `Jwt:AccessTokenMinutes` | `30` | – |
| `Jwt:RefreshTokenDays` | `7` | – |
| `Backup:Directory` | prod Windows：`\\dbserver\Backups\Ceremony\`（UNC）或 `D:\Backup\`；**dev（`(local)` Docker Linux MSSQL）：`/var/opt/mssql/data/`**（容器可寫，已設於 `appsettings.Development.json`） | 備份目錄（**非 secret，寫在 appsettings.json**）。**此路徑屬於「SQL Server 主機」的檔案系統**（`.bak` 由 DB engine 寫，不是 API process）；該主機的 SQL Server 服務帳號必須對此路徑有寫入權限，否則回 500 `error 5 (Access is denied)`。**路徑分隔符依目錄字串自動判斷**（含 `\`→Windows、否則 Unix；見 `SqlBackupService.JoinForSqlServer`，不可用 `Path.Combine` 以免 API 在 macOS/Linux 跑時組出 `D:\Backup\/file`）。目錄建立為 best-effort（API 與 DB 不同機時由 DBA 預建）。sidecar 模式建議走 UNC 寫入 DB 主機共用資料夾（避免多 client 各自有自己的 .bak 散落）。`sizeBytes` 在 API 看得到檔時用實檔大小，否則 fallback 查 `msdb.dbo.backupset` |
| `Backup:RetentionDays` | `30` | 保留天數；**目前僅 config 值，尚未實作清理服務**（仍依賴外部 SQL Agent 清舊備份）。未來可由 background worker 落實（見「後續可選增強」） |

**備份「清交易紀錄檔」（`POST /backup` 的 `clearLog=true`，2026-05-29 新增）**：完整備份後，依資料庫 recovery model 安全清交易紀錄檔，**不破壞還原鏈**（完整備份已先完成為鏈起點）：

- **FULL / BULK_LOGGED**：`BACKUP LOG [db] TO DISK = N'{Backup:Directory}/{ts}.trn' WITH NOFORMAT, NOINIT, NAME=N'Ceremony-Log Backup', SKIP, NOREWIND, NOUNLOAD`（正確截斷、保留 .trn）→ `DBCC SHRINKFILE(log, 1)`。`.trn` 與 `.bak` 同目錄、同樣需 SQL Server 服務帳號可寫。
- **SIMPLE**：`CHECKPOINT`（即截斷）→ `DBCC SHRINKFILE(log, 1)`。
- 清 log 失敗不影響備份成功（API 仍回 200，`logCleared=false` + `logClearError`）。
- ⚠ 不使用 `BACKUP LOG ... TO DISK='NUL'`（會破壞還原鏈，DBA 反模式）。屬半破壞性 DBA 操作，前端勾選時 confirm 加警語（見 [security.md](security.md)）。
| `Reporting:FontDirectory` | `./Fonts` | 內嵌 BiauKai 字型 |
| `Reporting:RdlcPositionsFile` | `./rdlc-positions.json` | 預載 RDLC 各模板的 cm 座標表（[printing-reports-positions.md](../blueprints/printing-reports-positions.md) 為 source of truth） |
| `Auth:BackdoorEnabled` | `true` | 是否啟用舊系統後門帳號 weypro（客戶確認後可逐步關閉） |
| `Auth:FailedLoginThreshold` | `5` | 失敗鎖定門檻（in-memory） |
| `Auth:FailedLoginLockMinutes` | `15` | 失敗鎖定時間 |
| `Logging:Seq:ServerUrl` | `http://seq:5341`（dev） | Seq log server |
| `Cors:AllowedOrigins` | `http://localhost:4200`（dev）/ `null` 與 `file://`（prod Electron renderer） | dev 為 ng serve、prod sidecar 模式 renderer 從 `file://` 載入時 Origin header 通常為 `null`，需明確 allow |

## 部署單元

### Sidecar 模式：所有東西打包在同一個 NSIS installer 內

```
寶覺寺法會報名系統-<ver>-setup.exe   ← electron-builder 產出
├── electron/                       ← Electron main + preload
├── dist/                           ← Angular SPA (renderer)
└── api/                            ← .NET sidecar
    └── Ceremony.Api.exe            ← dotnet publish --self-contained --single-file
        + 所有 dependencies（一檔內）
```

**Electron main 啟動流程**（**已實作**，實檔見 [frontend/electron/](../../frontend/electron/) `main.ts` / `sidecar.ts` / `config.ts` / `prereq.ts` / `download.ts` / `preload.ts`；下為示意，實作以動態 port + ready check 為準。**注意**：實作用內建 `findFreePort()`（node `net`）取代 `get-port`，因後者為 ESM-only 與 CJS main 不相容）：
```ts
import { spawn } from 'child_process';
import getPort from 'get-port';
import path from 'path';

const apiPort = await getPort();                      // 找空閒 port
const apiExe = path.join(process.resourcesPath, 'api', 'Ceremony.Api.exe');
const cfg = readUserConfig();                          // %APPDATA%/Ceremony/config.json
const connStr = buildConnectionString(cfg);            // 組 MSSQL conn string

apiProc = spawn(apiExe, [`--urls=http://localhost:${apiPort}`], {
  env: { ...process.env, ConnectionStrings__Ceremony: connStr },
});

await waitForReady(`http://localhost:${apiPort}/health`);
mainWindow.loadURL(`file://.../index.html#/?apiBase=http://localhost:${apiPort}/api/v1`);

app.on('before-quit', () => apiProc.kill('SIGTERM'));
```

### 後端打包（sidecar，**framework-dependent .NET 10**）

- **打包**：`dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`（腳本：[backend/publish.ps1](../../backend/publish.ps1) / [backend/publish.sh](../../backend/publish.sh)，後者可在 macOS/Linux cross-publish）
- **輸出**：`Ceremony.Api.exe`（實測約 **~64 MB**；**不含 .NET runtime**，但含 SkiaSharp/QuestPDF 原生庫；client 須裝 **.NET 10 ASP.NET Core Runtime**，由 prereq 偵測引導）；publish 腳本已移除 pdb
- **執行**：由 Electron main `spawn` 為子進程（動態 `--urls` port）；隨 Electron 啟動 / 關閉（`before-quit` kill）
- **設定來源**：`appsettings.json` 為 template（無密碼），Electron 啟動時透過 ENV var 覆蓋連線字串（`ConnectionStrings__Ceremony`）、CORS（`Cors__AllowedOrigins__0=null` / `__1=file://`）、JWT 簽章 key（`Jwt__SigningKey`，每機隨機存 config.json）

### 軟體相依偵測（prereq，**2026-06-02 新增**）

Electron main 開機先偵測 client 是否裝齊必要元件，缺了走 `/prereq` 頁引導安裝（[frontend/electron/prereq.ts](../../frontend/electron/prereq.ts)）：

| 元件 | 為何需要 | 偵測方式（Windows） | 缺少時 |
|---|---|---|---|
| **Microsoft Visual C++ 2015-2022 Redistributable (x64)** | SkiaSharp 列印（直書姓名 / 垂直地址 PNG）相依 | `reg query HKLM\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64 /v Installed`（含 WOW6432Node）→ `Installed=0x1` | `/prereq` 頁顯示「安裝」(launchInstaller) / 「前往下載」([aka.ms/vs/17/release/vc_redist.x64.exe](https://aka.ms/vs/17/release/vc_redist.x64.exe)) |
| **.NET 10 ASP.NET Core Runtime (x64)** | framework-dependent sidecar 執行相依 | `dotnet --list-runtimes` 解析含 `Microsoft.AspNetCore.App 10.*` | 同上（[dotnet.microsoft.com/download/dotnet/10.0/runtime](https://dotnet.microsoft.com/download/dotnet/10.0/runtime)） |

- **非 Windows（dev on macOS/Linux）**：略過偵測（回 ok），sidecar 走 `dotnet run`，不需 client runtime。
- **bundled installer（可選）**：若把 installer 放 `frontend/build/prereqs/`（→ 打包進 `resources/prereqs/`），`launchInstaller` 直接執行；缺檔則 `openExternal` 開官方下載頁。

### 備份下載（另存，**2026-06-02 新增**）

瀏覽器 SPA 無法選伺服器路徑、`.bak` 又由 DB 主機端 SQL Server 寫 → Electron 殼提供「下載到本機另存」：

```
備份頁按「下載」 → renderer 取 JWT → window.ceremony.downloadBackup(fileName, token)
  → main：dialog.showSaveDialog（原生另存）→ Electron net GET {apiBase}/backup/{file}/download（帶 Bearer）
  → 串流寫到使用者選的本機路徑（不佔 renderer 記憶體；.bak 動輒 ~100MB+）
```

- 後端 endpoint：`GET /api/v1/backup/{fileName}/download`（見 [api-design.md](api-design.md)、[get-backup-download.md](../blueprints/api-endpoints/get-backup-download.md)）。
- **路徑可讀限制**：API process 須讀得到 `Backup:Directory`。prod sidecar 模式建議 `Backup:Directory` 設 **UNC 共用**（`\\dbserver\Backups\Ceremony\`），讓 SQL Server 服務帳號可寫、client API process 可讀；dev docker MSSQL 的容器內路徑 API 端讀不到 → download 回 404（屬已知限制，dev 不影響備份本身）。
- 瀏覽器 fallback（非 Electron）：`BackupApi.fetchBlob` 抓 blob + `<a download>` 另存。

### 前端（Electron）

> **2026-05-26 決策**（仍有效）：
> 1. **僅打包 Windows 版本**（NSIS installer .exe）。寺方所有 client 都是 Windows。
> 2. **桌面 icon 沿用舊系統 .ico**（檔案放 `reference/icons/ceremony.ico`）。
> 3. **執行順序**：Angular SPA 全部 feature 跑通後再進入 Electron 包裝階段。
>
> **2026-05-28 補充**：採 sidecar pattern；installer 同時包含 Electron + Angular SPA + .NET API exe（不再走 server-side API）。

- **打包**：`electron-builder` — `win` target 唯一
- **發佈**：寺方 NAS 或內部 update server；electron-updater 自動檢查更新
- **設定**：應用內提供「設定」頁，使用者可填 DB 主機資訊（首次啟動引導；存 `%APPDATA%/Ceremony/config.json`）
- **API base URL 處理**：API port 動態指派，由 Electron main 啟動後告知 renderer（透過 query string / IPC / global 變數），Angular 從中讀取後設定 [environment.ts](../../frontend/src/environments/environment.ts) 的 `apiBaseUrl`

#### electron-builder.yml（Windows-only 範例）

```yaml
appId: tw.ceremony.bao-jue-temple
productName: 寶覺寺法會報名系統
copyright: © 寶覺寺
directories:
  buildResources: build
files:
  - dist/**/*
  - electron/**/*

# Sidecar：.NET API self-contained exe 一起打包到 resources/api/
extraResources:
  - from: ../backend/publish/win-x64
    to: api
    filter: ['**/*']

win:
  target:
    - target: nsis
      arch: [x64]
  icon: build/icon.ico            # ← 沿用舊系統 .ico
  artifactName: ${productName}-${version}-setup.exe

nsis:
  oneClick: false
  perMachine: true
  allowToChangeInstallationDirectory: true
  installerIcon: build/icon.ico
  uninstallerIcon: build/icon.ico
  installerHeaderIcon: build/icon.ico
  createDesktopShortcut: always
  createStartMenuShortcut: true
  shortcutName: 寶覺寺法會報名系統
```

> `extraResources` 在 runtime 解開到 `process.resourcesPath`（NSIS 安裝路徑下的 `resources/api/`）。Electron main 從那邊 spawn `Ceremony.Api.exe`。

> **以實際 [frontend/electron-builder.yml](../../frontend/electron-builder.yml) 為準**（上為示意）。實際另含：`nsis.include: build/installer.nsh`（`preInit` macro 把預設安裝資料夾固定為 `$PROGRAMFILES64\Ceremony`，保留中文 productName）；`extraResources` 含 `build/prereqs`；icon 用 `build/icon.png`（由 logo.png 來）。打包預設連線改走硬編 `DEFAULT_CONFIG`（[config.ts](../../frontend/electron/config.ts)），不再用 extraResources 種子檔。

#### Icon 來源

- **來源檔**：[reference/icons/ceremony.ico](../../reference/icons/)（由客戶提供舊系統原始 .ico；上傳後置於此路徑）
- **構建時**：CI 把 `reference/icons/ceremony.ico` 複製到 `build/icon.ico`
- **規格建議**：含 16/32/48/256 多尺寸 PNG-in-ICO；最大 256×256
- **無 .ico 時的 fallback**：暫用 placeholder（程式仍可跑，但安裝後桌面 icon 為通用圖示）

#### 程式碼簽章（Windows）

| 證書類型 | 設定 |
|---|---|
| EV Code Signing（或 OV） | `electron-builder.yml` 內 `win.certificateFile` + `CSC_LINK` / `CSC_KEY_PASSWORD` env var |

> 內網單一寺院使用 — Windows EV 證書可選；若不簽，SmartScreen 會警告但仍可安裝。建議至少 OV（成本較低）。

#### Auto-update（electron-updater）

```typescript
// electron/main.ts
import { autoUpdater } from 'electron-updater';

autoUpdater.setFeedURL({
  provider: 'generic',
  url: 'http://ceremony-update.local/releases/${os}',  // 內部 update server
});

autoUpdater.on('update-available', () => {
  // 通知使用者有新版
});

autoUpdater.on('update-downloaded', () => {
  // 詢問是否立即重啟更新
});

autoUpdater.checkForUpdatesAndNotify();
```

發布流程：
1. CI tag release → electron-builder 產 installer + `latest.yml`
2. 上傳到 update server `/releases/{os}/`
3. 各 client 啟動時自動檢查 → 提示更新
4. 強制更新策略：未來若有 critical bug，可設定最低版本門檻

#### 環境 base URL 處理（sidecar 模式）

dev / prod 用 [environment.ts](../../frontend/src/environments/environment.ts) 區分；prod 下 `apiBaseUrl` 必須在 Electron main spawn API 後動態覆寫（因為 sidecar API 用動態 port）。

```typescript
// electron/main.ts — 把動態 port 傳給 renderer
mainWindow.loadFile('dist/index.html', {
  search: `apiBase=${encodeURIComponent(apiBaseUrl)}`,
});

// frontend/src/main.ts — 啟動時讀 query string 覆寫 environment
import { environment } from './environments/environment';
const params = new URLSearchParams(window.location.search);
const apiBase = params.get('apiBase');
if (apiBase) (environment as any).apiBaseUrl = apiBase;

// 之後所有 *.api.ts 都從 environment.apiBaseUrl 讀
```

> Dev 模式（`ng serve`）：`apiBaseUrl` 直接走 `environment.ts` 預設值 `http://localhost:5050/api/v1`，不經由 Electron。

### 資料庫（MSSQL — 既有，完全不動）

- **版本**：沿用既有（192.168.1.151 或 localhost）
- **Schema**：**完全凍結**（客戶需求，見 [database-design.md](database-design.md)）
- **ORM**：Dapper + 手寫 SQL；**無 Migration 工具**
- **備份**：見 [database-design.md](database-design.md) 備份章節
- **DB 帳號**：sa → 應用專用帳號（最小權限，僅本 DB 表的 DML + EXEC backup proc）

> 部署時**不執行任何 DDL**。應用程式啟動只連 DB、讀資料、寫資料 — DB 結構零變動。

## CI/CD

GitHub Actions 工作流：

```yaml
# .github/workflows/ci.yml
on: [push, pull_request]
jobs:
  backend:
    - dotnet restore / build / test
    - 用 Testcontainers 跑真 MSSQL 整合測試
    - upload artifacts
  frontend:
    - npm ci / lint / typecheck / test
    - build Electron app（matrix: win/mac/linux）
    - upload artifacts
  e2e:
    - 啟動 backend (docker)
    - Playwright 跑 smoke + critical paths
```

```yaml
# .github/workflows/release.yml
on:
  push:
    tags: ['v*']
jobs:
  publish-backend:
    - 簽章 + 上傳到 release
  publish-electron:
    - electron-builder publish
    - 自動更新 manifest
```

部署：人工 promote staging → prod；prod 部署需 PR review + change ticket。

## 觀測（Observability）

| 層 | 工具 |
|---|---|
| Logging | Serilog → File (prod) / Seq (dev/staging) |
| Error tracking | Sentry（self-hosted in 寺方內網）or 文字 log 即可 |
| Tracing | OpenTelemetry → Jaeger（選用） |
| Metrics | Prometheus + Grafana（選用，內網一台機可省） |
| Health | `/health` endpoint + Electron 開機 ping |
| Audit | Serilog file log（**DB 凍結，不寫 audit_logs 表**，見 [security.md](security.md)） |

最低必要：File log + health check。Sentry 可選但強烈建議（前端 crash + 後端 exception 集中可視化）。

### Sentry 設定（如採用）

| 端 | 用途 |
|---|---|
| **後端**：`Sentry.AspNetCore` | 未捕獲例外、效能 trace |
| **前端**：`@sentry/angular` | renderer crash、JS error、Electron main crash |
| **filter**：PII | 用 `beforeSend` hook 移除 Name / Phone / Address 等個資再上傳 |

self-hosted Sentry 跑在內網（一台 docker），避免個資出網。

### Log 結構標準

```jsonc
{
  "@t": "2026-05-26T14:23:12.123Z",
  "@l": "Information",
  "@m": "Signup created",
  "@x": "...stacktrace...",  // 若有例外
  "EventType": "Audit",        // Audit / Application / Performance
  "AdminId": 5,
  "AdminName": "tim",
  "TraceId": "00-...-01",
  "RequestId": "0HMU...",
  "Operation": "signup.create",
  "Duration": 245,             // ms
  "Result": "success",
  "Payload": {                 // 已 PII mask
    "name": "王*明",
    "phone": "09****5678"
  }
}
```

輪替：daily，gzip；保留 90 天。

## 監控指標

| 指標 | 警示閾值 |
|---|---|
| API 5xx 率 | > 1% over 5 min |
| 登入失敗率 | > 10% over 5 min（可能爆破） |
| DB 連線失敗 | 任一次 |
| 備份失敗 | 任一次 |
| 磁碟空間 | < 10% free |

## 災難復原（DR）

### 備援策略

- **資料**：每日全備 + 每小時 log；備份送至異地 NAS / OneDrive
- **設定**：版控於 Git（去除 secret）
- **Secret 備援**：Vault / Key Vault 內容定期匯出至離線冷儲存

### 重建流程（RTO ≤ 1 小時）

```
1. 取新機（Windows Server 或 Ubuntu）
2. 安裝 SQL Server（同版本）
3. Restore 最新 .bak：
   RESTORE DATABASE Ceremony FROM DISK = 'X:\Backup\latest.bak'
     WITH REPLACE, RECOVERY;
4. 套用最近 transaction log（如有）：
   RESTORE LOG Ceremony FROM DISK = 'X:\Backup\latest.trn' WITH RECOVERY;
5. 部署 Ceremony.Api（.NET 8 runtime 已預裝）
6. 設定環境變數 / secret（連線字串、JWT key）
7. 啟動服務，驗 /health
8. 各 client 切換新 API URL
9. 業務驗收：列印 5 種報表確認 OK
```

### RTO / RPO

| 指標 | 目標 |
|---|---|
| RTO（Recovery Time Objective） | ≤ 1 小時 |
| RPO（Recovery Point Objective） | ≤ 1 小時 |

### 演練

- **季度演練**：將 prod 備份 restore 到 staging，跑完整流程 + 列印
- **年度大演練**：模擬 prod 主機完全失效，從零重建
- **演練紀錄**：寫入 changelog，記錄發現的 gap 並修正

### 滾回（Rollback）

若新版上線後發現大問題：

```
T+0  → 偵測異常（用戶回報 / Sentry / health check 失敗）
T+5m → 決策：rollback or hotfix
T+10m → Electron 端：因 client 是舊版（24h 內換新版漸進），多數使用者其實還在舊版；只需把後端 API 換回舊版
T+15m → 後端 rollback：將舊版 Ceremony.Api .dll 復原 + 重啟服務
        DB 不需動（schema 凍結）
T+20m → 驗證舊版可用
T+24h → 寫 post-mortem + 修正後重新部署
```

關鍵保護：**DB schema 完全凍結** → 新舊版 API 共用同一 DB，rollback 無資料遺失風險。

## 環境特定設定速查

| 設定 | dev | staging | prod |
|---|---|---|---|
| API base URL | `http://localhost:5000` | `https://ceremony-staging.local` | `https://ceremony.local` |
| **DB 連線位置** | `(local)` | 待定 | `192.168.1.151` |
| **DB 帳號** | `sa` | `sa` | `sa` |
| **DB 密碼** | `<from dotnet user-secrets>` | `<from Vault>` | `<from ENV var ConnectionStrings__Ceremony>` |
| DB | docker MSSQL 容器 / 本機 MSSQL | 寺方備援機（prod 副本） | 寺方主機 |
| Secret 存放 | dotnet user-secrets | Key Vault | ENV vars / DPAPI / Vault |
| Logging | Seq UI | Seq + File | File only |
| Sentry | console | self-hosted | self-hosted |
| JWT TTL | 短（5 min）便於測試 | 標準（30 min） | 標準（30 min） |
| `Auth:BackdoorEnabled` | true | true | **由業務決定** |
| `RateLimit:Enabled` | false | true | true |
| Swagger | 開放 | 開放 | **關閉** |
| HTTPS | optional | 強制 | 強制 |
| auto-update | 關 | 測試版 | 正式 channel |

## 已知限制

1. **單一寺院 / 單伺服器** — 無水平擴展需求，故不上 Kubernetes
2. **內網部署** — 不需要 WAF、DDoS 防護
3. **無多語環境** — 暫不需要 CDN

## 後續可選增強

- 加入 background worker 排程備份（取代 SQL Agent）
- 加入 SignalR 推送即時通知（如「新增報名成功」廣播給其他 admin）
- 加入 Web 端（純前端 Angular）作為輕量替代（給只看報表的成員）
