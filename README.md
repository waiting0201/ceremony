# 寶覺寺法會報名系統（Ceremony）

舊系統（WinForms + RDLC + MSSQL）的現代化重寫：**ASP.NET Core 10 (Dapper) + Angular 21 SPA + 既有 MSSQL（schema 凍結）**。

> 文件入口走 [CLAUDE.md](CLAUDE.md) → [docs/](docs/)。本檔只講「怎麼把本機跑起來測試」，打包與細節指向 docs。

## 專案結構

```
backend/    .NET 10 Clean Architecture (Domain / Application / Infrastructure / Api)
frontend/   Angular 21 SPA（zoneless + Material + signalStore）
mockup/     純 HTML/CSS 視覺草模
reference/  舊系統 source code（read-only 對照用）
docs/       所有設計文件 / blueprint / workflow（單一真實來源）
```

## 本機網頁測試（最常用）

前置依賴：**.NET SDK 10.x**（`dotnet --version`）、**Node 22+**（`node -v`）、可連上的 **MSSQL `Ceremony` DB**（本機通常從 prod 還原 `.bak`）。

### 1. 首次設定（換機才需要）

```bash
# Backend secrets — 連線字串 + JWT key（絕不入 repo，密碼見 user auto-memory）
cd backend/src/Ceremony.Api
dotnet user-secrets set "ConnectionStrings:Ceremony" \
  "Server=(local);Database=Ceremony;User Id=sa;Password=<dev-pwd>;TrustServerCertificate=True;MultipleActiveResultSets=True;Encrypt=False"
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)"

# Frontend 套件
cd ../../../frontend && npm install
```

> 實際 dev/prod 密碼存於 repo 外：`~/.claude/projects/-Users-tim-agents-ceremony/memory/db-credentials.md`

### 2. 啟動（開兩個 terminal）

```bash
# Terminal A — Backend API（port 5050）
cd backend/src/Ceremony.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run --urls=http://127.0.0.1:5050 --no-launch-profile

# Terminal B — Frontend SPA（port 4200）
cd frontend
npm start
```

### 3. 開瀏覽器

打開 **http://localhost:4200** → `/login` 用後門帳號 **`sa@system.local` / `Admin@123`** 登入。

> 健康檢查：`curl -s http://127.0.0.1:5050/health` 應回 `{"status":"healthy","db":"up"}`（`db:"up"` 才代表 DB 連得上）。CORS 已對 `localhost:4200` / `127.0.0.1:4200` 開放。

## 常用指令

```bash
# 後端
cd backend
dotnet test --nologo                            # 全部測試（148 unit + 60 integration，integration 需 DB）
dotnet test tests/Ceremony.Application.Tests    # 只跑單元測試（不需 DB）
dotnet build                                     # build 全部

# 前端
cd frontend
npm start          # ng serve（dev server + HMR）
npm run build      # 產生 dist/
npm test           # vitest
```

OpenAPI doc（Development 才開）：http://127.0.0.1:5050/openapi/v1.json

## 故障排除

| 症狀 | 解法 |
|---|---|
| API 啟動 throw `Jwt:SigningKey not configured` | 未設 user-secrets，跑「首次設定」 |
| `/health` 回 `"db":"down"` | MSSQL 沒啟動或連線字串錯，確認 `sqlcmd` 連得上、user-secrets 密碼正確 |
| 前端登入後馬上 401 | 剛改 `Jwt:SigningKey` 需重啟 API |
| Port 5050 / 4200 被佔 | `lsof -i :5050` 找出來 kill，或換 port 並更新 frontend env |

## 其他

- **打包成 Windows 安裝檔（Electron + .NET sidecar）**：見 [docs/design/infrastructure.md](docs/design/infrastructure.md)「部署型態」與 [.github/workflows/release.yml](.github/workflows/release.yml)（推 `v*` tag 自動打包）。
- **API 一覽（29 endpoints）**：[docs/blueprints/api-endpoints/README.md](docs/blueprints/api-endpoints/README.md)
- **Secret / 安全規則**：絕不入 repo；dev 走 `dotnet user-secrets`、prod 走 ENV vars。完整規則見 [docs/design/infrastructure.md](docs/design/infrastructure.md)。
- **開發流程**：動 code 必動 doc、API 走雙向稽核，見 [CLAUDE.md](CLAUDE.md)。
- **當前進度 / 待辦**：[docs/status.md](docs/status.md)
