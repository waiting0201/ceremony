---
title: Electron 包裝（Windows-only, Sidecar）
purpose: 把 Angular SPA + .NET sidecar API 打成單一 Windows NSIS installer，含軟體偵測 / DB 連線設定 / 備份下載
status: in-progress
applicable_when: 要動 Electron 殼、sidecar 啟動、首次設定流程、prereq 偵測、桌面打包時
related_agents:
  - software-architect-blueprint
  - frontend-architect
  - backend-engineer
related_docs:
  - ../design/infrastructure.md
  - ../design/security.md
  - ../design/api-design.md
  - api-endpoints/get-backup-download.md
keywords: [electron, sidecar, nsis, prereq, vc-redist, dotnet-10, config.json, 備份下載, framework-dependent, default-config, installer.nsh, autoHideMenuBar, 解除安裝, uninstall, 開始功能表捷徑]
last_updated: 2026-06-18 (開始功能表加解除安裝捷徑;寺方四項調整)
---

## 寺方部署調整（2026-06-02，已重新打包）

實機部署前依寺方要求做四項調整，皆 frontend/electron/打包層級（後端不動）：

1. **安裝資料夾固定英文 `Ceremony`**：保留中文 `productName`（app/捷徑名仍中文），用自訂 NSIS include [build/installer.nsh](../../frontend/build/installer.nsh) 的 `preInit` macro 把 InstallLocation 覆寫為 `$PROGRAMFILES64\Ceremony`；electron-builder.yml `nsis.include: build/installer.nsh`。使用者仍可在精靈手動改路徑。
2. **出廠預寫 DB 連線、跳過 `/setup`（default-config.json = 連線權威）**：寺方為**同機部署**（程式裝在 DB 主機 192.168.1.151 上），連線固定。bootstrap **每次啟動**讀打包種子 `default-config.json`（[config.ts](../../frontend/electron/config.ts) `readDefaultConfig`）以其連線覆寫 `%APPDATA%/Ceremony/config.json`（保留每機隨機 jwtKey）→ 直接 spawn sidecar；改種子後立即生效、也清掉殘留舊測試連線。種子 `dbHost = 192.168.1.151`（同機部署 → 連自身 IP）。種子缺檔則退回 `/setup`（保險）。種子含 sa 密碼 → **gitignore 不入 repo**，只 commit `default-config.example.json`；打包機本地填真實值並烘進 installer（`extraResources` → `resources/default-config.json`）。取捨見 [security.md](../design/security.md)。
3. **視窗移除選單列**：[main.ts](../../frontend/electron/main.ts) `Menu.setApplicationMenu(null)` + BrowserWindow `autoHideMenuBar: true`；保留標題列與最小化/關閉鈕。
4. **立即備份直接寫 `Backup:Directory`（D:\Backup，不選資料夾）**：同機部署，.bak 由 SQL Server 寫本機 D:\Backup 即可（[backup-page.ts](../../frontend/src/app/features/backup/backup-page.ts) `onBackup` = 確認→POST 備份→顯示結果）。**先前「先選位置再備份」已撤回**；[download.ts](../../frontend/electron/download.ts) 下載另存保留為備用能力（UI 未掛）。
5. **開始功能表加「解除安裝」捷徑（2026-06-18）**：electron-builder assisted installer 預設已產出 uninstaller exe + 控制台移除項，但**不建開始功能表的解除安裝捷徑**。[build/installer.nsh](../../frontend/build/installer.nsh) `customInstall` macro 在 `$SMPROGRAMS` 建 `解除安裝 ${PRODUCT_NAME}.lnk` 指向 `$INSTDIR\${UNINSTALL_FILENAME}`（與 app 捷徑同層，因未設 `menuCategory`）；`customUnInstall` macro 於真正解除安裝時刪除該捷徑。升級時舊 uninstaller 先靜默移除、customInstall 重建，不殘留。`config.json` 保留（未設 `deleteAppDataOnUninstall`，故升級與解除安裝皆不刪 DB 設定）。

### 🔴 備份必失敗的真因（2026-06-02 修正）

