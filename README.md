# 寶覺寺法會報名系統（Ceremony）

舊系統（WinForms + RDLC + MSSQL）的現代化重寫：**ASP.NET Core 10 (Dapper) + Angular 21 SPA + 既有 MSSQL（schema 凍結）**。

> 文件入口走 [CLAUDE.md](CLAUDE.md) → [docs/](docs/)。本檔負責「怎麼把本機跑起來」與「怎麼打包成 Windows 安裝檔」。

## 專案結構

```
backend/          .NET 10 Clean Architecture (Domain / Application / Infrastructure / Api)
frontend/         Angular 21 SPA（zoneless + Material + signalStore）
mockup/           純 HTML/CSS 視覺草模（含 5 份列印 1:1 對位預覽）
reference/        舊系統 source code、RDLC、抽出的圖檔（read-only 對照用）
docs/             所有設計文件 / blueprint / workflow（單一真實來源）
```

## 前置依賴

| 元件 | 版本 | 檢查指令 |
|---|---|---|
| .NET SDK | 10.x | `dotnet --version` |
| Node | 22+ | `node -v` |
| MSSQL `(local)` + `Ceremony` DB | 任意可連上 | `sqlcmd -S localhost -U sa -P <pwd> -Q "SELECT DB_ID('Ceremony')"` |

> **DB schema 完全凍結**：新系統直接連既有 `Ceremony` DB（不做 migration）。本機通常從 prod 還原 `.bak` 來建。

## 首次設定（換機才需要）

### Backend — 設定 user-secrets

連線字串 + JWT 簽章 key **絕不**寫進 repo；走 `dotnet user-secrets`（dev）/ ENV vars（prod）。

```bash
cd backend/src/Ceremony.Api

# DB 連線（密碼填本機 sa 密碼）
dotnet user-secrets set "ConnectionStrings:Ceremony" \
  "Server=(local);Database=Ceremony;User Id=sa;Password=<dev-pwd>;TrustServerCertificate=True;MultipleActiveResultSets=True;Encrypt=False"

# JWT 簽章 key（≥32 字元）
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)"
```

實際 dev / prod 密碼存於 user auto-memory（repo 外）：
`~/.claude/projects/-Users-tim-agents-ceremony/memory/db-credentials.md`

驗證：

```bash
dotnet user-secrets list
# 應看到 ConnectionStrings:Ceremony + Jwt:SigningKey
```

### Frontend — 安裝套件

```bash
cd frontend
npm install
```

## 啟動

開兩個 terminal。

### Terminal A — Backend API（port 5050）

```bash
cd backend/src/Ceremony.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run --urls=http://127.0.0.1:5050 --no-launch-profile
```

健康檢查：

```bash
curl -s http://127.0.0.1:5050/health
# {"status":"healthy","db":"up"}
```

OpenAPI doc（Development 才開）：http://127.0.0.1:5050/openapi/v1.json

### Terminal B — Frontend SPA（port 4200）

```bash
cd frontend
npm start
```

打開 http://localhost:4200 → `/login` 用後門帳號 `weypro / weypro12ab` 登入。

> CORS 已對 `http://localhost:4200` 與 `http://127.0.0.1:4200` 開放（[appsettings.json](backend/src/Ceremony.Api/appsettings.json) `Cors:AllowedOrigins`）。

## 常用指令

### 後端

```bash
cd backend

# 跑所有測試（148 unit + 60 integration）
dotnet test --nologo

# 只跑單元測試（不需 DB）
dotnet test tests/Ceremony.Application.Tests --nologo

# Build 全部
dotnet build

# 單一 endpoint 手動測試（backdoor 登入 → 拿 token）
TOKEN=$(curl -s -X POST http://127.0.0.1:5050/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"weypro","password":"weypro12ab"}' | jq -r .token)
curl -s -H "Authorization: Bearer $TOKEN" http://127.0.0.1:5050/api/v1/admins | jq
```

### 前端

```bash
cd frontend

npm start          # ng serve（dev server + HMR）
npm run build      # 產生 dist/
npm test           # vitest
```

## 打包 / 發佈（Windows 安裝檔）

部署型態為 **Electron + .NET sidecar 同一個 .exe**（單機版，連寺方區網 MSSQL）。完整架構見 [docs/design/infrastructure.md](docs/design/infrastructure.md)「部署型態 / 部署單元」。

### 一鍵打包

```bash
cd frontend
npm run dist
```

產出：`frontend/release/寶覺寺法會報名系統-<version>-setup.exe`（NSIS installer）。

### `npm run dist` 做了什麼（三階段）

| 階段 | 指令 | 產出 |
|---|---|---|
| 1. 後端 sidecar | `npm run api:publish`（→ [backend/publish.sh](backend/publish.sh) / [publish.ps1](backend/publish.ps1)） | `backend/publish/win-x64/Ceremony.Api.exe`（framework-dependent .NET 10 單一 exe，~64MB，已去 `.pdb`） |
| 2. 前端 + Electron | `npm run electron:build`（= `ng build --base-href ./` + `tsc -p tsconfig.electron.json`） | `dist/frontend/browser/`（renderer）+ `electron/dist/`（main/preload） |
| 3. 封裝 | `electron-builder`（[electron-builder.yml](frontend/electron-builder.yml)） | `release/…-setup.exe`；sidecar exe 收進 `resources/api/` |

