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
last_updated: 2026-08-11 (**出廠種子對 config.json 的覆寫改為 per-key merge**〔現場：使用者關掉的「自動選紙」每次重開又變回開——種子只有連線五欄，舊寫法整包 assign 只撿回 `jwtKey`，於是 `printFormPreselect` / `apiPort` 這些本機欄位每次開機被清掉。規則收進 `electron/config-merge.ts` 的 `mergeConfig`，`/setup` 存檔同一條；另補一行 `app-start` 診斷紀錄，否則現場紀錄看不出「重開過」這件事〕。同日先前 (**列印排障 ②③④⑤ 的入口位置更正為「`/reports/preview` 頁面上方的列印排障列」**〔原寫「工具列」，而那條 toolbar 要先產出 PDF 才出現——故障時按不到。判準升級為：復位鍵的可見性不得依賴任何會被故障本身破壞的前提〕。先前 2026-08-10 (**排障 ④ 也變成按鈕、並寫死「復位步驟不可以放進安裝程式」**〔起因：客戶回報**找不到那些檔案**——「請到 `%APPDATA%\Ceremony` 刪某個檔」在現場等於做不到〕：④ 改為 `/reports/preview` 工具列的「**印表機設定**」鈕〔`rundll32 printui.dll,PrintUIEntry /e /n "<預設印表機>"`，叫出 Windows 自己的列印喜好設定，改一次紙按確定就覆寫掉壞掉的 DEVMODE——**寫入的是 Windows 不是我們**〕；新增一段 ⚠ 說明**為什麼不放進安裝程式**〔`perMachine` 的 NSIS 跑在提權帳號下 `$APPDATA` 可能指到別的 profile、一台多使用者只清得到一個、真正該復位的東西不是檔案、而 `print-form-restore.json` **刪掉比留著糟**（那是還原使用者原本紙張的唯一憑證）〕。同日先前 (**列印排障新增「現場自己按得到的止血鍵」與失敗印表機黑名單**〔決策 9d，客訴 KYOCERA PA2000 **按下檢視器列印鈕後整個程式卡死**、選不了別台印表機也關不掉預覽，只能重啟〕：排障表新增 ③〔`/reports/preview` 工具列的「自動選紙：開/關」，寺方自己按，原本只有環境變數那條 IT 才做得到〕與 ⑤〔按回「開」＝清 `print-form-printers.json` 黑名單〕；`formResult` 對照表新增 `skipped-printer-blocked` / `skipped-over-budget` / `skipped-disabled` / `skipped-helper-busy` 四格；資料流那行的 helper 參數改為 `--budget-ms 3000 [--blocked …]` 並註明**逾時不再 kill**。判準轉變：預檢〔9c〕擋的是「寫壞」，但這次的失敗模式是**接觸本身**，所以規則升級為「出過一次事就永遠不碰那台驅動」。先前 2026-08-08 (**列印排障步驟 ① 的分流判準修正**：「記事本也噴 ⇒ 與本程式無關」是錯的——每使用者預設 DEVMODE 共用且持久化，我們寫壞了記事本一樣噴；改為「先跑 ③ 止血 ＋ ④ 復位再測，仍噴才判給驅動」。起因是 KYOCERA PA2000 在 v2.4.2 之後仍回報 `0x80010105`，根因見 ../gotchas.md 同名條。先前 2026-08-06 (列印排障段新增 **`formResult` 對照表**〔七種結果各自代表什麼、現場該做什麼；重點是**只有 `exact` 會去動使用者的驅動設定**，其餘全是「什麼都沒做」〕；自訂紙張 runbook 補一條 ⚠ **尺寸錯的表單不再被自動選用**〔名稱對但超過 ±0.5mm → `mismatch` → 不寫每使用者預設 DEVMODE，改由標題提示手動選紙；推翻 2026-08-04 的「仍選它」，理由見 blueprints/print-channel-electron.md 決策 9b。**實務影響：舊系統留下的資料卡／薦牌／文牒三張表單沒重建之前，那三種報表就是每次手動選紙**〕。先前 2026-08-05 (新增「**列印排障：紙張預選出問題時**」段——2026-08-05 客訴「選了印表機卻跳『您的印表機已發生未預期的設定問題 0x80010105』」的產物：四步 runbook〔用**記事本**開印表機內容做三十秒分流〔記事本也噴＝純 Windows／驅動問題〕→ 取診斷紀錄的 `formResult`/`formError` → `CEREMONY_PRINTFORM_EXE` 指到不存在路徑當**現場止血開關**〔列印本身不受影響，只回到手動選紙〕→ 到「列印喜好設定」覆寫壞掉的 DEVMODE〕。止血開關的行為自此**受文件保證**，`electron/print-form.ts` 的 `helperPath()` 有對應註解。根因見 gotchas.md 與 blueprints/print-channel-electron.md 決策 9a。先前 2026-08-04 (列印通道資料流插入 `Ceremony.PrintForm.exe apply <type>`〔開視窗前依報表名把驅動的每使用者預設紙張選成對應自訂表單，best-effort 3s 逾時，失敗不影響列印〕；打包樹與 CI 步驟補 `resources/printform/`；**自訂紙張 runbook 語意升級**——表單名稱從「建議」變成契約〔程式依名稱比對，名稱不符＝完全不會被選到〕，表格加 reportType 欄，補 ±0.5mm 容差與「尺寸不符仍選它但標題與診斷紀錄帶 ⚠」。見 blueprints/print-channel-electron.md 決策 9。先前 2026-08-02 (**「列印通道」段改寫**：送印交回 Windows 原生列印對話框，程式只開 PDF 檢視器視窗；print-settings.json 與所有送印參數移除；自訂紙張 runbook 改標為「不做等於白改」〔Chromium 檢視器是 fit-to-printable-area 等比縮放，不像舊系統非等比拉滿，選錯紙就位置全跑掉〕。見 blueprints/print-channel-electron.md。先前 2026-07-31 (新增「列印通道」段：Electron 主行程送印、print-settings.json 與 config.json 分家的理由、webPreferences.plugins:true 必要條件、現場印表機自訂紙張 6 種尺寸 runbook（含舊 form 尺寸錯誤警告）；CORS WithExposedHeaders 追加 X-Report-Page-Size。先前 2026-07-28 (CORS 補 WithExposedHeaders(Content-Disposition, X-Signup-Count) 修正前端讀不到這兩個 header 的既有 bug；記錄批次列印 in-memory job store 對「單實例 sidecar」的部署依賴。先前 2026-07-01 prereq installer 改固定內建離線安裝檔，記錄 build/prereqs 兩檔來源與直接下載連結))))))))))
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
- 升級採**手動覆蓋安裝**：發新版 `setup.exe` 給 client，直接執行即就地升級（NSIS 靠固定 appId `tw.ceremony.bao-jue-temple` 認得舊安裝，先靜默移除舊版再裝新版，沿用 `$PROGRAMFILES64\Ceremony`，一次更新含 sidecar API）。`%APPDATA%/Ceremony/config.json` 在升級時保留（`deleteAppDataOnUninstall` 對 update 不生效），DB 連線設定不掉。**注意 NSIS 預設不比對版號，裝同版/舊版也會覆蓋（無降版保護）**。
  - electron-updater 自動更新**尚未實作**（無 `electron-updater` 依賴、`electron-builder.yml` 無 `publish` 區塊、main process 無 `autoUpdater`）；列為未來項。CI 的 `latest.yml` 為日後自動更新預留。

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

1. 使用者灌完 installer 第一次啟動 → Electron 偵測 `%APPDATA%/Ceremony/config.json` 不存在 **且無出廠種子** → 跳「初次設定」(`/setup`) 頁
2. 使用者填：DB 主機（IP / hostname）、port（預設 1433）、DB 名稱（預設 Ceremony）、user、password
3. 按「測試連線」→ Electron 暫存到記憶體 → spawn API（傳 ENV var）→ 打 `/health` → 成功則寫 `config.json`
4. 後續啟動：Electron 讀 `config.json`（或出廠種子覆寫）→ spawn API → API 用 ENV var 取代 `appsettings` 預留欄位

> **連線表單只設定一次，第二次以後不再出現（2026-06-18 釐清）**：連線設定**持久化於磁碟**，`bootstrap()`（[main.ts](../../frontend/electron/main.ts)）每次啟動 `readConfig()` → `readDefaultConfig()`（種子覆寫）→ `startSidecar()`，只要連得上 `connected=true`，`electronReadyGuard` 就跳過 `/setup` 直接進主程式（→ 因未登入再導向 `/login`）。
> - **正式打包版（含出廠種子 `default-config.json`，dbHost=192.168.1.151）**：每次啟動以種子覆寫 config 連線 → 永遠有設定 → **完全不出現 `/setup`**（種子為連線權威）。
>   - ⚠️ **覆寫是 per-key merge，不是整包換掉（2026-08-11 修）**：種子只有連線五欄，`jwtKey` / `apiPort` / `printFormPreselect` 這種**只存在於本機**的欄位必須留著。舊寫法整包 assign（只手動撿回 `jwtKey`）⇒ 使用者關掉的「自動選紙」每次重開又變回開。規則收在 [frontend/electron/config-merge.ts](../../frontend/electron/config-merge.ts) `mergeConfig`：`undefined` ＝這份來源沒有意見 → 保留既有值。`/setup` 存檔（`saveConfigAndConnect`）走同一條。
> - **無種子環境（dev / 種子缺檔）**：第一次在 `/setup` 存的 `config.json` 留在 `%APPDATA%/Ceremony/` → 第二次 `readConfig` 讀回 → 不再跳 `/setup`。
> - **唯一會再出現 `/setup` 的情況**：連線**失敗**（DB 帳密錯 / 主機關機連不到 / SQL 服務未起 → sidecar 起不來 → `connected=false`），`electronReadyGuard` 導向 `/setup` 並顯示錯誤讓使用者重設；或 config 與種子皆不存在。

`config.json` 結構（純文字 JSON，**不加密**，方案 C）：
```jsonc
{
  "dbHost": "192.168.1.151",
  "dbPort": 1433,
  "dbName": "Ceremony",
  "dbUser": "ceremony_app",
  "dbPassword": "<plain-text>",
  "apiPort": 0,        // 0 = 動態指派（每次啟動找空閒 port）
  "jwtKey": "<base64>",// 每機隨機 JWT 簽章 key（首次寫入自動產生）；經 Jwt__SigningKey 注入 sidecar
  "printFormPreselect": true // 列印前是否幫使用者預選驅動紙張；缺欄位 = true（2026-08-10 起）
}
// 種子只有連線五欄；apiPort / jwtKey / printFormPreselect 是本機欄位，
// 每次啟動的種子覆寫**不得**把它們清掉（見上方 ⚠️ per-key merge）。
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
- **`%APPDATA%/Ceremony/config.json` 是使用者本機檔案**（不 commit、不上 update server）；首次啟動由出廠種子寫出後僅存在於該 client 的 user profile 下
- **出廠連線種子 `frontend/build/default-config.json`（2026-06-02）**：同機部署 → `dbHost=192.168.1.151` + sa 密碼，打包進 `resources/default-config.json`，`main.ts` **每次啟動**以它覆寫 config.json 連線（種子為權威，跳過 /setup；覆寫走 `mergeConfig` per-key merge，保留 jwtKey / apiPort / printFormPreselect 等本機欄位）。**gitignore 不入 repo**（範例 `default-config.example.json` 占位）；安裝檔內含明文密碼 → 限內部交付。詳見 [security.md](security.md) 出廠預寫段與 [blueprints/electron-packaging.md](../blueprints/electron-packaging.md)
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
| `Auth:SuperAdminEnabled` | `true` | 是否啟用系統 SuperAdmin 帳號 `sa@system.local`（非 DB；取代舊 weypro 後門，客戶確認後可關閉）。另有 `Auth:SuperAdminUsername` / `Auth:SuperAdminPassword` |
| `Auth:FailedLoginThreshold` | `5` | 失敗鎖定門檻（in-memory） |
| `Auth:FailedLoginLockMinutes` | `15` | 失敗鎖定時間 |
| `Logging:Seq:ServerUrl` | `http://seq:5341`（dev） | Seq log server |
| `Cors:AllowedOrigins` | `http://localhost:4200`（dev）/ `null` 與 `file://`（prod Electron renderer） | dev 為 ng serve、prod sidecar 模式 renderer 從 `file://` 載入時 Origin header 通常為 `null`，需明確 allow |

**CORS exposed headers（2026-07-28 修正，2026-07-31 追加）**：policy 除了 `AllowAnyHeader/AllowAnyMethod`，
還必須 `.WithExposedHeaders("Content-Disposition", "X-Signup-Count", "X-Report-Page-Size")`。這三個不在 CORS safelist，
不明示 expose 前端就讀不到——修正前 `ReportApi.extractFileName()` 永遠回 `null`、`signupCount`
永遠 `undefined`，一直靠 fallback 檔名運作。dev（`:4200`→`:5050`）與 prod（Electron `file://`，
Origin 為 `null`）都算跨源，兩邊都需要。見 [gotchas.md](../gotchas.md)。
`X-Report-Page-Size` 則是列印通道用的紙張尺寸（微米）；**Electron 主行程走 `net.request` 不受 CORS 限制、
一定讀得到**，這裡的 expose 只影響 renderer 直接讀的路徑（報表預覽頁、dev `:4200`）。

**單實例假設**：批次列印的 job 狀態存在 API process 的記憶體裡（見
[backend-design.md](backend-design.md)）。這依賴上述「一台 client 一個 sidecar、無反向代理、
無負載平衡」的部署形態；若日後改為多實例或加上 LB，job store 必須改為外部儲存。

## 部署單元

### Sidecar 模式：所有東西打包在同一個 NSIS installer 內

```
寶覺寺法會報名系統-<ver>-setup.exe   ← electron-builder 產出
├── electron/                       ← Electron main + preload
├── dist/                           ← Angular SPA (renderer)
├── api/                            ← .NET sidecar
│   └── Ceremony.Api.exe            ← dotnet publish --self-contained --single-file
│       + 所有 dependencies（一檔內）
└── printform/                      ← 列印前預選驅動自訂表單（Windows-only，~2 MB）
    └── Ceremony.PrintForm.exe      ← publish 腳本第二段；缺檔時列印照常，只是紙張不會被預選
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
  - **2026-07-01 決策**：改為固定內建離線安裝檔（client 現場常無網路，不能臨時連網下載），`frontend/build/prereqs/` 已放兩個檔（gitignore，不進 repo，僅本機/CI 打包用）：`vc_redist.x64.exe`（[aka.ms/vs/17/release/vc_redist.x64.exe](https://aka.ms/vs/17/release/vc_redist.x64.exe)）、`aspnetcore-runtime-10-win-x64.exe`（直接下載連結 [aka.ms/dotnet/10.0/aspnetcore-runtime-win-x64.exe](https://aka.ms/dotnet/10.0/aspnetcore-runtime-win-x64.exe)，會導向當前最新 10.0.x 版；`dotnet.microsoft.com/download/dotnet/10.0/runtime` 只是需手動點選的落地頁，非直接下載連結）。**新機器/CI 打包前須重新放這兩個檔**（不在 repo 裡）；發版時建議定期重新下載以跟上 patch 版號。

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

### 列印通道（**2026-08-02 改寫：回歸舊系統形狀**）

程式只負責把 PDF 開在預覽視窗，**送印本身交給 Windows 原生列印對話框**（印表機／份數／紙張／
方向／頁面範圍）。完整背景與決策見 [print-channel-electron.md](../blueprints/print-channel-electron.md)。

```
按列印
  → window.ceremony.openReportInViewer(type, apiPath, token)
  → main：Electron net GET {apiBase}{apiPath}（帶 Bearer，串流落 %TEMP%/ceremony-print/）
  → main：resources/printform/Ceremony.PrintForm.exe apply <type> --budget-ms 3000 [--blocked <hash…>]
          （best-effort，3s 不等了但**不 kill**；曾經出事的印表機直接跳過，連 exe 都不啟動）
          ↳ 依中文表單名比對驅動紙張清單 → SetPrinter Level 9 寫每使用者預設 DEVMODE
  → BrowserWindow({ plugins:true, parent: mainWindow }).loadFile(file://x.pdf)
  → 使用者按 Chromium PDF 檢視器工具列的 🖨 → Windows 原生 PrintDlgEx（有「內容」「頁面範圍」）
  → 視窗 closed → 刪 temp + 還原原本的紙張
```

- **沒有列印設定檔**：`print-settings.json` 與所有送印參數（印表機／份數／縮放／方向／紙張）
  已於 2026-08-02 移除。原生對話框自己會記（Windows 的每使用者 DEVMODE），我們再記一份
  只會有兩個真相來源。現場殘留的舊 `print-settings.json` 不再被讀取，留著無害。
- **程式仍不決定送印參數**（2026-08-04）：紙張預選動的是**驅動的每使用者預設 DEVMODE**
  （＝「列印喜好設定」裡的值，PrintDlgEx 的初值來源），不是傳給 Chromium 的送印選項；
  使用者仍可在對話框裡改掉。這是把舊系統唯一會主動設定的那一格補回來——
  少了它，一台印表機只有一個預設紙張，六種報表最多一種會對。
  helper 失敗（非 Windows／缺檔／逾時／驅動拒絕）一律略過，**絕不影響列印成敗**。
  還原快照存 `%APPDATA%/Ceremony/print-form-restore.json`，app 崩潰時由下次啟動撿回。
- **紙張尺寸在後端定案**：`ReportPageSizes.cs` → QuestPDF `page.Size()`，
  ＝舊系統「RDLC 只是排版，真正尺寸由 `DeviceInfo` 決定」的位置。送印端不再二次指定。
- **PDF 不經 renderer**：`streamApiToFile`（與備份下載共用）直接落檔，
  批次合併成一份可達數百 MB。
- **`webPreferences.plugins: true` 是必要條件**：Chromium 內建 PDF viewer 預設關閉，
  沒開的話檢視器視窗與報表預覽 iframe 都是空白（見 [security.md](security.md)）。
- **診斷紀錄**：`%APPDATA%/Ceremony/logs/print-YYYYMMDD.log`，
  入口在 `/reports/preview` **頁面上方的列印排障列**「診斷紀錄」按鈕（不需先產生預覽）。
- **瀏覽器 fallback**（非 Electron）：`PrintService` 退回 `openPdfInNewTab`，
  `ng serve` 與單元測試行為完全不變。

#### 列印排障：紙張預選（`Ceremony.PrintForm`）出問題時

紙張預選是唯一會**改動使用者電腦狀態**的列印步驟（寫驅動的每使用者預設 DEVMODE）。
它壞掉的典型症狀不是「列印失敗」，而是 **Windows 的列印對話框自己噴錯**——
2026-08-05 客訴「選了印表機卻出現**您的印表機已發生未預期的設定問題．0x80010105**」
（`RPC_E_SERVERFAULT`）就是這一類，根因見 [../gotchas.md](../gotchas.md) 與
[print-channel-electron.md](../blueprints/print-channel-electron.md) 決策 9a。

| 步驟 | 動作 |
|---|---|
| ① 分流 | 用**記事本**開同一台印表機的「內容」。**只有本程式噴** → 是我們寫的 DEVMODE。⚠️ **記事本也噴 ≠ 與本程式無關**（2026-08-08 修正）：那份每使用者預設 DEVMODE 是共用且持久化的，我們寫壞了記事本一樣噴。這時**不要**直接判給驅動，改跑下面的 ③ → ④ → 再測，仍噴才是純 Windows／驅動問題（重啟 Print Spooler 服務、重裝驅動） |
| ② 取證 | `%APPDATA%\Ceremony\logs\print-YYYYMMDD.log`（左側選單「開啟診斷紀錄」），看 `formResult` / `formKind` / `formError` / `printerVirtual` |
| ③ 止血（現場自己來，2026-08-10 起） | `/reports/preview` **頁面上方列印排障列**的「**自動選紙：開／關**」按鈕按成「關」。之後程式完全不碰驅動設定，只是回到每次手動選紙。**這是給寺方按的**，不需要 IT、不需要重出版本 |
| ③' 止血（遠端協助） | 設環境變數 `CEREMONY_PRINTFORM_EXE` 指到一個**不存在**的路徑 → helper 回 `helper-missing` → 整段紙張預選跳過。與 ③ 等效，適用於連 UI 都進不去的情況 |
| ④ 修機器 | `/reports/preview` **頁面上方列印排障列**的「**印表機設定**」按鈕 → 紙張改一次後按確定（覆寫一份乾淨的每使用者預設 DEVMODE）。2026-08-10 起這顆按鈕直接叫 `rundll32 printui.dll,PrintUIEntry /e /n "<預設印表機>"`，**現場不必自己去控制台找印表機**。⚠️ 那個視窗也開不起來／卡住 ＝ 純驅動問題（見 ①）|
| ⑤ 想再試一次 | 把 ③ 那顆按鈕按回「開」——它**同時清掉失敗印表機黑名單** `%APPDATA%\Ceremony\print-form-printers.json`（直接刪檔等效）。驅動換過／表單重建過之後才需要做這一步 |

> ⚠️ **現場的復位步驟一律要有按鈕**：實務上「請客戶到 `%APPDATA%\Ceremony` 刪某個檔」＝做不到（客戶找不到那個資料夾）。
> 所以 ③④⑤ 都收在報表預覽頁**上方的列印排障列**，`%APPDATA%` 路徑只寫給遠端協助的人看。
> ⚠️ **2026-08-11 修正**：首版把它們放在預覽工具列裡（`@if (previewUrl())` 之內），
> 要先產出一份 PDF 才看得到——可是要救的正是「印不出來 / 按下去卡死」，那時根本產不出預覽。
> 判準升級為：**復位鍵的可見性不得依賴任何會被故障本身破壞的前提**。
> 同理**不要把清除動作放進安裝程式**：`perMachine` 的 NSIS 跑在提權帳號下，`$APPDATA` 可能指到
> 另一個 profile（一台多使用者時也只清得到一個），而且真正該復位的東西（驅動的每使用者預設 DEVMODE）
> 根本不是檔案。還原快照 `print-form-restore.json` 更是**刪掉比留著糟**——那是把使用者原本的紙張
> 設定放回去的唯一憑證，正確做法是啟動時 `recoverPendingFormRestore()` 還原後再刪。

⚠️ ③' 的行為**是受文件保證的**（`electron/print-form.ts` 的 `helperPath()` 有對應註解）：
它是「不重新出版本就能關掉這個功能」的最後手段，改動那段程式前必須先讀這裡。

**失敗印表機黑名單**（2026-08-10 起，決策 9d）：只要某台印表機在自動選紙時出過事
（`skipped-printticket-reject` / `skipped-printticket-unavailable` / `driver-rejected` / `error`），
就記進 `print-form-printers.json`，**之後永遠不再接觸那台驅動**；`helper-timeout` / `helper-error`
因為判斷不出是哪一台，直接整台機器停用預選。
起因是 2026-08-10 客訴「KYOCERA PA2000 按下檢視器的列印鈕之後整個程式卡死，
選不了別台印表機也關不掉預覽，只能重啟」——那不是寫入造成的（預檢早就擋下寫入了），
而是**接觸本身**：每次列印都去叫醒 v4 驅動的設定模組，再在 3 秒逾時把 helper 殺在 COM 呼叫中間。

**`formResult` 對照表**（2026-08-06 起，見 [print-channel-electron.md](../blueprints/print-channel-electron.md) 決策 9b）。
只有 `exact` 會去動使用者的驅動設定，其餘全部是「什麼都沒做」：

| `formResult` | 意思 | 現場該做什麼 |
|---|---|---|
| `exact` | 找到同名表單、尺寸相符，**已設為預設紙** | 正常，不必動 |
| `mismatch` | 找到同名表單但**尺寸不符** → 刻意不設 | 依下方 runbook **重建那張表單**；本次請使用者在對話框手動選紙 |
| `not-found` | 驅動沒有這個名字的表單 | 依下方 runbook 建立（多半是名稱不完全一致） |
| `skipped-virtual` | 預設印表機是 Print to PDF／XPS／OneNote 之類 | 多半是使用者的預設印表機選錯了 |
| `skipped-printticket-reject` | 表單找到了，但**驅動不接受**我們組的紙張設定 → 刻意不寫（2026-08-08 起） | 這台就是不支援自動選紙，請使用者手動選。**不是故障**，`0x80010105` 正是硬寫下去的後果 |
| `skipped-printticket-unavailable` | 寫入前的檢查本身跑不起來（缺 `prntvpt.dll`／provider 開不了） | 同上先手動選紙。但**大量出現就要查**——那代表我們在很多機器上都放棄了自動選紙，看 `formError` 的 HRESULT |
| `skipped-printer-blocked` | 這台印表機曾經出過事，**我們已經永遠不碰它**（2026-08-10 起） | 正常防護。真的要重試，按列印排障列的「自動選紙」關再開（會清黑名單）|
| `skipped-over-budget` | 驅動太慢，我們已經不等了 ⇒ 不寫入 | 手動選紙。連續出現代表那台驅動反應很慢，通常也會一併被黑名單擋掉 |
| `skipped-disabled` | 使用者自己把「自動選紙」關掉了 | 正常，要恢復就按回「開」 |
| `skipped-viewer-open` / `skipped-helper-busy` | 已經有另一個列印視窗開著／上一次的 helper 還沒結束 | 正常行為（避免換掉那個視窗的紙、避免疊第二個驅動呼叫），稍候再印即可 |
| `unchanged` | 本來就已經是對的紙 | 正常 |
| `driver-rejected` / `error` / `helper-*` | helper 沒跑成功 | 看 `formError`／`win32`；列印本身不受影響 |

#### 現場印表機自訂紙張設定（IT 一次性，**每台 client 都要做**）

**這一步不做，等於白改**。Chromium PDF 檢視器是 **fit-to-printable-area 等比縮放置中**
（舊系統是 `DrawImage` 非等比拉滿整張紙，所以驅動裡是什麼紙都無所謂）。
用 A4 印 21×14.8 的資料卡 → 內容被縮小置中 → 位置全跑掉。

Windows：**控制台 → 裝置和印表機 → 選任一印表機 → 上方「列印伺服器內容」→「表單」頁籤 → 勾「建立新表單」**，
逐一建立下表 6 種（單位選公分，四邊邊界全填 0）：

| reportType | 表單名稱（**必須完全一致**） | 寬 × 高 |
|---|---|---|
| `datacard` | 資料卡 | 21 × 14.8 cm |
| `receipt` | 收據 | 21 × 29.7 cm |
| `tablet` | 薦牌 | 11.5 × 25.5 cm |
| `text` | 文牒 | 36.5 × 26.2 cm |
| `worship` | 普桌 | 21 × 29.6 cm |
| `worshipcard` | 普桌資料卡 | 21 × 14.8 cm |

⚠️ **表單名稱是契約，不是建議**（2026-08-04 起）：程式在開預覽視窗前會**依這個名稱**去驅動的紙張
清單比對，命中就把它設成預設紙張（＝舊系統 `SignupForm.cs:1770-1787` 的行為）。
名稱多一個空白、用了全形或別的字，就完全不會被選到 —— 使用者會退回「每次手動選紙」，
也就是 2026-08-04 那則客訴的狀態。名稱的唯一權威是 `ReportPageSizes.FormName`。

⚠️ **舊系統留下的同名 form 尺寸是錯的，必須重建**：舊程式寫死的是資料卡 201.7×142.2mm、
薦牌 115.1×254.0mm、文牒 348×251.5mm（舊系統靠點陣圖拉伸吃掉差異，見 [gotchas.md](../gotchas.md)），
1:1 送印後會變成真實裁切。收據與普桌的舊值本來就對，不必動。

⚠️ **尺寸錯的表單不會被自動選用**（2026-08-06 起）：名稱對但尺寸超過 ±0.5mm 容差時，程式**不會**
把它設成預設紙（`formResult` = `mismatch`），檢視器標題會提示「未自動選用…請手動選紙」。
先前的行為是「仍然選它」，改掉的理由是不值得為一張已知不對的紙去寫全域的每使用者預設 DEVMODE
（決策 9b）。**實務影響：舊系統留下的那三張表單沒重建之前，那三種報表就是每次手動選紙。**

尺寸比對容差是 **±0.5mm**（寬高各自判定）。**尺寸不符時程式仍會選它**（比停在 A4 好得多），
但會在檢視器視窗標題掛 ⚠ 警告、並在診斷紀錄寫 `formResult:"mismatch"` 與 `formMismatchMm`
——看到警告就是「這台的表單該重建了」。驅動裡根本沒有同名表單時是 `not-found`，同樣有警告。

⚠️ **滿版（邊界 0）報表的欄位離紙緣至少 0.5cm**：印表機的實體不可列印邊界會整欄吃掉更靠邊的內容
（已發生過薦牌客訴，見 [gotchas.md](../gotchas.md)）。這是硬體限制，設定 margin 0 也繞不過。

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

> **以實際 [frontend/electron-builder.yml](../../frontend/electron-builder.yml) 為準**（上為示意）。實際另含：`nsis.include: build/installer.nsh`（`preInit` macro 把預設安裝資料夾固定為 `$PROGRAMFILES64\Ceremony`，保留中文 productName）；`extraResources` 再加 `build/default-config.json → default-config.json`（出廠連線種子，缺檔自動略過）與 `build/prereqs`；icon 用 `build/icon.png`（由 logo.png 來）。

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

### 資料庫（MSSQL — 既有，schema 走 migration）

- **版本**：沿用既有（192.168.1.151 或 localhost）
- **Schema**：**可變更，走 DbUp migration**（2026-06-29 解除凍結，見 [database-design.md](database-design.md)）
- **ORM**：Dapper + 手寫 SQL；**Migration 工具：DbUp**（`Ceremony.Migrations`，部署時冪等執行）
- **備份**：見 [database-design.md](database-design.md) 備份章節
- **DB 帳號**：runtime 用應用專用帳號（最小權限，僅本 DB 表的 DML + EXEC backup proc，**無 DDL**）；**DbUp migration 於部署時用獨立高權限帳號執行**（目標設計）。

> **現況（2026-07-21）**：客戶端 migration 採方案 B——**API sidecar 啟動時自動跑 DbUp**（`Ceremony.Migrations` 隨 `Ceremony.Api` publish 打包；冪等、fail-fast、可 config `Migration:RunOnStartup=false` 關閉）。因此**啟動時會執行 DDL**（ALTER TABLE / CREATE VIEW）。目前客戶端用 `sa`（具 DDL）故可行——這是相對上述「runtime 帳號無 DDL」目標設計的**已知偏離**：若未來把 runtime 帳號降權，需改走方案 A（Electron 啟 sidecar 前以獨立高權限帳號跑 `Ceremony.Migrations.exe`）。詳見 [data-migration.md](../blueprints/data-migration.md)「Migration 如何在客戶端執行」。

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

### Release workflow（**已實作**：[.github/workflows/release.yml](../../.github/workflows/release.yml)）

打 `v*` tag（或手動 `workflow_dispatch`）→ 在 **`windows-latest`** 產出 NSIS 安裝檔：

1. `actions/setup-dotnet`（版本讀 [backend/global.json](../../backend/global.json)，目前 pin `10.0.103`）+ `actions/setup-node`（22，npm cache）
2. `npm ci`（frontend）
3. **從 GitHub Actions secret `DEFAULT_DB_CONFIG` 寫出 `frontend/build/default-config.json`**（出廠連線種子，含 sa 密碼；secret 不入 repo，CI 注入）
4. `pwsh backend/publish.ps1` → `backend/publish/win-x64/Ceremony.Api.exe`（framework-dependent sidecar）
   + `backend/publish/win-x64-printform/Ceremony.PrintForm.exe`（同腳本第二段，~2 MB，只需 .NET Runtime）
5. `npm run electron:build`（ng build + tsc electron）
6. `npx electron-builder --win --publish never` → `frontend/release/…-setup.exe`（種子 bundle 進 `resources/default-config.json`）
7. `actions/upload-artifact` + tag 觸發時 `softprops/action-gh-release` 附 `.exe` / `.blockmap` / `latest.yml`

**為何只在 Windows**：electron-builder NSIS target 需 Windows（mac/Linux 走 Wine 不穩、無法簽章）。**刻意不跑 `npm run dist`**（該 script 是 bash 寫法，Windows 原生殼會炸）→ 改上述分步。簽章未配置（無憑證）→ electron-builder 自動跳過。

> **CI secret 設定**：repo Settings → Secrets and variables → Actions 新增 `DEFAULT_DB_CONFIG`，值為 `default-config.json` 的 JSON 內容（`dbHost`/`dbPort`/`dbName`/`dbUser`/`dbPassword`）。未設則 CI build 不含種子 → 安裝後退回 `/setup` 手填（非錯誤）。
>
> 註：上方 `ci.yml` 仍為**規劃示意**（未建）；目前實作只有 release.yml。

部署：人工 promote staging → prod；prod 部署需 PR review + change ticket。

## 觀測（Observability）

| 層 | 工具 |
|---|---|
| Logging | Serilog → File (prod) / Seq (dev/staging) |
| Error tracking | Sentry（self-hosted in 寺方內網）or 文字 log 即可 |
| Tracing | OpenTelemetry → Jaeger（選用） |
| Metrics | Prometheus + Grafana（選用，內網一台機可省） |
| Health | `/health` endpoint + Electron 開機 ping |
| Audit | Serilog file log（現況不寫 audit_logs 表；DB 已可變更，查詢型審計表為待評估，見 [security.md](security.md)） |

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
        DB 多數情況不需動（migration 採向後相容；除非該版含破壞性 schema 變更）
T+20m → 驗證舊版可用
T+24h → 寫 post-mortem + 修正後重新部署
```

關鍵保護：**migration 採向後相容（只加不破壞）** → 新舊版 API 共用同一 DB，rollback 無資料遺失風險。破壞性 schema 變更須另備 down-migration 或延後到舊版下架。

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
| `Auth:SuperAdminEnabled` | true | true | **由業務決定** |
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