「資料備份」回 500 `BACKUP_NOT_CONFIGURED` 的根因**不是** D:\Backup 不存在，而是 [sidecar.ts](../../frontend/electron/sidecar.ts) `spawn` 未設 `cwd` → single-file exe 的 **ContentRoot 取自工作目錄**（變成 Electron cwd）→ **appsettings.json 未載入** → `Backup:Directory` 為 null。修法：`spawn` 帶 `cwd = resources/api`（exe 同層含 appsettings.json）。詳見 [gotchas.md](../gotchas.md)。另需 `D:\Backup` 存在且 SQL 服務帳號（`NT Service\MSSQLSERVER`）可寫。已用打包 bundle 實測 `POST /backup` 200、寫出 ~103MB .bak。

## 背景與動機

Angular SPA + .NET API 主流程完成後，最後一步把系統包成寺方 client 可直接安裝的桌面程式。寺方全是 Windows、內網、無公雲。採 **sidecar pattern**：每台 client 自帶一份 API instance，連集中 MSSQL 主機（架構詳見 [infrastructure.md 部署型態](../design/infrastructure.md)）。

本階段同時補三個只有桌面殼才能做的需求：
1. **軟體偵測**：開機檢查 VC++ Redistributable + .NET 10 ASP.NET Core Runtime。
2. **備份下載另存**：`.bak` 由 DB 主機端寫，瀏覽器選不了本機路徑 → Electron 原生「另存新檔」。
3. **DB 連線字串設定**：首次啟動引導頁填連線資訊 → 寫 `%APPDATA%/Ceremony/config.json`。

## 範圍

### 做什麼
- Electron 殼（[frontend/electron/](../../frontend/electron/)）：`main` 生命週期 + 動態 port spawn sidecar + `/health` ready check + `before-quit` kill。
- 首次啟動導流：`/prereq`（軟體偵測）→ `/setup`（DB 連線設定）→ 連線成功載入主程式。
- 軟體偵測（`prereq.ts`）：VC++（registry）、.NET 10（`dotnet --list-runtimes`）；缺則引導安裝 / 開下載頁。
- DB 連線設定（`config.ts` + `/setup` 頁）：寫 `config.json`（純文字方案 C）+ 每機隨機 `jwtKey`。
- 備份下載（`download.ts` + 後端 `GET /backup/{file}/download`）：原生另存 + 串流寫檔。
- 打包：[electron-builder.yml](../../frontend/electron-builder.yml) — win NSIS、extraResources 引入 sidecar exe、icon 來自 [reference/icons/logo.png](../../reference/icons/logo.png)。
- sidecar 發佈：**framework-dependent .NET 10**（[backend/publish.sh](../../backend/publish.sh) / `.ps1`）。

### 不做什麼
- 非 Windows target（mac/linux 不打包）。
- auto-update（electron-updater）：保留為後續（需內網 update server）。
- DB schema 變動：schema 腳本走 DbUp（`Ceremony.Migrations`），**經 Api ProjectReference 隨 sidecar publish 自動打包、由 Api 啟動時自動執行**（方案 B，2026-07-21），不需另外調打包配置。詳見 [data-migration.md](data-migration.md)「Migration 如何在客戶端執行」。
- Windows Authentication / DPAPI 加密 config（方案 C 已決策；保留升級路徑）。

## 使用者流程

```
首次啟動：
1. Electron main 偵測 prereq（VC++ / .NET 10）
2. 缺 → /prereq 頁：安裝 / 前往下載 → 重新檢查
3. 齊 → 無 config → /setup 頁：填 DB 主機/port/名稱/帳密 → 測試連線 → 儲存並連線
4. main 寫 config.json（產生 jwtKey）→ 動態 port spawn sidecar（注入 ConnectionStrings/Cors/JwtKey ENV）
5. 等 /health 200 → 帶 ?apiBase= 重新載入 renderer → 進登入頁

後續啟動：
1. 偵測 prereq → 讀 config → spawn sidecar → /health 200 → 載入主程式（自動連線）

備份下載：
備份頁「下載」→ 原生另存對話框 → main 以 Bearer GET download endpoint 串流寫到本機
```

## 設計決策