> 跨平台：sidecar 用 `dotnet publish -r win-x64` 在 **macOS / Linux 也能 cross-publish** 出 win-x64 exe；但 `electron-builder` 的 NSIS Windows target **建議在 Windows 上跑**（或 CI Windows runner）。

### 打包前置依賴

| 元件 | 用途 |
|---|---|
| .NET SDK 10.x | `dotnet publish` 出 sidecar |
| Node 22+ | `ng build` + electron-builder |
| Windows（建議） | NSIS installer 封裝；非 Windows 只能出到第 2 階段 |
| `frontend/build/icon.png` | 桌面 / installer icon（來源 `reference/icons/logo.png`，electron-builder 自動轉 .ico） |

### client 端執行依賴（裝機後）

framework-dependent 打包，client 須有：**.NET 10 ASP.NET Core Runtime (x64)** + **Microsoft Visual C++ 2015-2022 Redistributable (x64)**（SkiaSharp 列印用）。缺少時 Electron 開機 `/prereq` 頁引導安裝（[frontend/electron/prereq.ts](frontend/electron/prereq.ts)）。

### 打包預設連線（單機版重點）

installer 內**已內建預設 DB 連線 `192.168.1.151 / sa / Ceremony`**（[frontend/electron/config.ts](frontend/electron/config.ts) `DEFAULT_CONFIG`）。使用者首次啟動 → `/setup` 頁五欄（含密碼）**已預填** → 按「測試連線」→「儲存並連線」即可（仍可改 IP / 帳密）。設定寫入 client 本機 `%APPDATA%/Ceremony/config.json`。

> ⚠️ **安全例外**：`DEFAULT_CONFIG` 含**明文 prod 密碼**，偏離 [CLAUDE.md](CLAUDE.md) 規則 11（secret 不入 repo），為**已接受例外**（見 [docs/design/security.md](docs/design/security.md)「打包預設連線」段）。此密碼會進 installer，且 `config.ts` 一旦 commit 即進 git 歷史（事後須 rewrite history 才能清除）。

## API 一覽（29 個 endpoint shipped）

完整索引 + 對應舊 Form line ref：[docs/blueprints/api-endpoints/README.md](docs/blueprints/api-endpoints/README.md)

| Domain | Endpoints |
|---|---|
| Auth | `POST /auth/login` · `POST /auth/logout`（JWT 黑名單） |
| Admin | `GET/POST /admins` |
| Believer | `GET/POST /believers` · `GET/PUT/DELETE /believers/:id` |
| Signup | `GET/POST /signups` · `GET/PUT/DELETE /signups/:id` · `GET /signups/:id/logs` · `POST /signups/export`（Excel） |
| Category | `GET/POST /categories` · `PUT/DELETE /categories/:id` |
| Prepay | `POST /prepay/load` |
| Reports | `GET /reports/{datacard,receipt,tablet,text,worship}?signupId=` · `POST /reports/batch`（PDF 合併） |
| Backup | `POST /backup`（SQL Server BACKUP DATABASE） |

## Secret / 安全規則

| 規則 | 說明 |
|---|---|
| **絕不入 repo** | DB 密碼、JWT key、API token 等永不 commit |
| dev | `dotnet user-secrets`（存 `~/.microsoft/usersecrets/`） |
| prod | ENV vars（`ConnectionStrings__Ceremony` / `Jwt__SigningKey`） |
| 真實密碼 | 僅存於 user auto-memory（repo 外） |

完整規則見 [docs/design/infrastructure.md](docs/design/infrastructure.md) Secret 管理段。

## 開發流程

| 任務類型 | 規範 |
|---|---|
| 新增 API endpoint | 走 [docs/blueprints/api-endpoints/](docs/blueprints/api-endpoints/) 雙向稽核（forward + reverse），詳見 [CLAUDE.md](CLAUDE.md) 規則 10 |
| 修改既有功能 | 對照 [docs/blueprints/legacy-coverage/](docs/blueprints/legacy-coverage/) 勾選新狀態 |
| 動 code 必動 doc | 依 [CLAUDE.md](CLAUDE.md) 「文件同步規則」 |

## 故障排除

| 症狀 | 可能原因 | 解法 |
|---|---|---|
| API 啟動 throw `Jwt:SigningKey not configured` | 未設 user-secrets | 跑「首次設定」段 |
| `/health` 回 `"db":"down"` | MSSQL 沒啟動或 connection string 錯 | 檢查 `sqlcmd` 能否連上；確認 user-secrets 內 password 正確 |
| 前端登入後馬上 401 | JWT 簽章 key 不一致或被撤銷 | 重新 `npm start`；如果剛改 `Jwt:SigningKey` 需重啟 API |
| `dotnet test` integration 全紅 | DB 連不上 | 跟「API 啟動」同檢查；integration test 要 `(local)` Ceremony DB |
| Port 5050 / 4200 被佔 | 其他進程 | `lsof -i :5050` 找出來 kill；或改 `--urls=http://127.0.0.1:<其他port>` 並更新 frontend env |

## 進度

當前狀態 + 待辦：[docs/status.md](docs/status.md)