### 關鍵選擇
- **framework-dependent .NET 10（非 self-contained .NET 8）**：使用者要求偵測 .NET 10 → 內包 runtime 偵測無意義；省去 .NET runtime（~80MB），但因列印用 SkiaSharp 原生庫，`Ceremony.Api.exe` 實測仍 **~64MB**（非 ~10MB）。缺 runtime 由 prereq 引導。專案已 `net10.0`，無 TFM 遷移。publish 腳本移除 `libSkiaSharp.pdb`（~80MB）+ electron-builder `!**/*.pdb` 排除。
- **apiBase 動態傳遞**：sidecar 用動態 port；main 連線後帶 `?apiBase=` 重新 `loadFile`，[frontend/src/main.ts](../../frontend/src/main.ts) 在 bootstrap 前覆寫 `environment.apiBaseUrl`（各 `*.api.ts` 在 DI 建構時才讀，故有效）。
- **findFreePort() 取代 get-port**：`get-port` v7+ 為 ESM-only，與 CJS main 不相容 → 改用 node `net` 自寫（[sidecar.ts](../../frontend/electron/sidecar.ts)）。
- **每機隨機 jwtKey**：避免用 `appsettings.json` placeholder 當可預測簽章 key（見 [security.md](../design/security.md)）。
- **備份下載走 main 串流**：`.bak` ~100MB+，由 main `net` 串流寫檔避免 renderer blob 爆記憶體；瀏覽器 fallback 才用 blob。
- **啟動導流用 CanActivate guard**（[electron-ready.guard.ts](../../frontend/src/app/core/platform/electron-ready.guard.ts)）套在 `/login` 與 `''`；`/prereq` `/setup` 不套（避免循環）。瀏覽器模式 guard 直接放行。

### 取捨
- 取「IT 部署簡單」捨「本機 secret 強防護」（方案 C 純文字 config）。
- 取「installer 小 + 可偵測」捨「零 runtime 相依」（framework-dependent 需 client 裝 .NET 10）。
- 備份下載需 `Backup:Directory` 對 API process 可讀（prod 走 UNC）；dev docker 容器路徑讀不到 → 404（已知限制）。

## 跨層影響

| 層級 | 影響 | 變動摘要 |
|---|---|---|
| 視覺 | 是 | 新 `/prereq`、`/setup` 頁；備份頁加「下載另存」區塊 |
| 前端 | 是 | `src/main.ts` apiBase 覆寫；`core/platform/electron.ts` 橋接 + guard；`features/prereq` `features/setup`；`backup-page` 下載；`BackupApi.downloadUrl/fetchBlob`；routes 加 2 路由 + guard |
| 後端 | 是 | `GET /backup/{file}/download`（controller + handler + service + 契約）；`HealthController [AllowAnonymous]` |
| API | 是 | 新 endpoint（見 [get-backup-download.md](api-endpoints/get-backup-download.md)） |
| 資料庫 | 否 | 本功能不動 schema |
| 基礎建設 | 是 | framework-dependent publish；electron-builder.yml；prereq 偵測；ENV 注入（Conn/Cors/JwtKey） |
| 安全 | 是 | 下載 traversal 防護；每機 jwtKey；CORS null/file:// |

## 驗收標準

- [ ] `dotnet build` + 全測試綠（含 `IsValidBackupFileName` 18 case）
- [ ] `ng build` 0 warning（含 prereq/setup chunk）
- [ ] `electron:compile` 0 error
- [ ] dev（macOS）`electron:dev`：視窗載入、/setup 填 dev DB → 連線 → 進主程式、備份下載原生另存
- [ ] **Windows 實機（待驗）**：`npm run dist` 產 `寶覺寺法會報名系統-<ver>-setup.exe`；安裝 → prereq 偵測缺項引導 → setup → 連線 → 備份下載；桌面 icon = logo
- [ ] 通過 [code-review](../workflows/code-review.md) / [qa-testing](../workflows/qa-testing.md)
- [ ] design/ doc 已同步

## 風險與未解問題

- **Windows-only 驗證待補**：開發於 macOS，NSIS 產出 / registry prereq 偵測 / 實機安裝需 Windows 機驗證。
- **Backup:Directory 可讀性**：prod 須設 UNC 共用，否則下載 404；需在部署文件提醒 DBA。
- **客戶正式 .ico**：目前用 logo.png 由 electron-builder 產 .ico；客戶若提供原始 .ico 再替換。
- **auto-update 未接**：需內網 update server，後續再做。
- **後門帳號 sa@system.local / 明文密碼**：既有 pending 安全項（[status.md](../status.md)），與本階段獨立。

## 參考資料

- [infrastructure.md](../design/infrastructure.md)（部署型態 / 軟體相依偵測 / 備份下載 / 後端打包）
- [security.md](../design/security.md)（Sidecar DB 認證決策 / 下載 traversal / jwtKey）
- 實作：[frontend/electron/](../../frontend/electron/)、[electron-builder.yml](../../frontend/electron-builder.yml)、[backend/publish.sh](../../backend/publish.sh)
