---
title: Project Status
purpose: 追蹤本專案目前進度、待辦清單、已完成項目、阻塞項；每次任務開始/完成/卡住都要更新
applicable_when: 開始任何任務前要先讀本檔、收到新需求要新增、完成任務要移到 Done、卡住要移到 Blocked
related_agents: []
related_docs:
  - blueprints/README.md
  - workflows/feature-development.md
keywords: [status, 狀態, 進度, todo, backlog, in-progress, blocked, done, roadmap]
last_updated: 2026-07-05 (薦牌 OneOne 變體 Number/陽上/亡者 Y 座標修正 2cm Margin 偏移；debugOverlay 改用 page.Background()；亡者中心線置中)


---

> 本檔由 Claude **自動維護**。任務開始/完成/卡住都必須更新。新增項目也要寫入。詳細規則見 [../CLAUDE.md](../CLAUDE.md) 「狀態追蹤規則」。

**Current Version**: `v2.1.2`（SemVer；版號單一真實來源為 `frontend/package.json`，UI 自動連動；規範見 [conventions.md](conventions.md) 「軟體版本規範」）

## 🔄 In Progress

> 正在進行中的任務（一次最多 3–5 項，避免並行過載）

- 🔄 **Electron 包裝（Windows-only, Sidecar）+ 軟體偵測 + 備份下載 + DB 連線設定** — Started 2026-06-02
  - Blueprint: [electron-packaging.md](blueprints/electron-packaging.md)
  - Plan: 骨架 → 可打包 NSIS installer 全做完（使用者確認全範圍）
  - **新需求 3 項**：(1) 開機偵測 VC++ Redistributable + .NET 10 ASP.NET Core Runtime，缺了引導安裝；(2) 備份「下載 .bak 到 client 另存」（新後端 streaming endpoint + Electron 原生另存對話框）；(3) 首次啟動引導頁設定 DB 連線字串 → 寫 `%APPDATA%/Ceremony/config.json`
  - **架構決策（2026-06-02，取代舊敘述）**：sidecar 改 **framework-dependent .NET 10**（不再 .NET 8 self-contained）。理由：使用者要求偵測 .NET 10 runtime → 內包 runtime 偵測就無意義；framework-dependent 省去 .NET runtime（~80MB），但因列印用 SkiaSharp 原生庫，`Ceremony.Api.exe` 實測仍 ~64MB（非 ~10MB），靠 client 機器上的 .NET 10 ASP.NET Core Runtime（缺則由 prereq 偵測引導安裝）。已改寫 [infrastructure.md](design/infrastructure.md) 打包段
  - 桌面 icon：用 [reference/icons/logo.png](../reference/icons/logo.png)（1254×1254 方形現成 app icon）→ electron-builder 產 Windows .ico
  - **已完成（2026-06-02）**：
    - 後端 `GET /api/v1/backup/{file}/download`（traversal guard + 18 單元測試）、`/health` `[AllowAnonymous]`、[backend/publish.sh](../backend/publish.sh)/`.ps1`（framework-dependent .NET 10，移除 pdb）
    - Electron 殼 [frontend/electron/](../frontend/electron/)：`main`/`preload`/`config`/`prereq`/`sidecar`/`download` + `tsconfig.electron.json`
    - 前端：`src/main.ts` apiBase 覆寫、`core/platform/electron.ts`+guard、`features/prereq`、`features/setup`、備份頁下載 UI、`BackupApi` download
    - 打包：[electron-builder.yml](../frontend/electron-builder.yml)、package.json scripts/deps、icon from logo.png
    - **dev 驗證（macOS）**：dotnet build + 168+63 單元測試綠；`ng build` 0 warning；`electron:compile` 0 err；`publish.sh` 產 `Ceremony.Api.exe`(64M，無 runtime)；**`electron-builder --win --dir` 成功**（sidecar 入 resources/api、icon.ico 產出、無 pdb 洩漏）；prereq 偵測模組 mac→skip 正常
  - **✅ Windows 機打包成功（2026-06-02，本機 win32 10.0.26200）**：實機環境 node v24.13 / npm 11.6 / dotnet SDK 10.0.102；步驟（因 `dist` script 含 bash 語法，改 Windows 原生分步）：`npm install` → `pwsh backend/publish.ps1`（產 `Ceremony.Api.exe` 65MB、無 pdb）→ `npm run electron:build`（ng build 0 warning + tsc 0 err）→ `npx electron-builder --win`。產出 **`frontend/release/寶覺寺法會報名系統-2.0.0-setup.exe`（145MB，NSIS perMachine）**；sidecar 正確嵌入 `resources/api/Ceremony.Api.exe`、icon 由 logo.png 產出。code signing 跳過（無憑證，預期）。
    - **踩雷**：`backend/global.json` 指定 SDK `10.0.103`，本機只有 `10.0.102` → `dotnet publish` 起初印 SDK-not-found 警告但靠 rollForward 仍完成；建議把 global.json 降到 10.0.102 或設 `rollForward` 避免誤判
  - **✅ 寺方四項調整（2026-06-02，已重新打包）**：
    1. **安裝資料夾固定英文 `Ceremony`**：新增 [frontend/build/installer.nsh](../frontend/build/installer.nsh)（NSIS `preInit` macro 覆寫 InstallLocation 為 `$PROGRAMFILES64\Ceremony`）+ electron-builder.yml `nsis.include`；保留中文 productName/捷徑名
    2. **出廠預寫 DB 連線、跳過 /setup（連線權威 = default-config.json）**：bootstrap **每次啟動**讀打包種子 [frontend/build/default-config.json](../frontend/build/default-config.example.json) 以其連線覆寫 config.json（保留每機隨機 jwtKey）→ 改種子後立即生效、也清掉殘留舊測試連線。種子 dbHost = `192.168.1.151`（**同機部署**，程式裝在 DB 主機上 → 連自身 IP；安裝檔已帶此值）。種子含 sa 密碼 → **gitignore 不入 repo**（範例 `default-config.example.json`）；缺種子才退回 /setup。`config.ts` `readDefaultConfig`、electron-builder.yml extraResources 加 `default-config.json`
    3. **視窗移除選單列**：`main.ts` `Menu.setApplicationMenu(null)` + `autoHideMenuBar: true`（保留標題列）
    4. **立即備份直接寫 D:\Backup（不選資料夾）**：同機部署，.bak 由 SQL Server 寫本機 `Backup:Directory`（D:\Backup）即可；`backup-page.onBackup` 為單純「備份→顯示結果」（先前「先選位置再備份」已**撤回**，相關 download 另存改回備用、UI 移除）
  - **🔴 修掉「資料備份必失敗」真因**：`sidecar.ts` spawn 未設 `cwd` → single-file exe 的 ContentRoot 跑掉（變 repo root/安裝目錄）→ **appsettings.json 沒載入** → `Backup:Directory` null → 備份回 500 `BACKUP_NOT_CONFIGURED`。修：spawn 帶 `cwd = resources/api`。另 `D:\Backup` 須存在且 SQL 服務帳號可寫（dev 機已 `icacls` 授權 `NT Service\MSSQLSERVER`）
  - **✅ 實測（打包後 win-unpacked bundle，連本機 SQL；192.168.1.151 從 dev 機連不到故以 localhost 驗後端路徑）**：cwd 修好後 ContentRoot = `resources/api`、appsettings 載入、`POST /backup` **200** 寫出 ~108MB `.bak`；**`clearLog:true` 亦 200**（FULL recovery → 產 `.trn` log backup + SHRINKFILE、`logCleared=true`、`logClearError=null`）；`electron:build` 0 warning/0 err；`electron-builder --win` 成功、bundled 種子 = `192.168.1.151`
  - **⚠ 仍待實機驗證（需安裝執行）**：安裝精靈預設路徑 = `Program Files\Ceremony`、首次啟動不出現 setup 直接連線（config.json = 種子連線）、視窗無選單列、立即備份成功寫 D:\Backup、registry VC++ / .NET 10 偵測、實機列印
  - **⚠ 寺方 DB 主機需求**：`D:\Backup` 存在且 `NT Service\MSSQLSERVER`（或實際 SQL 服務帳號）可寫（舊系統已用此路徑，多半已具備）
  - **✅ prereq installers 已內建（2026-07-01）**：`frontend/build/prereqs/` 放妥 `vc_redist.x64.exe` + `aspnetcore-runtime-10-win-x64.exe`（gitignore 不進 repo，來源見 [infrastructure.md](design/infrastructure.md) 「軟體相依偵測」）；client 現場常無網路，改固定內建離線安裝檔，`launchInstaller` 不再落到 `openExternal`
  - **後續（不阻當前）**：auto-update（需內網 update server）

## 📋 Backlog

> 待辦清單，依優先級排序（P0 最高）

### P0 — 必做且急

- [ ] **回填 DB 技術數據** — 連本機 Ceremony DB 跑 8 段 SQL，把結果回填到 docs/
  - Why P0: 阻擋實作開工；其他都靠這些數據估算
  - Detail: 見 [pending-business-input.md](pending-business-input.md) §A
  - Output: 更新 [database-design.md](design/database-design.md)、[performance.md](design/performance.md)

- [ ] **召開業務需求確認會議** — 把 12 個業務問題集中問完
  - Why P0: 列印需求、訓練排程、合規策略都卡在這
  - Detail: 見 [pending-business-input.md](pending-business-input.md) §B
  - Output: 答案回填對應 blueprint / design doc

### P1 — 重要

- [ ] **建立 `Ceremony.Migrations`（DbUp）骨架** — DB 解除凍結後的 migration 管道
  - Why P1: 後續任何 schema 變更（密碼雜湊/索引/新表）都依賴此管道；先把骨架與部署整合做好
  - Detail: DbUp console/library 專案 + 版本化 `.sql` 嵌入資源 + 部署時冪等執行 + migration 專用高權限連線（與 runtime 帳號分離）
  - 原則: 變更須向後相容（並行運行期不破壞舊系統）；見 [data-migration.md](blueprints/data-migration.md)

- [ ] **待評估：DB 解禁後的具體 schema 變更**（各自獨立決策，非綁定）
  - `Admins.Password` 雜湊化（擴欄 + PBKDF2/Argon2）— 見 [security.md](design/security.md) #1
  - 大表搜尋索引（`Signups` 熱點欄位）— 先確認應用層手段不足，見 [performance.md](design/performance.md)
  - `audit_logs` / `login_attempts` 表、`Admins.Role`（RBAC）— 見 [security.md](design/security.md)
  - Why P1: 解凍主因多半是想做這些；但需逐項評估效益/相容性後才實作

- [ ] **普桌（Worship）陽上姓名不顯示** — 同薦牌的「全形 CJK 窄欄被 QuestPDF 靜默丟字」（陽上字級 2–3cm 在 2.2cm 欄寬）
  - Why P1: 普桌是目前 dev 主要資料型別，姓名完全沒印出來
  - Fix 方向: WorshipRenderer 比照 TabletRenderer 套 `StackVertical` + 不約束寬度；並以 worship RDLC 重新核對欄距/字級（3cm 字在 2.2cm 欄距是否重疊需確認）
  - 連帶: SkiaImageHelpers（文牒垂直地址）仍 `SKTypeface.FromFamilyName("BiauKai")`，建議改讀 `ReportFonts.ResolvedPath` 避免同類 fallback

- [ ] **列印精修：剩餘 variant 座標 + 實機驗收**（**2026-05-29 完成 4 項，見 Recently Done**）
  - ✅ 已完成：(a) Tablet 9 variant 各自 layout（座標權威抽自 9 個 .rdlc XML）(b) Text 2 variant 切換 + DeadName 座標 (c) worship2.png 背景嵌入（EmbeddedResource）(d) Text PhotoAddress 25×605px PNG（`SkiaImageHelpers.VerticalAddress`，移植 Library.cs）(e) DataCard dashed line（`SkiaImageHelpers.DashedLine`，SkiaSharp 自繪）
  - 仍待補：**Worship 6 variant 各自座標 layout**（本輪只做字級切換 + 背景，選定 4 項不含 worship 變體座標）

- [ ] **客戶實機列印驗收**（需印表機環境）— 客戶實際印 1 張 datacard / tablet / text / worship 對位後決定 Worship variant 精修優先序

- [ ] **列印對位 CI 自動量測（±0.05cm）**：把 PDF 渲染出的欄位座標自動量測比對 RDLC ground truth，避免人工目視驗收

- [ ] **列印模組可行性 PoC**（驗證 RDLC 直接重用 vs QuestPDF 重畫）
  - Why P1: 列印對位是最大風險；2026-05-27 新決策：先評估 RDLC 直接重用，不行才走 QuestPDF
  - 驗證項目：
    - (1) .NET Core RDLC 渲染套件評估（AspNetCore.Reporting trial / FastReport trial / Stimulsoft trial，含商用授權成本確認）
    - (2) 部署機（Windows Server）標楷體 / DFKai-SB 字型相依測試
    - (3) 印表機實機輸出對位（1 個薦牌變體）
    - (4) PDF byte stream 可否走 PDF.js 預覽 + 後端合併
  - 若全部通過 → 走 RDLC（19 個 .rdlc 直接 copy 到 backend project）
  - 若任一不通過 → 改 QuestPDF（用既有 [printing-reports-positions.md](blueprints/printing-reports-positions.md) 規範）
  - Output: PoC 報告 + 1 份對齊舊系統的 PDF + 實體列印對比照 + 技術選型決策回填 [printing-reports.md](blueprints/printing-reports.md)

### P3 — 後階段（Angular SPA 完成後再做）

- [ ] **Electron 包裝（Windows-only，Sidecar Pattern）** — Angular SPA + .NET API self-contained exe 打成單一 NSIS installer
  - Why P3: 桌面殼是最後一步；前期完全在瀏覽器開發
  - **架構決定 2026-05-28**：採 sidecar pattern；每台 client 含自己的 API instance，連集中 MSSQL 主機。詳見 [infrastructure.md 部署型態](design/infrastructure.md#部署型態2026-05-28-改為-sidecar-架構)
  - **DB 認證 = 方案 C（純文字 JSON config 存 `%APPDATA%/Ceremony/config.json`）**，trade-off 已記錄於 [security.md Sidecar 模式 DB 認證決策](design/security.md#sidecar-模式-db-認證決策2026-05-28)
  - 前置條件:
    - 客戶提供舊系統 .ico 放 [reference/icons/ceremony.ico](../reference/icons/)
    - DB 主機 IP / 帳密 / 防火牆規則確認（[pending-business-input §A](pending-business-input.md)）
    - `Backup:Directory` 改 UNC path（避免各 client 各自 .bak 散落）
  - 實作項目（預估 3–5 天）:
    - `electron/main.ts` spawn sidecar + 動態 port + ready check + before-quit kill
    - `electron/config.ts` 讀寫 `%APPDATA%/Ceremony/config.json` + 首次啟動引導頁
    - 後端加 `--urls` 動態 port 接收 + `/health` 端點（已有）
    - `electron-builder.yml` 含 `extraResources` 引入 `Ceremony.Api.exe`
    - Angular `main.ts` 從 `?apiBase=...` query string 覆寫 `environment.apiBaseUrl`
    - CORS 加 `null` / `file://` origin
    - **備份「選擇儲存位置」**（2026-05-29 延後到此）：browser SPA 無法選伺服器路徑/做原生資料夾對話框；待 Electron 殼以原生 dialog 選位置（或加 `GET /backup/{file}/download` 串流 .bak → 瀏覽器另存）。目前備份直接寫 `Backup:Directory`，前端不提供選位置 UI
  - Output: `寶覺寺法會報名系統-{version}-setup.exe`（約 180–220 MB）

### P2 — 想做

- [ ] **環境部署 4 項** — 部署位置/IP、update server、Code Signing、Sentry — 見 [pending §C](pending-business-input.md)
- [ ] **訓練導入 3 項** — 排程、並行期、緊急聯絡 — 見 [pending §D](pending-business-input.md)
- [ ] **Harness 自身**：Observability / HITL 章節化 / Iteration meta-rule / Cost tracking / doc-lint skill / PostToolUse hook
- [ ] **32 位元 Windows client 支援評估** — 2026-07-01 實測某台寺方機器為 32 位元，裝 x64-only 安裝包報「不是正確的 Win32 應用程式」
  - Why P2（先擱置）：工程量不小（後端多一條 `win-x86` publish、electron-builder 加 `ia32` target、另備 32 位元版 runtime 安裝檔、且 SkiaSharp/QuestPDF 原生庫是否有 win-x86 版未確認），且 32 位元 Windows 已停產多年，優先建議該 client 換 64 位元機器
  - Detail: 根因與決策見 [gotchas.md](gotchas.md)「安裝包是 x64-only」條目
  - 若之後真的有多台 32 位元機器（換機不可行）才評估重啟此項

## 🚧 Blocked

> 卡住中，需要外部資訊或決策才能繼續

- [ ] **薦牌（TabletRenderer）實體對位修正 — 主欄溢出已修正、亡者欄位改用中心線動態置中，仍待實機確認** — `reference/薦牌問題.pdf`
  - Blocker（已縮小範圍）: 客戶反映實際列印紙條插入蓮花瓶牌位座後文字對不準視窗；已排除座標搬移手誤、PDF 內部重疊/超出頁面邊界、往生字級疊字舊 bug 三種可能。2026-07-03 用新的 `debugOverlay` 疊 `reference/template/薦牌.jpg` 樣板照片量測，發現並修正了 Base 變體主欄可用高度（`deadFull`）確實比窗框內緣多出約 2.5cm 的 porting 落差；2026-07-05 使用者反映「亡者列印沒有很正」，改用「故／靈位」字符中心線為基準全面重寫亡者定位邏輯（動態算置中座標，取代編譯期固定常數）（見下方 Detail）——但樣板照片是否等於客戶目前實際使用的牌位座尚未確認，仍需實體量測才能結案
  - Waiting on: 使用者/現場人員印出修正後版本（可搭配 `TabletRenderer.Render(data, debugGrid:true)`）並插入同一個牌位座，回報視窗上緣/下緣對到第幾條 1cm 刻度線，確認這次修正方向正確或算出更精確的修正量
  - Since: 2026-07-02（2026-07-03、2026-07-05 部分修正）
  - Detail: [gotchas.md](gotchas.md)「薦牌實體對位」條、[printing-reports.md](blueprints/printing-reports.md)「薦牌實體對位開放問題」

## 📍 目前文件化進度（會話開始先讀這份）

**文件化階段：✅ 完整**。所有可由 code / 分析文件反推的內容已寫入。剩餘缺口集中於 [pending-business-input.md](pending-business-input.md)（27 項，需業務 / DBA / 客戶提供）。

**下一階段：實作期**。建議流程：
1. 業務確認會議（解鎖 [pending §B](pending-business-input.md)）+ DBA 跑 SQL（解鎖 [§A](pending-business-input.md)）
2. 啟動實作骨架（**Angular SPA 先**：純瀏覽器版 + 後端 Dapper API）
3. 列印模組 PoC 先做（風險最高）
4. 主流程一個一個搬：登入 → 信眾 → 報名 → 預繳 → 列印 → 報表（皆在瀏覽器測試）
5. **最後階段才包 Electron（僅 Windows）**：套用舊系統 .ico
6. 並行運行 → 全切換 → 舊系統下架

### 已產出文件清單（46 份）

| 類別 | 檔案 | 狀態 |
|---|---|---|
| 入口 | [CLAUDE.md](../CLAUDE.md) | ✅ |
| 路由 | [docs/status.md](status.md) / [glossary.md](glossary.md) / [business-rules-implicit.md](business-rules-implicit.md) / [pending-business-input.md](pending-business-input.md) | ✅ |
| Design × 8 | visual / frontend / backend / api / database / performance / security / infrastructure | ✅ |
| Blueprints × 7 | auth-and-admin / believer / category / signup / prepay / printing / printing-positions / data-migration | ✅ |
| Workflows | qa-testing / user-training / RPEV / feature-dev / bug-fix / code-review | ✅ |
| Reference 探索報告 × 7 | scratch/explore/01..07.md | ✅ |
| RDLC 資源 | reference/extracted-images/（worship2.png 等）| ✅ |

## ✅ Recently Done

> 最近完成的項目（保留最近 10 項或 30 天，滿了搬到 Archive）

- [x] **報名維護：勾選多筆只精準列印勾選的那幾筆** — Done 2026-07-03
  - 需求來源：使用者問「報名維護的資料可以勾選，勾選好後，能只列印勾選的嗎」；查現況發現 grid 已有 checkbox 多選（shift-click 範圍選取），但多選列印會退化成「編號 min~max 區間批次列印」，編號不連續時會多印非選取列（v1 既知限制，見 [signup-management.md](blueprints/signup-management.md) 舊版「多筆列印實作策略」段）。使用者選擇：後端加 `signupIds` 批次列印（而非前端逐筆呼叫單筆 API 再合併）
  - 做法：`POST /api/v1/reports/batch` 的 `BatchReportRequest` 加 `SignupIds`（`IReadOnlyList<Guid>?`），有值時優先於 `numberStart`/`numberEnd`；`ISignupRepository` 加 `SearchByIdsAsync`（`WHERE SignupID IN @Ids ORDER BY Number`）；worship 過濾規則（只印 SignupType=4）在兩種模式下一致。前端 `signup-list-page.ts` 的 `actionPrint` 改為多選時直接送 `signupIds`，移除原本「將列印編號 X–Y…含非選取 N 筆」的確認對話框（不再需要，因為現在真的精準）
  - 驗證：後端新增 6 個單元測試（`BatchReportHandlerTests`）+ 2 個整合測試（`ReportsEndpointsTests`，含真實 DB），`dotnet test` 全綠；前端 `ng build` 0 warning/0 error
  - 文件同步：[post-reports-batch.md](blueprints/api-endpoints/post-reports-batch.md)、[signup-management.md](blueprints/signup-management.md)「多筆列印實作策略」、[api-design.md](design/api-design.md) Reports/Print 表

- [x] **開發用列印位置檢視工具（文牒/資料卡/薦牌樣板疊圖）** — Done 2026-07-03（內部 dev-only 工具，不影響對外版本號）
  - 需求：延續薦牌既有 `debugGrid` 格線校正工具的精神，讓開發人員能疊上 `reference/template/` 的實體樣板掃描照直接肉眼比對列印欄位對不對齊，涵蓋文牒/資料卡/薦牌三種報表
  - 做法：比照 `WorshipRenderer` 既有的 EmbeddedResource 背景圖手法，`DataCardRenderer`/`TextRenderer`/`TabletRenderer` 的 `Render(...)` 加 `debugOverlay` 參數；`ReportsController` 的 3 個 GET endpoint 加 `?debugOverlay=true`，僅 Development 環境放行（其他環境回 404）；不加前端 UI
  - 素材：`reference/template/{文牒,資料卡,薦牌}.jpg`（200 DPI 掃描），資料卡樣板有 EXIF 側拍已用 Pillow 轉正
  - **附帶發現**：疊圖後直接印證了下方 Blocked 項「薦牌實體對位」的客訴——Base/OneOne 變體文字貼近甚至超出雕花窗框邊緣；另外資料卡的欄位標籤與樣板紙已印標籤重複繪製、略為錯位
  - 驗證：`dotnet build`/`dotnet test` 全數通過；`RendererSmokeTests.cs` 新增 `DebugOverlay` 回歸測試；`CEREMONY_PDF_DUMP` 落地後用 `pdftoppm` 轉圖目視確認三種報表疊圖皆正確對齊且方向正確
  - 文件同步：[printing-reports.md](blueprints/printing-reports.md)「開發用列印位置檢視工具」、[api-design.md](design/api-design.md) Reports/Print 表註記
  - **追加（2026-07-03）用樣板照片量測薦牌，修正一個確定的 bug**：對「附帶發現」的薦牌窗框溢出做像素分析（量出雕花窗框內緣 Y: 6.2294~16.0782cm），確認 Base 變體主欄可用高度 `deadFull=11.0331cm` 確實比窗框內緣多出約 2.5cm（14 字以上長名字會印到窗框外）——這是可從量測值直接反推的 porting 落差，經使用者確認後改為量測值 `deadFull=8.4957`，回歸鎖 `Tablet_Base_LongDeadName_StaysWithinMeasuredWindow`；仍歸類在下方 Blocked（樣板照片是否等於客戶實際牌位座尚待實機確認）
  - **追加（2026-07-03）資料卡改版，已結案**：量測發現樣板照片沒有「亡者」欄、也沒有堂號（HallName）欄，樣板第一個欄位「陽上：」實際在 Top≈2.69cm（原程式碼畫在 4.707cm），樣板右側印有跟薦牌同款「故◯◯靈位」窗框圖案。經使用者確認改版方向（Number 留左、預繳留右、堂號不印、亡者改印進右側窗框）後：`DataCardData`/`DataCardModel`/`ReportModelBuilders.DataCard` 移除 `HallName`；新增 `DrawDeadNamesInWindow`（比照 TabletRenderer 直書堆疊 + GroupFontPt 縮字，多位亡者用「、」串接塞進窗框缺口）；陽上整段上移對齊樣板；移除失去意義的虛線分隔。因資料卡是平面 A5 紙（不像薦牌要塞實體 3D 牌位座），量測值可直接定案不需等實機測試。回歸鎖 `DataCard_MultipleDeadNames_StayWithinMeasuredWindow`。詳見 [printing-reports.md](blueprints/printing-reports.md)「資料卡改版」
  - **再追加（2026-07-03）拿掉重複標題**：使用者指出樣板紙本身就已經預印每個欄位的標題文字，程式不需要再印一次。拿掉「陽上：」「地址：」「電話：」「備註：」「確認無誤請簽名：」5 個標題 `DrawText` 呼叫 + 簽名底線 `Line1`（樣板已印），程式現在只印欄位內容；順手清掉因此變成死碼的 `DrawLine` method 與 `DrawText` 的 `vAlign`/`VerticalAlign` 參數。用 `CEREMONY_PDF_DUMP` + `pdftoppm` 目視確認疊圖後不再有雙重疊字，PDF 複製存於 `reference/output/`（`.gitignore` 排除，不進 repo）供直接開啟檢視。`dotnet test` 309 個測試全數通過
  - **再追加（2026-07-04）使用者指定版面微調**：陽上改 3 排 × 2 欄（6 字寬 4.8cm，1st→第一排、2nd/4th→第二排前/後、3rd/5th→第三排前/後）；地址上移 1cm、備註下移 0.5cm，兩者皆縮寬到 10.4cm 避開右側樣板窗框、可換行不裁切；亡者窗框內文字再靠右 0.3cm。新增回歸測試 `DataCard_FiveLivingNamesAndWrappedText_DumpsCalibrationPdf`（5 位陽上 + 刻意寫長地址/備註觸發換行），疊圖確認新版面互不重疊，PDF 存於 `reference/output/datacard_five_living_wrapped_overlay.pdf`。`dotnet test` 310 個測試全數通過
  - **再追加（2026-07-05）使用者再指定版面調整**：地址/電話/備註改對齊陽上的方式（直接對齊樣板量到的標題「上緣」：地址 6.4135、電話 8.8392、備註 9.8679，取代前一版用位移量推算的座標）；亡者改成跟薦牌一樣的 2×3 矩陣（1st 中間上、2nd 右邊上、3rd 左邊上、4th 右邊下、5th 左邊下、6th 中間下，完全比照 `TabletRenderer.DrawDeadNames`），並整體往下 0.1cm、往左 0.1cm。**踩雷**：窗框內緣只有 2.9845cm 寬塞不下 3 欄用 0.8cm 字級（短名字時 `GroupFontPt` 不會縮，欄距 0.75cm 幾乎貼在一起），改把這個窗框專用字級基準降到 0.6cm 才留得出間隙——用 `甲乙丙丁戊己` 6 個相異單字疊圖才看得出這個問題（用同姓氏測試資料會誤判成正常）。新增回歸測試 `DataCard_SixDeadNames_MatrixStaysWithinMeasuredWindow`、`DataCard_SixDistinctDeadNames_MatrixColumnsDoNotTouch`、`DataCard_OneDeadName_MatrixCenterTopRenders`；PDF 存於 `reference/output/`。`dotnet test` 312 個測試全數通過

- [x] **薦牌（TabletRenderer）Number 位置微調** — Done 2026-07-05
  - 需求：使用者提出薦牌 3 項修改，經確認後只有第 3 項需要動 code——亡者「中間上/右邊上/左邊上/右邊下/左邊下/中間下」矩陣（3 位以上變體）使用者確認現況已符合、不用改；陽上排法使用者要求維持參照舊系統 RDLC 座標、不重新設計；Number（掛號）位置從左上角原點往下、往右各移 0.1cm
  - 做法：`TabletRenderer.cs` 的 Number `DrawText` 呼叫從 `(0.0, 0.0)` 改成 `(0.1, 0.1)`，9 個變體共用同一行程式碼，全部套用
  - 驗證：`dotnet test` 312 個測試全數通過；用 `debugOverlay` 疊圖 + 裁切左上角目視確認「郵1」不再貼齊紙張邊緣
  - 文件同步：[printing-reports.md](blueprints/printing-reports.md)「薦牌實體對位開放問題」、[printing-reports-positions.md](blueprints/printing-reports-positions.md) §3
  - **追加（2026-07-05）使用者反映「薦牌亡者的列印沒有很正」**：第一輪用 `debugOverlay` 疊圖逐一實測（不是憑感覺）發現 Two 變體 `DeadNameTwo Left=4.2` 跟窗框內緣（量測 4.191cm）幾乎貼齊、Base 變體 `DeadNameThree/Five Left=4.0` 更嚴重直接印到邊框外，先用固定值 `Left=4.34`／`4.25` 修正
  - **再追加（2026-07-05）使用者給出更完整規則，全面改版取代固定值**：以樣板紙「故」「靈位」兩組靜態字的字符中心線為排版基準——1 位亡者完全置中、2 位分居中心線左右、3+ 位沿用 2×3 矩陣但中間欄置中在中心線。量出中心線 `X=5.685cm`（「故」「靈位」bounding box 中心幾乎重合，跟窗框幾何中心互相印證），並疊回無渲染文字的原始樣板照片核對精確貫穿兩組字視覺中心。**方法論改變**：位置改成「先算 `GroupFontPt` 共用字級，再動態算置中座標」，取代「編譯期固定常數、不管字級縮多小位置都不變」的舊做法，一併解決 Base 變體先前「只能剛好清邊框」的取捨疑慮。新增回歸測試 `Tablet_OneDeadName_DumpsCenteredOverlay`；`dotnet test` 314 個測試全數通過；PDF 存於 `reference/output/{tablet_one_dead_centered_overlay,tablet_two_variant_overlay,tablet_base_three_dead_overlay}.pdf`
  - **三追加（2026-07-05）「只有一位時，亡者位置沒在故靈位正中間」，第一次改法猜錯方向**：誤判成「Y 座標也要整體置中在故～靈位空隙裡」（`Top = 故下緣 + (空隙高度 − 文字高度) / 2`），量出故下緣 `Y=7.5946cm`／靈上緣 `Y=13.462cm`（空隙 5.8674cm）套用。收緊 `GroupFontPt` avail 至實測空隙這項改動是對的、有保留。
  - **四追加（2026-07-05）使用者糾正「還是不對 要在故的正下方」**：原意是水平方向在中心線上，垂直方向要**緊接在故正下方**，不是整塊漂浮置中在空隙中間。改回 `Top = 故下緣量測值 7.5946`（跟改版前的舊值 7.5825 幾乎相同，等於只補上水平置中、垂直維持原邏輯）。`dotnet test` 314 個測試全數通過（無新增測試，沿用同一個回歸測試驗證新結果）；PDF 已更新至 `reference/output/tablet_one_dead_centered_overlay.pdf`
  - **五追加（2026-07-05）使用者確認實體紙張尺寸為 11.5×25.5cm**：原 RDLC 值 25.4cm 高度少了 0.1cm。`TabletRenderer.PageHeightCm` 改為 `25.5`（9 個變體共用同一常數，全部套用），順手把 `page.Size(...)` 呼叫跟 `DrawCalibrationGrid` 內原本重複寫死的 `25.4f`/`11.5f` 都改成讀取 `PageWidthCm`/`PageHeightCm` 常數，避免下次改頁面尺寸又漏改其中一處。所有欄位座標都是絕對值、不受頁高影響，只補足頁尾多出的 0.1cm 空白。`dotnet test` 314 個測試全數通過（無需新增測試）；重新產出全部薦牌疊圖 PDF 確認頁面比例正確、樣板疊圖仍對齊
  - **六追加（2026-07-05）使用者反映「只有一位往生者的 template 引用有問題，template 變比較小，上方跟右方有一大片留白」，抓到真的 bug**：三個 renderer（DataCard/Text/Tablet）的 debugOverlay 最初都用 `.Image(TemplateImage).FitArea()`（保留原圖比例、留白），但樣板掃描照實際比例跟假定的頁面 cm 比例對不上，尤其**薦牌 OneOne 變體**（內容區 11.5×21.5cm，比例 1.87）跟樣板原生比例（2.23）落差最大，量測確認疊圖只填滿容器寬度的 83.7%、右側留白達 16%。改用 `.FitUnproportionally()`（直接拉伸填滿容器，忽略原圖比例）——這個工具本來就是假設「樣板照片＝我們的 cm 座標系統」去比對位置，非等比縮放反而更符合這個假設。三個 renderer 都受影響、都已修正（不只薦牌）。修正後像素量測確認全部疊圖填滿容器寬高達 99.8%+。`dotnet test` 314 個測試全數通過（無需新增測試）；PDF 已更新至 `reference/output/`
  - **七追加（2026-07-05）使用者追問「上下也（跟）右有留白，再確認一下」，第一次判斷「這是 QuestPDF 限制非 bug」**：OneOne 變體疊圖上下緣各留 118px（≈2cm），對應 `tmpTabletOneOne` 的 Page Margin 設計。試過負值 `TranslateY(-2cm)` 想蓋滿整張紙，但在 `page.Content().Layers(...)` 底下操作會被 Margin 整層裁掉（回歸測試抓到：疊圖版 PDF 位元組數跟不疊圖版完全一樣），因此先下結論「技術限制、改不了」，改回原本版本。
  - **八追加（2026-07-05）使用者不接受「限制」的說法、指示參考 3 位亡者（無 margin）疊圖方式，重新查出正確做法**：關鍵在於先前只試了 `page.Content()` 內的路徑，沒有進一步查 QuestPDF 是否有繞過 Margin 的其他 API。改用 `page.Background(...)`——這是畫在「整張實體紙」座標系統，不受 `page.Content()` 的 Margin 影響；`layers.PrimaryLayer().Background(...)` 在 `debugOverlay=true` 時改用 `Colors.Transparent`（保留呼叫維持 Layers 容器尺寸，只是不再蓋白底擋住 Background 疊的樣板照片）。修正後像素量測確認 OneOne 疊圖四邊留白全部歸零，完整看到牌位圖案。`dotnet test` 314 個測試全數通過；PDF 已更新至 `reference/output/`。**教訓**：「這是第三方套件的技術限制」這個結論下得太早——只證明了一條路走不通，不代表沒有其他路徑；使用者的直覺（3 位亡者疊圖沒問題，代表 template 引用方式本身沒問題，問題出在別處）是對的
  - **九追加（2026-07-05）疊圖修好、蓋滿整張紙後，露出更早就存在的排版 bug**：使用者指出「留白可以了，但是 y 軸的位置不對，請參考三位亡者的 y 軸位置」，點名 Number、陽上、亡者三處。用 cm 尺標疊在渲染結果上精確量測，確認根因：這三處分別跟 TwoOne/UnderscoreOne/OneTwo/One 等「沒有 Page Margin」的變體共用同一個座標常數（`Number Top=0.1`、`LivingNameOne Top=14.00389`、`DeadGapTop=7.5946`），但只有 OneOne 有 2cm Margin，`page.Content()` 座標原點比真實頁面頂端低 2cm，OneOne 印出來的實體位置因此比其他共用同一常數的變體低了 2cm——這個錯位其實從一開始就存在，只是被「舊版疊圖只顯示裁切後內容區」巧合遮住，這輪蓋滿整張紙才第一次真正看見。三處都加上 `data.Template == TabletTemplate.OneOne ? 2.0 : 0.0` 的補償，content-Y 減去這個值。**技術細節**：原本擔心負值 Y（如 `0.1-2.0=-1.9`）會像先前 Image 疊圖一樣被 QuestPDF 整層裁掉，但實測**文字元素不會被裁**，只有 `Image` 疊圖那條路徑會——同一個 Margin 座標系統，不同元素類型的裁切行為不一樣。`dotnet test` 314 個測試全數通過；PDF 已更新至 `reference/output/`

- [x] **新增報名「選擇信眾」picker 對齊舊系統** — Done 2026-07-02，**更版 v2.1.2**（PATCH：沿用既有 endpoint，僅前端行為/樣式對齊）
  - 需求：使用者要求 signup-edit-form 的信眾 picker 搜尋方式與清單欄位要跟舊 `NewSignupForm` 完全一樣
  - 落差：舊系統單一輸入框 OR 比對 14 欄（Name/Phone/6組陽上/6組往生），清單每筆報名一列共 16 欄；新版原本只搜 Name、每信眾一列簡化卡片
  - 做法：**不新增後端功能**——改用既有 `GET /api/v1/signups`（`SignupApi.search` + `scopeName/scopePhone/scopeLivingName/scopeDeadName`）取代 `/believers` 搜尋，回傳的 `SignupListItem` 本已含全部所需欄位；選定後仍用既有 `GET /believers/{id}` 補完整明細（zipcode ID 等）供表單預填
  - 詳見 [signup-management.md](blueprints/signup-management.md)「新增（NewSignupForm）」段落與 [legacy-coverage/new-signup-form.md](blueprints/legacy-coverage/new-signup-form.md) row 3/4/24
  - 已知落差：`/signups` 未暴露 `CeremonySort`，無法 100% 重現舊排序，前端以整體反轉近似
  - **追加修正（2026-07-02）**：placeholder 改「輸入姓名、電話、陽上名或往生名...」（原文寫「信眾姓名」易誤導只搜姓名）
  - **追加修正 2（2026-07-02）**：搜尋改回「按鈕觸發」（原本 `(input)` 事件無論有無 debounce 都是「打字就打 API」，使用者反映感覺慢）——對齊舊 `btnBelieverSearch_Click` 語意，文字輸入只更新框內顯示，按「搜尋」鈕或 Enter 才真正查詢；新增 `believerHasSearched` 旗標修正「無符合資料」提示的觸發時機（避免打字未按搜尋就誤顯示）
  - **追加修正 3（2026-07-02）**：`--c-dead-name-bg`（往生欄底色，全域 token）從 `#EFDCC4` 改成 `#E3B274`——原色跟 grid hover 色（`--c-row-alt`/`--c-primary-soft` 這類淺米色）太接近分不清，此為全域樣式變數，believers-page / signup-list-page 等其他頁面的往生欄也一併變深，見 [visual-design.md](design/visual-design.md)
  - **追加修正 4（2026-07-02）**：picker「搜尋」按鈕高度跟 `.search-input` 對不齊（`.btn`/`.btn-sm` 預設高度都跟 `--control-height` 不同），改為 scoped override 明確對齊；hover 往生欄「沒反應」——因 `.dead` 自己有底色蓋掉 `tr:hover` 的背景，比照既有 `is-selected + dead` 的 `color-mix` 手法，讓 hover 時整列（含往生欄）都一起變色
  - 驗證：`ng build` 0 warning/err、`ng test` 通過；**仍待**實機手測：搜尋 14 欄命中 + 多筆報名重複列顯示 + 搜尋改成按鈕觸發（打字不再自動查）+ 搜尋按鈕高度與輸入框對齊 + hover 整列（含往生欄）都變色

- [x] **prereq 離線安裝檔內建（VC++ Redistributable + .NET 10 ASP.NET Core Runtime）** — Done 2026-07-01，**更版 v2.1.1**（PATCH：僅打包資源調整，無程式邏輯變更）
  - 背景：實機打包測試時 prereq 頁偵測到缺 ASP.NET Core Runtime 10.x（[frontend/electron/prereq.ts](../frontend/electron/prereq.ts) 判斷正確、非 bug，見 [gotchas.md](gotchas.md)），使用者要求現場安裝不依賴連網下載
  - 做法：把 `vc_redist.x64.exe`（[aka.ms/vs/17/release/vc_redist.x64.exe](https://aka.ms/vs/17/release/vc_redist.x64.exe)）與 `aspnetcore-runtime-10-win-x64.exe`（[aka.ms/dotnet/10.0/aspnetcore-runtime-win-x64.exe](https://aka.ms/dotnet/10.0/aspnetcore-runtime-win-x64.exe)）放入 `frontend/build/prereqs/`（既有 gitignore + electron-builder.yml extraResources 機制，本身無需改 code）；已驗證兩檔為合法 Windows PE 執行檔
  - 文件同步：[infrastructure.md](design/infrastructure.md) 「軟體相依偵測」補來源連結與決策
  - **注意**：兩檔不進 repo，新機器/CI 打包前須重新放置（見 infrastructure.md 提醒）

- [x] **重複報名警示：同信眾同年同法會即時提示（不阻擋）** — Done 2026-06-30，**更版 v2.1.0**（MINOR：新增 endpoint + UI，向後相容）
  - 需求：新增/編輯報名時，若選定信眾在同一 `(Year, CeremonyCategoryID)`（**忽略報名類型**）已有報名 → 跳警示，但**仍可報**
  - 後端：新增唯讀 `GET /signups/duplicates`（`CheckSignupDuplicatesHandler` + `SignupRepository.FindDuplicatesByBelieverAsync`，直接查 `dbo.Signups` 5 欄，編輯帶 `excludeSignupId` 排除自身）
  - 前端：`SignupApi.checkDuplicates` + `signup-edit-form` 以 `combineLatest`(year/法會/信眾，debounce 300ms) 即時觸發 → 信眾區塊下 `.alert-warn` 逐筆列「編號·報名類型」；`submit()` 不變
  - 決策：判定**忽略 SignupType**（會把同年同法會跨類型的既有合法情境也提示，刻意）、比對精確 CeremonyCategoryID（同 §1.2 粒度）、僅警示不影響 409 編號衝突
  - 驗證：`dotnet build` 0 err、`npm run build` 0 err；**仍待**端到端手測
  - 文件同步：[get-signup-duplicates.md](blueprints/api-endpoints/get-signup-duplicates.md)（新藍圖）、[api-design.md](design/api-design.md) endpoint 表、[signup-management.md](blueprints/signup-management.md) 新增/編輯段、[business-rules-implicit.md §1.4](business-rules-implicit.md)、legacy-coverage new/edit-signup-form 註記新版增強

- [x] **薦牌／文牒印滿第 6 位往生/陽上（修正 legacy 缺陷）** — Done 2026-06-30
  - 背景：舊系統第 6 位可登錄/存 DB/搜尋但列印被默默丟掉（11 個 RDLC 變體皆無第 6 格 textbox）；新 renderer 沿用同缺陷只畫 `[0..4]`
  - 實作：`TabletRenderer`(往生 default + 陽上 Two/One/Base) + `TextRenderer`(往生 tmpText + 陽上 inline) 共 4 組補 `[5]`，座標補矩陣空位、納入 `GroupFontPt` 統一字級分組（主名 `Avail` 改看第 6 位、無第 6 位時行為不變＝向後相容）
  - 座標：薦牌往生 `9.4464/4.9`、薦牌陽上 `15.44174/1.56167`、文牒往生 `5.72264/12.41251`、文牒陽上 `17.25916/21.87382`（見 [business-rules-implicit.md §18](business-rules-implicit.md)）
  - 驗證：solution build 0 err；Infrastructure 65 測試綠（新增「只填第 6 位 vs 全空」回歸鎖隔離 `[5]` 渲染）；pdftotext + pdftoppm 影像確認薦牌/文牒往生+陽上各 6 位全印出、第 6 位落在矩陣空位
  - **仍待**：實機預印紙對位驗收（±0.2cm，需印表機環境）

- [x] **「列印普桌」啟用條件改看選取列（與搜尋篩選解耦）** — Done 2026-06-29
  - 需求：即使搜尋篩選非普桌，也要能直接挑普桌資料列印；但要驗證、不能出 bug
  - 改法：右鍵「列印普桌」由 `filters.signupType === 4` 改為 `selected.every(r => r.signupType === 4)`；混選含非普桌即 grey out + tooltip「選取含 N 筆非普桌資料」（[signup-list-page.ts](../frontend/src/app/features/signups/signup-list-page.ts) `buildPrintItem`；同時移除 `MenuContext.signupTypeFilter` 死碼）
  - 無 bug 依據：後端防呆未動且本就是真正守門 — 單筆 by-id 驗證 `type=4`（[GenerateReportHandlers.cs:121](../backend/src/Ceremony.Application/Reports/GenerateReportHandlers.cs)）、批次 `BatchReportHandler` 強制 `type=4`（[BatchReportHandler.cs:25-26](../backend/src/Ceremony.Application/Reports/BatchReportHandler.cs)）；前端只放行純普桌選取
  - 驗證：`tsc -p tsconfig.app.json --noEmit` 0 err；無殘留 `signupTypeFilter` 參照
  - 文件同步：[business-rules-implicit.md §16](business-rules-implicit.md)（淘汰舊規則 + 記錄 why-safe）、[frontend-design.md](design/frontend-design.md) 選單啟用表、[signup-management.md](blueprints/signup-management.md)（選單表/啟用條件/2 checklist）
- [x] **解除「DB 完全凍結」限制，改為可走 migration（DbUp）** — Done 2026-06-29（決策 + 文件同步）
  - 沿革：2026-05-26 客戶曾裁定「DB 完全凍結、密碼明碼、無 migration」（status 下方 Archive 有記錄）；**2026-06-29 解除**，schema 可變更、走 **DbUp** 版本化 `.sql` migration（不導入 EF Core Migrations，ORM 維持 Dapper）
  - 範圍：本次只「解除原則 + 引入 migration 工具」；具體變更（密碼雜湊、加索引、audit/login_attempts 新表、RBAC role 欄位）改列為**待個別評估**的 backlog，不一次拍板
  - 原則：runtime DB 帳號仍**無 DDL**，migration 於部署時用獨立高權限帳號執行；schema 變更須**向後相容**（並行運行期舊系統仍讀寫同一 DB）
  - 文件同步：[database-design.md](design/database-design.md)（單一真實來源，重寫決策段 + 技術選型 + 索引段）、[backend-design.md](design/backend-design.md)（Migration→DbUp、加 `Ceremony.Migrations` 專案）、[data-migration.md](blueprints/data-migration.md)（un-deprecate、改 active）、[performance.md](design/performance.md)（索引改可走 migration 的待評估）、[security.md](design/security.md)（密碼/RBAC/audit/鎖定改待評估 migration）、[auth-and-admin.md](blueprints/auth-and-admin.md)、[infrastructure.md](design/infrastructure.md)（DB 區塊 + rollback 前提）
- [x] **報名堂號隔離（修正 legacy 連動缺陷）** — Done 2026-06-29
  - 缺陷：legacy 編輯一筆報名改堂號會回寫共用 `Believers.HallName`，連動同信眾全部報名；新版原樣保留。業務定案堂號為**信眾層級** → 採方案 C（零 schema）
  - 後端：`UpdateWithLogAsync` 移除整段 Believer 更新 + 三個 `*ForBeliever` 參數（[ISignupRepository.cs](../backend/src/Ceremony.Application/Signups/ISignupRepository.cs) / [SignupRepository.cs](../backend/src/Ceremony.Infrastructure/Repositories/SignupRepository.cs) / [UpdateSignupHandler.cs](../backend/src/Ceremony.Application/Signups/UpdateSignupHandler.cs)）；堂號仍寫 SignupLog 快照
  - 前端：報名表單堂號改唯讀（比照員工/固定編號），取自 `selectedBeliever()`；移除 `hallName` form control（[signup-edit-form](../frontend/src/app/features/signups/signup-edit-form.component.ts)）
  - 驗證：新增 `UpdateSignupHandlerTests`（6 測試，含 `Edit_never_writes_back_to_Believer` 回歸守門）；後端 291+6 綠、前端 ng build 綠
  - 文件：[signup-hallname-isolation.md](blueprints/signup-hallname-isolation.md)（done）、B13 ✅、glossary / business-rules §3.1 / edit-signup-form 覆蓋表同步
- [x] **報名表單依當月自動帶季別法會** — Done 2026-06-23
  - 需求：新增報名時「法會分類」依當前月份自動帶出季別 root（1-4月春季 / 5-8月中元 / 9-12月秋季），可編輯預設，子法會仍人工挑選
  - 實作：新增 [ceremony-season.ts](../frontend/src/app/shared/util/ceremony-season.ts)（`seasonForMonth`/`currentSeason`/`resolveSeasonRootId`，GUID 優先 title 退場）；[signup-edit-form.component.ts](../frontend/src/app/features/signups/signup-edit-form.component.ts) `loadCategories()` 後呼叫 `applySeasonDefault()`（僅 create 模式 + 欄位未有值才帶入，編輯模式不覆蓋）。`tsc --noEmit` 綠
  - 業務面：定案 pending B3 的「月份範圍」部分，記入 [business-rules-implicit.md](business-rules-implicit.md) §17；尚待每日尖峰筆數
- [x] **開始功能表加「解除安裝」捷徑 + 升級政策定為手動覆蓋安裝** — Done 2026-06-18
  - 升級：手動覆蓋安裝（NSIS 同 appId 認舊版→先靜默移除再裝新版，沿用 `$PROGRAMFILES64\Ceremony`，config 保留）；electron-updater 自動更新未實作（標未來項）。文件改 [infrastructure.md](design/infrastructure.md)
  - 解除安裝：原本只能從控制台移除 → [installer.nsh](../frontend/build/installer.nsh) `customInstall`/`customUnInstall` macro 在開始功能表建/刪 `解除安裝 ${PRODUCT_NAME}.lnk`（指向 electron-builder uninstaller）。已對照 app-builder-lib NSIS 模板確認 hook 與 `UNINSTALL_FILENAME`/`PRODUCT_NAME` 變數；最終 NSIS 編譯待 Windows 打包驗證。見 [electron-packaging.md](blueprints/electron-packaging.md) 第 5 項
- [x] **UI 版號改從 package.json 自動連動** — Done 2026-06-18
  - 症狀：bump `frontend/package.json` 到 2.0.1 後，介面仍顯示 v2.0.0（不連動）
  - 真因：`environment.ts` / `environment.prod.ts` 的 `version` 為寫死字串 `v2.0.0`，發版時只改了 package.json
  - 修：兩個 environment 檔改 `import { version } from '../../package.json'` → `version: \`v${version}\``；`npm run build` 通過，產物含 `2.0.1`。發版規範同步縮為「只 bump package.json + status.md」（見 [conventions.md](conventions.md) / [frontend-design.md](design/frontend-design.md)）
- [x] **修正「DB 連線後直接進首頁」→ 強制登入** — Done 2026-06-18
  - 症狀：DB 連線成功 / App 啟動後 renderer 重載，直接進首頁、跳過登入
  - 真因：`AuthStore` 把 `{user, token}` 寫 `localStorage`，重載時 `loadFromStorage` 還原舊 token → `authGuard.isLoggedIn()` 為 true → 放行（殘留 token 可能來自不同 DB / 已失效）
  - 修：[auth.store.ts](../frontend/src/app/core/auth/auth.store.ts) session 改 **記憶體 only**（移除 load/saveToStorage），重載即清空 → 強制回 `/login`
  - 驗證：`ng build` 0 error；Docs 同步 [security.md](design/security.md) JWT 段 + checklist

- [x] **移除 weypro 後門 → 系統 SuperAdmin `sa@system.local`** — Done 2026-06-18
  - 舊系統硬編後門 `weypro/weypro12ab` 改為系統內建 SuperAdmin `sa@system.local` / `Admin@123`（非 DB，adminId 0）
  - Code：`AuthOptions` `Backdoor*`→`SuperAdmin*`、[LoginHandler.cs](../backend/src/Ceremony.Application/Auth/LoginHandler.cs)、`appsettings.json`；**168 unit 測試綠**、整合測試專案編譯過（憑證字面同步替換）
  - Docs 同步：security/database-design/infrastructure/glossary/auth-and-admin/post-auth-login/user-training/pending-business-input/README（legacy `reference/*` 保留 weypro 為史實）
  - ⚠️ `Admin@123` 在 `appsettings.json`（私有 repo），屬 security.md 已知風險 #2；prod 可由 `Auth:SuperAdminEnabled=false` 關閉（待業務確認）

- [x] **上 GitHub（私有）+ Windows 打包 CI + 歷史 secret 清除** — Done 2026-06-18
  - **打包預設連線最終定案＝gitignored 種子檔（合規）**：曾短暫走「硬編 `DEFAULT_CONFIG`」(規則 11 例外)，後**撤回**改回遠端原本的 `readDefaultConfig` + `build/default-config.json`（gitignore）+ `default-config.example.json` 範本；密碼不入 repo
  - **新增 [.github/workflows/release.yml](../.github/workflows/release.yml)**：`v*` tag / 手動 → `windows-latest` → setup-dotnet(global.json) + Node22 → npm ci → 從 secret `DEFAULT_DB_CONFIG` 寫種子 → publish.ps1 → electron:build → `electron-builder --win` → release.exe 附到 GitHub Release。解決 mac 無法產 NSIS
  - **歷史改寫（git-filter-repo）**：移除 `reference/*.bak`（102MB 超 GitHub 限制、含 PII）+ scrub 兩組 sa 密碼字面（dev/prod）。repo 由公開改私有後 push 到 `github.com/waiting0201/ceremony`
  - **合併遠端 `windows打包`**：吸收 sidecar cwd 修正（備份 500 真因）、無選單列、installer.nsh 英文安裝目錄、backup-page 直寫 D:\Backup
  - 需在 GitHub 設 Actions secret `DEFAULT_DB_CONFIG`（CI 打包用，見 [infrastructure.md](design/infrastructure.md) Release workflow 段）

- [x] **補 2 項便利功能 B1+B2：選信眾自動帶入預繳歷史 + 固定編號顯示（含修信眾編輯洗掉 IsFixedNumber 既有 bug）** — Done 2026-06-02
  - **B1 預繳歷史自動帶入**（對齊舊 `NewSignupForm.BelieverSelected:1102-1115`）：新 endpoint `GET /api/v1/prepay?believerId&year` → 撈該信眾「Year ≤ year 最新一筆報名」的預繳（`ORDER BY Year DESC, CeremonySort DESC`，查 `dbo.SignupView`），`prepayYear` 非 null 才回值；新檔 `GetBelieverLatestPrepayHandler` + `ISignupRepository.GetLatestPrepayByBelieverAsync` + `BelieverLatestPrepayResult` 契約。前端 `signup-edit-form.pickBeliever` 選信眾後呼叫並 patch 預繳年/法會（失敗 try/catch 不阻斷）
  - **B2 固定編號**：`BelieverListItem` 補 `IsFixedNumber`（repo 兩個 SELECT + 映射）；報名表單新增唯讀「固定編號 是/否」顯示（比照員工類型）；**連帶修既有 bug** — `believer-edit-form` 編輯時 `isFixedNumber` 寫死 false → 存檔把信眾 IsFixedNumber 洗成 false，且原本無 UI；改為載入真值 `item.isFixedNumber` + 新增 checkbox
  - **測試**：`GetBelieverLatestPrepayHandlerTests` 3 case；後端 **168 unit + 45 infra + 60 integration = 273 全綠**；前端 tsc 0 / ng build 0 warning
  - **實機驗證（dev real DB，已重啟 API）**：`GET /prepay?believerId=…&year=115` → 200 `{prepayYear:121, …title:"春季"}`；不存在 believerId → 200 三欄 null；believers 清單已帶 `isFixedNumber`
  - **Doc**：新 [get-prepay-believer-latest.md](blueprints/api-endpoints/get-prepay-believer-latest.md) / [new-signup-form.md](blueprints/legacy-coverage/new-signup-form.md) rows 22+34 ✅ / [api-design.md](design/api-design.md) prepay 段 / [api-endpoints/README.md](blueprints/api-endpoints/README.md)

- [x] **進 Electron 前最終新舊交叉比對 + 全 CRUD live 測試 + 修 3 偏差 + coverage 文件刷新到 100%** — Done 2026-06-02
  - **CRUD live 測試（dev real DB，跑完已清乾淨）**：Categories / Admins / Believers / Signups 全 create→read→update→delete round-trip 通過，含業務規則 live 驗證：
    - 法會類型深度限制（孫節點）→ 422 `CATEGORY_DEPTH_LIMIT`；刪有子節點根 → 409；管理者重複 username → 409、username 編輯不可變、軟刪 `IsEnabled=0`；信眾名單必恰 6 元素、刪有報名信眾 → 409 `BELIEVER_HAS_SIGNUPS`「…已有報名資料，不能刪除！」
    - **報名 NumberTitle 自動推導全 5 型**：1→No / 2→寺 / 3→觀 / 4→普 / 5→郵；重號 → 409「{year} {法會} {類型} 編號重複，請重新確認！」；改報名後 SignupLog 快照累加（1→2）
    - 用 year=999 隔離測試資料；附帶清掉 45 筆殘留整合測試垃圾（"another"/"itest_b_…"）
  - **交叉比對（10 Form vs 新實作）**：3 組稽核 agent 逐行對照舊原始碼，結論「無阻擋上線的缺漏功能」；查出並**全部修正** 3 個真實偏差：
    1. **報表編號字串格式**（`GenerateReportHandlers.cs` `SignupReportContext`）：先前一律 `{title}-{號}` 連字號錯誤 → 改 per-type（datacard `title.號` / receipt 只印號 / tablet·text `type==2 只印 title` 否則 `title+號` / worship `title+號`），對齊舊 `SignupForm.btnPrint` 488-637；新增 `ReportNumberFormatTests` 9 case
    2. **SignupLog 排序**（`SignupLogRepository.cs:28`）：`ASC` → `DESC`（最新在前，對齊舊 `OrderByDescending`）
    3. **信眾表單地址 cascade**（`believer-edit-form`）：移植報名表單城市→區域連動下拉 + 同寄件地址，取代「郵遞區號 ID」數字框（契約不變；tsc 0 / ng build 0 warning）
  - **coverage 文件刷新**：10/10 Form 全達 **100% complete**（先前低覆蓋多為「前端 shipped 未打勾」stale）；上線 gate grep（`⏳/🤔`）**0 row-level 命中**；NewSignupForm 剩餘 WinForms 列印內部事件統一 ❌ 故意捨棄（改 server-side PDF）
  - **測試**：後端 156+45+60 → **165 unit + 45 infra + 60 integration = 270 全綠**（+9 編號格式）
  - **⚠️ 仍待後續（不阻 Electron，已記 backlog/pending）**：(a) 列印實機驗收 + Worship variant（P1）；(b) 2 項便利功能 — 選信眾自動帶預繳歷史（需 `GET /prepay?believerId`）、`BelieverListItem` 補 `IsFixedNumber`；(c) **安全簽核**：prod 關後門帳號 `sa@system.local`、密碼明文儲存取捨需 owner（[security.md](design/security.md)）
  - **Doc**：10 份 legacy-coverage + README / [printing-reports.md](blueprints/printing-reports.md) / [get-signup-logs.md](blueprints/api-endpoints/get-signup-logs.md) / [gotchas.md](gotchas.md)（+3 條）/ believer & signup coverage
  - **注意**：dev API（PID 在跑）仍是舊 DLL；report/log 修正需重啟 `dotnet run` 才會 live 生效（測試已覆蓋）

- [x] **薦牌直書修正開頭全形空格「蓋到下一格」(GroupFontPt 列數 Trim→Length，對齊 Stack)** — Done 2026-06-02
  - **真實案例驗證**：signup `543EA33D-3DFB-472B-8DCF-C8663792F12D` 的 `DeadNameTwo="　蔡炎城"`（開頭 U+3000，用來把名字往下推排版）render 後**蓋到下方「蔡貴仁」**（pdftoppm 轉圖目視確認）
  - **根因**：`GroupFontPt` 用 `Trim().Length` 算列數（去掉開頭空格→3 列），但 `Stack` 渲染不 trim（4 列）→ 字級沒縮 → 溢出列距蓋下一格
  - **修正**：`VerticalText.GroupFontPt` 改 `name.Length`（與 Stack 一致）→ 縮到 0.466cm 剛好塞進列距、不蓋。回歸測試 `GroupFontPt_counts_leading_fullwidth_space_as_a_row`；Application 155 + Infrastructure 45 全綠
  - **已決策（使用者）**：寫入端**完全不 trim 姓名開頭/結尾**（保留半/全形開頭結尾空格作排版，刻意偏離 legacy 的 save-time `.Trim()`）。**前後端共 5 處**改為僅純空白 → null：後端 `CreateSignupHandler`/`UpdateSignupHandler`/`BelieverWriteValidator` 的 `NormalizeNames`、前端 `signup-edit-form`/`believer-edit-form` 的 names `.map`。新增 `Names_preserve_leading_fullwidth_space_for_layout` 測試；Application 156 全綠
- [x] **薦牌字級門檻改用「真實字數」(排除半/全形空格)，姓名中間刻意空格不再誤縮字級** — Done 2026-06-02
  - **問題**：使用者在姓名中間故意輸入空格作排版間隙；`PrintTemplateSelector` 字級門檻用原始 `.Length > 7`，空格被當一字 → 7 真字+1 空格=8 → 誤把 0.8cm 縮成 0.6cm
  - **釐清（AskUserQuestion）**：空格是**刻意排版用、要保留** → 渲染（`VerticalText.Stack`/`GroupFontPt`）不動，只修字級門檻
  - **修正**：`PrintTemplateSelector.RealCharCount`（`char.IsWhiteSpace`）套到 `dead1Long`/`dead2Long`；**刻意偏離 legacy**（舊 `Trim().Length` 計入中間空格）。新增門檻 + 渲染回歸測試（鎖住兩 helper 語意相反），Application 155 + Infrastructure 44 全綠
  - **Doc**：[business-rules-implicit.md](business-rules-implicit.md) 字級段 / [gotchas.md](gotchas.md)「姓名中間空格」/ [legacy-coverage/signup-form.md](blueprints/legacy-coverage/signup-form.md) / [printing-reports.md](blueprints/printing-reports.md)
- [x] **列印直書姓名改「整組統一字級」（修正字有大有小，對齊舊系統固定字級）** — Done 2026-05-29
  - **回報**：文字變有大有小，還是參考舊程式碼的邏輯（舊系統同類名字**同大小**）
  - **釐清（AskUserQuestion）**：舊邏輯是固定字級（薦牌 ParaFontSize、文牒固定 0.8）→ 同大小但長名字會疊；逐格縮 → 不疊但大小不一。使用者選 **「一致字級 + 不疊（整體縮）」**
  - **修正**：`VerticalText` 把逐格 `FitFontPt` 改成 **`GroupFontPt`**：以舊字級為起點，取整組（所有往生一組、所有陽上一組）每格「可用高/字數」的最小值當**統一字級**套到全組 → 全組同大小、最擠的也塞得下、不重疊；不需要時 = 舊字級。往生/陽上各自一組（對齊舊系統 ParaFontSize 只管往生）。薦牌 + 文牒同步
  - **驗證（影像）**：文牒 5 亡 → 5 個往生**同大小**不疊（之前主欄較大）；文牒/薦牌 3 亡 3 陽 → 全維持 0.8/0.6 不縮；薦牌 4 亡 4 陽 → 往生同大小、陽上整組同縮不疊。Infrastructure **42 綠**（`GroupFontPt` 4 case）。未開 API
  - **Doc**：[printing-reports.md](blueprints/printing-reports.md)（第四輪字級策略）/ [gotchas.md](gotchas.md)（直書改「整組統一字級」不逐格縮）

- [x] **文牒往生/陽上姓名重疊修正（檢查數量+字級，對照舊 code）+ 抽共用 VerticalText** — Done 2026-05-29
  - **回報**：文牒往生者文字會重疊，檢查數量跟文字大小
  - **檢查（對照舊 [SignupForm.cs PrintText:1335](../reference/old/Ceremony/SignupForm.cs#L1335)）**：數量判斷（恰 2 亡→tmpTextTwo 否則 tmpText）與 `ChooseText` 一致 ✅；舊 PrintText **無 ParaFontSize**，亡/陽固定 0.8cm，與新版一致 ✅
  - **真因**：TextRenderer 先前**沒**套用薦牌那輪的直書/縮字修正（仍 width-wrap）→ 次要格 3 字名溢出到下一格重疊
  - **修正**：抽共用 [VerticalText](../backend/src/Ceremony.Infrastructure/Reporting/VerticalText.cs)（`Stack` 直書 + `FitFontPt` 縮字 + `Avail` 列距），薦牌/文牒共用；TextRenderer 亡者矩陣列距 2.06375cm、陽上 1.98436cm；薦牌也同步改用共用工具
  - **新增 `Avail`（faithful 文字大小）**：次要格**只有「正下方有名字」才用列距縮字**，否則維持原字級 → 3 亡 3 陽全部 0.8cm 不縮（無第 4/5 位）；5 亡才縮上排。薦牌同步套用（3 亡全 0.6 不縮）
  - **驗證**：影像（文牒 3d3l/5d2l/2d2l + 薦牌 3d3l/4d4l 回歸）皆不重疊、僅必要時縮字；Infrastructure **41 綠**（測試引用改 `VerticalText.*`）。未開 API
  - **Doc**：[printing-reports.md](blueprints/printing-reports.md)（第三輪驗證）/ [gotchas.md](gotchas.md)（直書五要點 + `Avail`「下一格空則不縮」）

- [x] **文牒地址字黏一起 + 薦牌往生/陽上字級過縮重疊（皆對照舊 code 修正）** — Done 2026-05-29
  - **(1) 文牒地址黏在一起**：[SkiaImageHelpers.VerticalAddress](../backend/src/Ceremony.Infrastructure/Reporting/SkiaImageHelpers.cs) 逐字步進原照搬舊 [Library.cs:34-124](../reference/old/Ceremony/Commons/Library.cs#L34-L124) 的 `MeasureString.Height − 9`，但 GDI+ MeasureString.Height 膨脹（≈1.4–1.5×字級），SkiaSharp 的 `Descent−Ascent` 已是緊湊行高（25px字→25.6px），再 −9 變 16.6px < 字面 23px → 重疊。**修正**：步進改用字型行高（不再 −9/−10）。影像驗證字距正常
  - **(2) 薦牌往生/陽上字級+重疊**：對照舊 [SignupForm.cs PrintTablet:1148](../reference/old/Ceremony/SignupForm.cs#L1148)（字數判斷：ParaFontSize 亡>7字→0.6否則0.8，與新版一致）+ RDLC 格屬性（次要格 `CanGrow=true`）。**真因**：上一輪縮字用 RDLC **名目格高**（往生1.5875）過度縮小 3 字往生名。**修正**：縮字可用高改取**到下一格的列距**（往生 1.8639、陽上 1.43785cm）→ 3 字往生名（1.8<1.8639）**維持舊系統 0.6cm 不縮且不重疊**；陽上列距較窄仍略縮但分開。影像驗證 2d4l/4d4l/3d3l 往生回原字級不疊、陽上不疊
  - **驗證**：Infrastructure 測試 **41 綠**（新增列距案例守門）；用測試 harness 直接算圖目視，未開 API
  - **Doc**：[printing-reports.md](blueprints/printing-reports.md)（薦牌驗證段 + 地址段）/ [gotchas.md](gotchas.md)（直書四要點：不約束寬度+顯式換行+依**列距**縮字+主欄不縮；GDI+→Skia magic number 不可照搬）

- [x] **文牒地址字體修正：SkiaSharp 垂直地址 PNG 渲染成 tofu 方框** — Done 2026-05-29
  - **回報**：文牒地址字體有問題
  - **真因**：文牒地址是 [SkiaImageHelpers.VerticalAddress](../backend/src/Ceremony.Infrastructure/Reporting/SkiaImageHelpers.cs) 畫的垂直 PNG，用 `SKTypeface.FromFamilyName("BiauKai")` → OS 找不到家族名（macOS 叫「標楷體-繁」）→ `SKTypeface.Default`（無中文字符）→ 地址整排 **tofu 方框**。**關鍵**：Skia 的 FromFamilyName 與 QuestPDF 的 FontManager 是**兩條獨立**字型路徑，前一輪的 `ReportFonts` 註冊只救到 QuestPDF，救不到 Skia（故薦牌好了、文牒地址仍壞）。**此 bug 在 production 也會發生**（非僅 dev）
  - **修正**：SkiaImageHelpers 改用 `SKTypeface.FromFile(ReportFonts.ResolvedPath)` 載入與 QuestPDF 同一個字型檔（快取一次），找不到才退 FromFamilyName/Default
  - **驗證**：dump 垂直地址 PNG 影像 → 修正前整排 tofu 方框；修正後「台北市中正區忠孝東路一段2號」正確直排標楷體、數字 2 旋轉 90°（對齊舊 [Library.cs](../reference/old/Ceremony/Commons/Library.cs#L34-L124)）。Infrastructure 測試 **40 綠**
  - **Doc**：[printing-reports.md](blueprints/printing-reports.md)（remaining 該項標 ✅）/ [gotchas.md](gotchas.md)（QuestPDF 與 SkiaSharp 兩條獨立字型路徑、各自用檔案路徑載入）

- [x] **薦牌往生/陽上姓名重疊修正 + 確認數量判斷/字級/位置** — Done 2026-05-29
  - **回報**：薦牌往生跟陽上名字會重疊，請檢查判斷名字數量 / 文字大小 / 文字位置
  - **逐項對舊系統核對**（用測試 harness 渲染 9 變體×3字姓名目視 + 對 [SignupForm.cs PrintTablet:1148](../reference/old/Ceremony/SignupForm.cs#L1148)、9 個 RDLC）：
    - **判斷名字數量** ✅：`ChooseTablet` 與舊連續填值情境一致；**Base 版面只有 5 亡+5 陽格 → 第 6 名不印，與舊系統一致**（RDLC 無第 6 格，非 bug）
    - **文字大小** ✅：ParaFontSize（亡 >7字→0.6cm 否則 0.8cm）與舊一致；陽上字級依變體 0.6/0.8cm 對齊 RDLC
    - **文字位置** ✅：對齊 RDLC 絕對座標
  - **重疊真因**：RDLC 次要姓名格很矮（往生2/3 高 1.5875cm≈2字、陽上次欄 1.26cm≈2字）且 `CanGrow=true`；前一輪修字型丟字時**完全移除了高度約束** → 3 字姓名直書溢出到下一格 → 重疊
  - **修正**：[TabletRenderer](../backend/src/Ceremony.Infrastructure/Reporting/TabletRenderer.cs) 直書改 **縮字塞欄高**（新 `FitVerticalFontPt`：n字×字級 ≤ 欄高才不溢出；主欄夠高不縮、只壓矮的次要格；**縮字級不壓行高** → 字與字不互疊）。影像確認 4d4l/2d4l 次要姓名縮小但**不再重疊**、主姓名維持原字級
  - **驗證**：Infrastructure 測試 **40 綠**（新增 `FitVerticalFontPt` 2 case）；9 變體影像目視
  - **Doc**：[printing-reports.md 薦牌驗證段](blueprints/printing-reports.md) / [gotchas.md](gotchas.md)（直書三要點：不約束寬度+顯式換行+依欄高縮字）

- [x] **薦牌（Tablet）尺寸/字體修正：字型未真正註冊 + 直書姓名被靜默丟字** — Done 2026-05-29
  - **回報**：薦牌尺寸/文字尺寸/位置/字體有問題
  - **排查（RDLC ground truth）**：把 9 個 `tmpTablet*.rdlc` 的頁面/欄位座標（Top/Left/W/H/FontSize/Family）逐一抽出比對 [TabletRenderer](../backend/src/Ceremony.Infrastructure/Reporting/TabletRenderer.cs) → **座標全部正確**。真因有二：
    1. **字型沒真正載到**：renderer 用 `FontFamily("BiauKai")`，但 macOS BiauKai.ttc 家族名其實是「標楷體-繁/BiauKaiTC」→ SkiaSharp 找不到 → silently fallback **PingFang TC**（`pdffonts` 實測）。字寬不同 → 尺寸/位置/字體全錯
    2. **直書姓名整欄消失**：窄欄(≈字級)+自動換行的直書，換成真標楷體（全形字寬≈欄寬）後 QuestPDF 因單字放不下而**靜默丟字**（fallback PingFang 較窄時剛好沒事，故被字型 bug 掩蓋）
  - **修正**：
    - 新增 [ReportFonts.cs](../backend/src/Ceremony.Infrastructure/Reporting/ReportFonts.cs)：啟動時 `FontManager.RegisterFontWithCustomName("BiauKai", stream)`，字型來源 `CEREMONY_KAI_FONT` → Windows kaiu.ttf → macOS BiauKai.ttc(glob) → Linux TW-Kai；找不到印警告不 fallback。在 `AddCeremonyInfrastructure` 呼叫
    - TabletRenderer 直書姓名改 `StackVertical`（每字一行 `\n`、不約束寬度）；新增 helper 單元測試
  - **驗證（dev real DB，1亡1陽 OneOne）**：`pdffonts` PingFangTC → **BiauKaiHK-Regular(標楷體)**；`pdftotext`+`pdftoppm` 影像：姓名整欄消失 → 「往/生/甲」「陽/上/一」正確直書定位。旁證 datacard/receipt/text 姓名正常。Infrastructure 測試 **35 綠**
  - **Doc**：[printing-reports.md 字型註冊段](blueprints/printing-reports.md)（重寫）/ [gotchas.md](gotchas.md)（+2 條：家族名 fallback、全形 CJK 窄欄丟字）
  - **⚠️ 連帶發現（已記 backlog P1）**：**普桌（Worship）陽上姓名同類丟字、目前不顯示**——字型修正是共用的、讓普桌字型也變正確，但普桌窄欄丟字是既有問題（3cm 字 > 2.2cm 欄寬，與字型無關），需另做

- [x] **列印預覽：預覽區填滿視窗、距底 12px** — Done 2026-05-29
  - [reports-preview-page.scss](../frontend/src/app/features/reports/reports-preview-page.scss) 改 flex chain（`:host height:100%` → `.page` flex column → `.page-header`/`.form-bar` flex:0 → `.preview` `flex:1; min-height:0` → `.pdf-frame` `height:100%`），取代原 iframe 固定 720px / `.preview min-height:600px`；對齊報名維護/信眾維護填滿模式（shell `.content` 已 12px）。`ng build` 0 warning。Doc：[visual-design 列印預覽段](design/visual-design.md)

- [x] **法會類型「看不到子項」排查 → 非 bug（dev DB 無子分類），加 dev seed** — Done 2026-05-29
  - **症狀**：使用者回報法會類型維護頁看不到子項
  - **排查**：直查 dev DB `dbo.CeremonyCategorys` → 僅 **3 筆根分類（春季/中元/秋季）、0 筆子項**（ParentID 全 NULL）；live API 跑「建立子項」round-trip（POST parentId=春季 → GET 回 `春季 children=[__diag_child__]` 正確巢狀 → DELETE 204 清掉）證實**前後端皆正常**（`ListCategoriesHandler` 樹建構 + `categories-page` 渲染 `root.children`）。結論：純資料缺，非程式 bug
  - **處理（依使用者選擇）**：新增 idempotent dev seed [backend/db/seed/dev-seed-categories.sql](../backend/db/seed/dev-seed-categories.sql)，以**根分類名稱**對應 ParentID（不寫死 GUID）、`WHERE NOT EXISTS (Title, ParentID)` 防重覆；春季→梁皇寶懺/藥師法會/大悲懺、中元→盂蘭盆/三時繫念/瑜伽焰口、秋季→地藏法會/水陸法會（共 8 筆）
  - **已套用 dev DB 並驗證**：執行後 total=11/children=8；**再跑一次仍 11（idempotent）**；API `/categories` 回完整兩層樹。**僅 dev**；正式 DB 凍結
  - **Doc 同步**：[database-design.md §2 CeremonyCategorys](design/database-design.md) 加「本機 dev 資料現況」註

- [x] **新增報名表單編排對齊舊 NewSignupForm + 地址城市/區域連動下拉（含新後端 Zipcodes API）** — Done 2026-05-29
  - **需求 + 決策**：使用者要「新增報名表單編排參照舊原始碼」。釐清後（AskUserQuestion）定案：**保留單頁**（不重做舊兩步驟，沿用 mockup v4）+ **欄位編排對齊舊版** + **地址做城市→區域連動下拉**（需新後端 Zipcodes API）
  - **後端（全新 Zipcodes 唯讀 API）**：
    - `GET /api/v1/zipcodes/cities`（distinct City，`GROUP BY City ORDER BY City`）+ `GET /api/v1/zipcodes?city=`（該城市區域，item `{zipcodeId,city,area,zipcode}`，`ORDER BY Zipcode`；city 空回空陣列）；對齊舊 [NewSignupForm.cs:662-677/406-460](../reference/old/Ceremony/NewSignupForm.cs#L662)，**未過濾 IsDisplay**（同舊）
    - 新檔：`Application/Zipcodes/`（ZipcodeContracts / IZipcodeRepository / ListZipcodeCitiesHandler / ListZipcodeAreasHandler）、`Infrastructure/Repositories/ZipcodeRepository.cs`（Dapper）、`Api/Controllers/ZipcodesController.cs`（`[Authorize]`）；DI 兩處註冊
    - **實機 dev DB smoke**：login(sa@system.local) → cities **22** 筆、`台北市` areas **12** 筆（中正區/100、大同區/103、中山區/104… 依 Zipcode 排序）皆 HTTP 200
    - Tests：`Ceremony.Application.Tests/Zipcodes/ZipcodeHandlersTests` 5 case；Application 單元測試 **151 綠**；`dotnet build` 0 warning
  - **前端**：
    - 新增 `core/api/zipcodes/`（ZipcodeApi.cities/areas + models）
    - 重寫 [signup-edit-form.component](../frontend/src/app/features/signups/signup-edit-form.component.ts)（create/edit 共用）單頁編排對齊舊版：**法會資料→信眾→基本資料→地址→陽上/往生名單→編號/費用→備註/預繳**
    - 地址改 **城市→區域連動下拉**（區域 option value=ZipcodeID）+ 唯讀郵遞區號顯示 + **同寄件地址 checkbox**（複製 mail→text；mail 空 → verbatim「請先輸入寄件地址」）；取代原本手填「郵遞區號 ID」數字
    - 員工類型**唯讀顯示**（信眾屬性；inline 編輯信眾故意捨棄）；**固定編號待補**（read DTO `BelieverListItem` 無 `IsFixedNumber`）
    - 既有報名（編輯/代入）只有 city/area 字串無 zipcodeId → `applyAddress` 以區域名稱比對回填 zipcodeId
    - 契約不變（仍送 `CreateSignupRequest` 的 `mailZipcodeId`/`textZipcodeId`）；`tsc` 0、`ng build` 0 warning
  - **未做（刻意）**：兩步驟流程（單頁 mockup v4）/ inline 新建信眾 / `GET /zipcodes/lookup` 反查（不需要）/ 固定編號（待 read DTO 補欄）
  - **Doc 同步**：新 [api-endpoints/get-zipcodes.md](blueprints/api-endpoints/get-zipcodes.md) / [api-design.md Zipcodes 段](design/api-design.md)（標 ✅）/ [legacy-coverage/new-signup-form.md](blueprints/legacy-coverage/new-signup-form.md)（rows 3/4/5/9-13/19-24 ✅、覆蓋率 21→59%）/ [signup-management.md 新增段](blueprints/signup-management.md)（重寫）/ [frontend-design.md](design/frontend-design.md)

- [x] **信眾維護清單三項微調：距底 12px、欄位對齊舊原始碼、改右鍵選單** — Done 2026-05-29
  - **動機**：使用者要求信眾維護清單比照報名維護處理 — (1) 表格距 bottom 12px (2) 欄位參照舊原始碼 (3) 改右鍵選單的方式
  - **距底 12px**：[believers-page.scss](../frontend/src/app/features/believers/believers-page.scss) 改 flex chain（`:host height:100%` → `.page` flex column → `.results-card` flex:1 → `.vgrid-zone`/viewport flex:1）；shell `.content` padding-bottom 12px 已是全域（沿用報名維護那次）
  - **欄位對齊舊原始碼**：新增 [believer-columns.ts](../frontend/src/app/features/believers/believer-columns.ts)，22 個可見欄 1:1 抽自 [BelieverForm.Designer.cs](../reference/old/Ceremony/BelieverForm.Designer.cs) dgvBelievers（header/width/順序）：員工/堂號/姓名/聯絡電話/寄件城市·區域·地址/文牒城市·區域·地址/往生1·2·3·3-1·5·6/陽上1·2·3·3-1·5·6 + 列尾 ⋮ 操作欄；取代原本 6 欄 RWD 簡表（hide-sm/md/lg）。清單由 plain `<table>` 改 CDK 虛擬捲動 div-grid（沿用全域 `.vgrid-*`；believer 搜尋無 TOP cap，名字如「陳」可能上千筆，虛擬捲動護 DOM）；往生欄底色沿用 `.vgrid-td.dead`
  - **改右鍵選單**：移除每列 inline「編輯/刪除」按鈕，改共用 [ContextMenuService](../frontend/src/app/shared/context-menu/context-menu.service.ts)（右鍵 contextmenu 事件 + 列尾 ⋮ kebab 兩入口），選單「編輯 / 刪除（danger）」；對齊 admins/signups pattern（舊 cmsBelievers 僅「刪除」，編輯走 cell-click，新版合併）。刪除沿用 `ConfirmDialogService`；有報名衝突由 backend `DeleteBelieverHandler` 回 409 verbatim「{name} 已有報名資料，不能刪除！」前端顯示
  - **未做（刻意）**：欄寬持久化 / 多選批次（信眾維護無此需求；完整版見報名維護）
  - **驗證**：`tsc --noEmit` ✅ exit 0；`ng build` ✅ 0 warnings；believers-page chunk ~8 → 20.17 kB（虛擬捲動 + 欄定義 + context menu）；後端未動
  - **Doc 同步**：[visual-design.md 信眾維護頁面](design/visual-design.md)（全段重寫）/ [frontend-design.md](design/frontend-design.md) 實作參考 / [legacy-coverage/believer-form.md](blueprints/legacy-coverage/believer-form.md)（row 7 右鍵 ✅、覆蓋率 47→53%）/ [believer-management.md](blueprints/believer-management.md) 驗收標準（修正 26 欄筆誤 + 勾選）

- [x] **資料備份加「清交易紀錄檔」checkbox（儲存位置延後 Electron）** — Done 2026-05-29
  - **需求**：使用者要「立即備份先跳選儲存位置 + 一個清 log checkbox」。釐清後：選儲存位置**延後 Electron**（browser 無法選伺服器路徑，已記 P3 backlog）；清 log = **SQL Server 交易紀錄檔**，本次實作
  - **後端**（[SqlBackupService.cs](../backend/src/Ceremony.Infrastructure/Backup/SqlBackupService.cs)）：`BackupRequest` 加 `ClearLog`；`BackupResponse` 加 `LogCleared`/`LogBackupFileName`/`LogClearError`。清 log 依 recovery model 安全處理（不破壞還原鏈）：
    - **FULL/BULK_LOGGED** → `BACKUP LOG [db] TO DISK=...{ts}.trn`（正確截斷、保留 .trn）+ `DBCC SHRINKFILE(log,1)`
    - **SIMPLE** → `CHECKPOINT` + `DBCC SHRINKFILE(log,1)`
    - 純函式 `BuildClearLog()` 供單測；清 log 失敗 try/catch 不讓備份 API 失敗（回 `logCleared=false`+原因）
  - **前端**（[backup-page.ts](../frontend/src/app/features/backup/backup-page.ts)）：加 checkbox「備份後清除交易紀錄檔」；勾選時 confirm dialog 加警語（danger）；成功 dialog 顯示清 log 結果（FULL 附 .trn 檔名 / 失敗顯示原因）
  - **Tests**：`BackupSqlTests` +6（SIMPLE/FULL/BULK_LOGGED/Windows 分隔/identifier+log 名跳脫）→ 14；全後端 **243 綠**（148+35+60）
  - **實機驗證（dev real DB）**：dev Ceremony 為 **FULL** recovery → `POST /api/v1/backup {"clearLog":true}` 回 200、`logCleared=true`、產 `*.trn`；`{}` → `logCleared=false`

- [x] **列印資料套入 — 4 項版面精修 + QuestPDF 裁字 bug 修正（這才是「列印資料還沒套入」真因）** — Done 2026-05-29
  - **🐛 關鍵 bug（真因）**：5 個 renderer 用 QuestPDF `.Height()` 把 text box 夾成 RDLC 精確高度，但 QuestPDF 預設 line-height（~1.2–1.5× 字級）**超過**該高度 → QuestPDF **靜默裁切 / 丟棄**文字（不報錯）。結果 Number、所有 label（亡者:/陽上:/地址:/電話:）、phone、prepay、簽名 label 都沒印出（pdftotext 驗證修正前 ~17 欄位只有 4 個渲染）
    - **修正（5 renderer 全改）**：停用 `.Height()` 夾字；VerticalAlign Middle/Bottom 改 translate Y offset 模擬；每個 text span 設 `.LineHeight(1f)`。修正後全欄位正確（pdftotext + pdftoppm 影像雙驗）
  - **完成 4 項**：
    1. **worship2.png 背景嵌入**（WorshipRenderer）：copy 成 `Reporting/Assets/worship2.png` EmbeddedResource，載一次當底層（Top 0.26141 Left 0.42 W 20.04729 H 28.88438 cm FitProportional）；Number 改置中且不再被裁
    2. **Tablet 9 變體座標**（TabletRenderer）：依 `TabletTemplate` 切換；座標權威抽自 9 個 `tmpTablet*.rdlc` XML（含 Tablix→cell→Rectangle 巢狀絕對座標）；`tmpTabletOneOne` Page margin 2cm；DeadName 字級來自 ParaFontSize
    3. **Text 2 變體 + PhotoAddress 垂直地址**（TextRenderer）：依 `TextTemplate` 切換 + DeadName 座標取自 tmpText/tmpTextTwo.rdlc Rectangle2；PhotoAddress 改真 25×605px 垂直地址 PNG（新 `SkiaImageHelpers.VerticalAddress`，1:1 移植 [Library.cs:34-124](../reference/old/Ceremony/Commons/Library.cs#L34-L124)：中文直排、`[a-zA-Z0-9\-\(\)]` 旋轉 90°）；嵌 Top 4.1 Left 25.4 W 0.66 H 16.8 FitProportional
    4. **DataCard 虛線**（DataCardRenderer）：Line2 改真虛線 PNG（新 `SkiaImageHelpers.DashedLine`，先前因 QuestPDF 2026 收回 SkiaSharp Canvas 而用 solid）；實線簽名線 Line1 不變
  - **新 helper**：`Ceremony.Infrastructure/Reporting/SkiaImageHelpers.cs`（VerticalAddress + DashedLine；SkiaSharp 3.119.4 已引用）
  - **字型 bundling 風險**：QuestPDF 與 SkiaSharp 都需標楷體；部署機（Windows / Electron sidecar）必須 bundle TW-Kai / DFKai-SB
  - **Tests**：`RendererSmokeTests.cs` 21 case（DataCard / Receipt / 9 Tablet / 2 Text / 6 Worship / SkiaImageHelpers PNG 全綠）；後端全套 148 unit + 25 infrastructure + 60 integration = **233** 綠
  - **仍 pending**：實機列印驗收（需印表機）/ Worship 6 variant 各自座標 / ±0.05cm CI 量測
  - **Doc 同步**：[printing-reports.md](blueprints/printing-reports.md)（列印資料套入段 + 技術選型表）/ [gotchas.md](gotchas.md)（4 條新陷阱）

- [x] **資料備份 end-to-end — 前端串接 + 後端對齊舊 code** — Done 2026-05-29
  - **後端對齊舊 [MainForm.cs:95-113](../reference/old/Ceremony/MainForm.cs#L95-L113)**（[SqlBackupService.cs](../backend/src/Ceremony.Infrastructure/Backup/SqlBackupService.cs)）：
    - 檔名 `Ceremony-{yyyyMMddHHmmss}.bak` → `{yyyyMMddHHmmssffffff}.bak`（6 位微秒、無前綴）
    - SQL flags `WITH FORMAT, INIT, COMPRESSION` → `WITH NOFORMAT, NOINIT, NAME = N'Ceremony-Full Database Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10`
    - DB 名稱由開啟連線動態取得（`conn.Database`，非 hardcode `[Ceremony]`）
    - 備份目錄建立改 **best-effort**（try/catch）；路徑用 `JoinForSqlServer()` 依 DB 主機分隔符組（不用 `Path.Combine`，避免 API 在 macOS/Linux 跑時組出 `D:\Backup\/file` 混用分隔符）；仍 config-driven（`Backup:Directory`）
    - `sizeBytes`：API 看得到檔用實檔大小，否則 fallback 查 `msdb.dbo.backupset`（API 與 DB 不同機時 .bak 在 DB 主機）
    - 抽 `internal static BuildBackup()` / `JoinForSqlServer()` pure helper 供單測
  - **Config**：appsettings.json 加 `"Backup": { "Directory": "D:\\Backup\\", "RetentionDays": 30 }`（prod Windows 預設，非 secret）；**dev override**：`appsettings.Development.json` 設 `/var/opt/mssql/data/`（`(local)` Docker Linux MSSQL 可寫，無 `D:\`）；目錄須 SQL Server 服務帳號可寫；**RetentionDays 尚未實作清理服務**（仍靠外部 SQL Agent）
  - **實機驗證（dev real DB）**：login(sa@system.local) → `POST /api/v1/backup` 預設名 + 自訂名皆 **HTTP 200**，回正確路徑 `/var/opt/mssql/data/{yyyyMMddHHmmssffffff}.bak` + sizeBytes≈108MB（msdb backupset）
  - **踩雷**：舊版 `Directory.CreateDirectory("D:\\Backup\\")` 曾在 macOS 建出垃圾資料夾 `src/Ceremony.Api/D:\Backup\`，其反斜線名稱使 MSBuild `**/*.resx` glob 列舉失敗（`MSB3552`）→ build 壞；已刪並改 best-effort（見 [gotchas.md](gotchas.md)）
  - **Tests**：新 test project `Ceremony.Infrastructure.Tests` 的 `BackupSqlTests` 8 case（檔名格式 / 自訂名 / SQL flags verbatim / 識別子與路徑跳脫 / `JoinForSqlServer` Windows+Unix 分隔符無 `\/`）；加入 Ceremony.slnx
  - **Frontend（全新）**：icon `database`、`core/api/backup/`（BackupApi.run + models）、nav item `{ /backup, 資料備份, database }`（位於載入預繳與法會類型之間）、route `/backup`、`features/backup/backup-page.ts`（ConfirmDialog 確認 → run → 成功 dialog 顯示 fileName/fullPath/size，錯誤顯示後端 verbatim 中文）；`ConfirmDialogConfig` 加 `hideCancel?` 單 OK 結果 dialog
  - **Doc 同步**：[legacy-coverage/main-form.md](blueprints/legacy-coverage/main-form.md)（backup row end-to-end ✅）/ [auth-and-admin.md](blueprints/auth-and-admin.md)（驗收標準勾選）/ [api-design.md](design/api-design.md)（路徑改 `/backup` + 檔名/flags/動態 DB 名）/ [infrastructure.md](design/infrastructure.md)（Backup config）/ [frontend-design.md](design/frontend-design.md) + [visual-design.md](design/visual-design.md)（nav + 結果 dialog）

- [x] **管理者維護加 row 右鍵 + 編輯 / 刪除 + 對應 backend endpoints** — Done 2026-05-28
  - **動機**：用戶要求清單按右鍵要有編輯跟刪除（同 signups 互動 pattern）
  - **舊系統行為**（[AdminsForm.cs:124-158](reference/old/Ceremony/AdminsForm.cs#L124-L158)）：右鍵只有「刪除」；編輯是 cell click 載入 form。username 編輯時 disabled、軟刪除（IsEnabled=0）
  - **新版改善**：右鍵 menu 兼具「編輯」+「刪除」；編輯沿用 overlay shell；密碼編輯時選填（留空=不變更）
  - **Backend 新增 2 endpoint**：
    - `PUT /api/v1/admins/{id}` — 更新 name + 選擇性 password；username immutable（對齊舊 [`txtUsername.Enabled = false`](reference/old/Ceremony/AdminsForm.cs#L84)）
    - `DELETE /api/v1/admins/{id}` — 軟刪除（`IsEnabled = 0`）對齊舊行為
    - 新檔：`UpdateAdminHandler` / `DeleteAdminHandler` + `UpdateAdminRequest` contract
    - 擴充 `IAdminRepository`：`UpdateAsync(id, password?, name)` / `SoftDeleteAsync(id)` / `GetByIdAsync(id)`
    - 註冊 DI + 沿用 `ExceptionMiddleware` `_NOT_FOUND` 後綴 → 404
    - 驗證：`dotnet build` ✅ 0 warnings；148 unit + 60 integration 全綠
  - **Frontend**：
    - `AdminApi.update(id, body)` / `remove(id)` + `UpdateAdminRequest` model
    - [admin-edit-form](../frontend/src/app/features/admins/admin-edit-form.component.ts) 支援 edit mode：
      - `admin: AdminListItem | null` input；signal effect 切 mode
      - 編輯模式 → username `disable()`、password validators 改 optional（minLength 4 仍 enforced 但 required 拔掉）
      - placeholder「留空表示不變更」提示
    - [admins-page](../frontend/src/app/features/admins/admins-page.ts) 加 row 右鍵 + kebab button：
      - 沿用 [ContextMenuService](../frontend/src/app/shared/context-menu/context-menu.service.ts)（reuse signups pattern）
      - 2 menu items：編輯 / 刪除（danger）
      - 刪除走 [ConfirmDialogService](../frontend/src/app/shared/confirm-dialog/confirm-dialog.service.ts) 取代 native confirm
    - 驗證：`tsc --noEmit` ✅；`ng build` ✅ 0 warnings；admins-page chunk 7.9 → 11.1 kB
  - **未測**：backend dev server 仍跑舊版 DLL（dotnet run 不 auto-reload），需重啟後 PUT / DELETE 才會生效；前端 HMR 已抓到新 code，重整瀏覽器即可

- [x] **全系統 create/edit form 統一改 Full-Screen Overlay** — Done 2026-05-28
  - **動機**：4 個 CRUD feature 原本 4 種不同 pattern（signups route / believers side sheet / categories split panel / admins inline card），不一致；報名 form 25 欄 + picker 撞 sheet 560px；route 切換造成列表 destroy + 重打 API
  - **共用元件**：
    - 全域 `.overlay-*` SCSS primitive（[styles.scss](../frontend/src/styles.scss)）— backdrop / panel / header / body / actions / close-btn；panel **content-adaptive**：`min-width: min(420px, 92vw); max-width: 92vw; max-height: 92vh`，小 form 縮成小窗、大 form 自動撐大
    - `<app-form-overlay>` shared component（[shared/form-overlay/form-overlay.component.ts](../frontend/src/app/shared/form-overlay/form-overlay.component.ts)） — inputs `title` / `dirty`，output `close`；ESC + backdrop + × button 三路皆觸發；dirty 時走 `ConfirmDialogService` 確認再關
    - 全域 `@keyframes fadeIn` / `pop` 動畫 keyframes 抽到 styles.scss（原本只在 confirm-dialog 內部）
  - **4 feature 全部遷移**：每個 feature 抽出 `*-edit-form` standalone component（含 form / API call / dirtyChange emit / submit() 公開方法），list page 用 `<app-form-overlay>` 包：
    - **Signups**：抽 [signup-edit-form.component](../frontend/src/app/features/signups/signup-edit-form.component.ts)；list page 加 `editOverlay` signal + 3 entry points（新增報名 / 修改報名 / 右鍵代入新增 + 修改資料）；signup-edit-page route 改 thin wrapper 保留 deep link
    - **Believers**：抽 [believer-edit-form.component](../frontend/src/app/features/believers/believer-edit-form.component.ts)；移除 `.sheet-*` 用法；刪除走 `ConfirmDialogService` 取代 native `confirm()`
    - **Categories**：抽 [category-edit-form.component](../frontend/src/app/features/categories/category-edit-form.component.ts)；移除右側 panel split layout；3 個 entry（新增根 / 新增子 / 編輯）+ 刪除確認
    - **Admins**：抽 [admin-edit-form.component](../frontend/src/app/features/admins/admin-edit-form.component.ts)；移除 inline expand 卡片；密碼 mismatch 驗證保留
  - **文件規範**：
    - [frontend-design.md「CRUD 頁面排版模式」段](design/frontend-design.md#crud-頁面排版模式2026-05-28e-全系統統一改-form-overlay) 全段重寫，列出 4 feature 對齊、共用 shell API、deprecated pattern 清單
    - [visual-design.md「Form Overlay」段](design/visual-design.md#form-overlay編輯彈窗2026-05-28e-取代-side-sheet-成為-createedit-唯一-pattern) 新增 spec；舊「Side Sheet」段加 deprecated 標記
  - **保留**：`/signups/new`、`/signups/:id/edit` route 作 deep link fallback（重整或外部連結進入仍能用）；`signup-search-state` + `markStale` 邏輯沿用（list 用 overlay 後其實不再需要 markStale，但 route fallback 仍會用）
  - **驗證**：每 step 都 `tsc + ng build` 通過；最終 initial 312.86 kB、signup-list-page 108.5 kB、signup-edit-page 3.1 kB（從 20 kB 大幅縮減）、believers 17.7 kB、categories 9.7 kB、admins 7.9 kB
  - **背後資料**：backend 完全沒動，148 unit + 60 integration 持平

- [x] **搜尋功能對齊舊系統 LoadSearchSignups + bug 修正** — Done 2026-05-28
  - **Backend bug fix**（[SignupRepository.cs](../backend/src/Ceremony.Infrastructure/Repositories/SignupRepository.cs)）：
    - `SignupType = -1` 視為「全部」不過濾（對齊舊 [SignupForm.cs:828](../reference/old/Ceremony/SignupForm.cs#L828) `(int)dlSearchSignupType.SelectedValue != -1`）；之前後端會送 `WHERE SignupType = -1` → 永遠 0 筆 ❌
    - `Number = 0` 視為「無篩選」（對齊舊 [SignupForm.cs:829](../reference/old/Ceremony/SignupForm.cs#L829) `nudSearchNumber.Text != "0" && != ""`）
    - `SearchByNumberRangeAsync` 同步加 -1 處理
  - **Frontend 對齊舊邏輯**（[signup-list-page.ts](../frontend/src/app/features/signups/signup-list-page.ts)）：
    - 「啟用」label → 「範圍」（舊 [Designer.cs cbIsScope.Text](../reference/old/Ceremony/SignupForm.Designer.cs) = "範圍"；只控年份 `>=` vs `==`）
    - 預設 `year = null`（舊 txtSearchYear 起始空白），不再自動填當年
    - 預設 `scopeName = false`（4 個 scope checkbox 全部 unchecked）
    - **不自動執行搜尋** — `ngOnInit` 改只 `loadCategories()`；對齊舊 SignupForm constructor 沒呼叫 `LoadSearchSignups()`
    - **scope checkbox 連動 key textbox**：當 4 個 scope 全部 unchecked → key 控件 `disable() + setValue('')`；任一勾選 → `enable()`（對齊舊 [SignupForm.cs:730-780](../reference/old/Ceremony/SignupForm.cs#L730-L780) `cbSearchName_CheckedChanged` + `EnabledSearchKey`）
    - 用 `valueChanges + takeUntilDestroyed` 訂閱 4 個 scope 控件值
    - **初始空狀態文案**：「請設定搜尋條件後點「搜尋」」（hasSearched signal）；搜過才顯示「無資料，請重新搜尋！」
  - **驗證**：backend dotnet build ✅；148 unit + 60 integration 全綠；frontend tsc + ng build ✅ 0 warnings

- [x] **報名維護清單三項微調：單一水平 scroll、表格填滿視窗、底色欄位隔線加深** — Done 2026-05-28
  - **單一水平 scroll**：
    - 拆掉外層 `.vgrid-scroll-wrap` 的 `overflow-x: auto`（之前 viewport 內 + 外層各 1 條 h-scroll，共 2 條）
    - 改成 cdk-virtual-scroll-viewport 本身的 `cdk-virtual-scrollable` `overflow: auto` 同時負責 h + v scroll（只剩 1 條 h + 1 條 v）
    - header 移到上方獨立的 `.vgrid-header-clip { overflow: hidden }`，內層 `.vgrid-header` 設 `[style.width.px]=totalGridWidth`
    - viewport 滾動 → `(scroll)` 事件 → `headerInner.style.transform = translateX(-scrollLeft)` 同步 header 水平位移
    - ViewChild 抓 `CdkVirtualScrollViewport` + header 元素以驅動 sync
  - **表格高度填滿視窗（距底 12px）**：
    - shell `.content` `padding-bottom` 從 16px → 12px（precision 對齊使用者要求；其他 padding 不變）
    - signup-list-page 改 flex chain：`:host { height: 100% }` → `.page { display: flex; flex-direction: column; height: 100% }` → `.results-card { flex: 1; min-height: 0; display: flex; flex-direction: column }` → `.vgrid-zone { flex: 1; min-height: 0 }` → viewport `flex: 1`
    - 移除 viewport hard-coded `height: 600px`；現由 flex 自動填滿剩餘空間
  - **底色欄位隔線加深**：`.vgrid-td.dead { border-right-color: var(--c-text-disabled) }` (#B3AC9C，比原 `--c-border-soft` #E8E2D3 深 30%；在 `--c-dead-name-bg` #EFDCC4 橙底上對比 ≥ AA)；header `.vgrid-th` 也改用 `--c-border`（深一階）
  - **驗證**：`tsc --noEmit` ✅；`ng build` ✅ 0 warnings；signup-list-page chunk 103 → 104 kB（極小增量）

- [x] **報名維護 — virtual scroll + 欄寬持久化 + cbShowAll 持久化 shipped** — Done 2026-05-28
  - **virtual scroll**：把 `<table>` 重寫成 CDK Virtual Scroll + div-grid
    - 引入 `@angular/cdk/scrolling` `ScrollingModule`
    - 列固定 `itemSize=26`，viewport 高 600px；只渲染可視 ~25 列（資料量再多也維持輕量 DOM）
    - 用 div-grid（`display: grid; grid-template-columns`）對齊各欄；保留 `role="table/row/cell"` 確保 a11y
    - 取捨：失去 `<table>` 結構但換來 virtual scroll；舊有 sticky header / 選取背景 / 往生欄底色全保留
  - **欄寬持久化**（localStorage `ceremony.signupList.colWidths`）：
    - 每欄抽出 `SignupColumnDef`（id / label / width / toggleOnly / cellClass / accessor / resizable）放於 [signup-columns.ts](../frontend/src/app/features/signups/signup-columns.ts)
    - signal `columnWidths: Record<string, number>` + effect 自動 sync localStorage
    - 每欄 header 右緣 6px `.vgrid-resize` handle，pointer-drag 即時調整；clamp 32–600px
    - 「重設欄寬」按鈕（results-header）→ `columnWidths.set({})` 回預設
    - reload 後沿用上次寬度
  - **cbShowAll 持久化**（localStorage `ceremony.signupList.showAll`）：
    - signal `showAll` 從 localStorage 初始化；effect 自動 sync；reload 沿用上次狀態
  - **共用樣式抽到全域** [styles.scss](../frontend/src/styles.scss)：
    - `.vgrid-scroll-wrap / .vgrid-stack / .vgrid-header / .vgrid-th / .vgrid-row / .vgrid-td / .vgrid-resize / .vgrid-viewport` — 整套 virtual grid 樣式可給未來其他 list page 共用
    - 配合 `.dense-controls / .pane / .data-table.dense / .kebab-btn`，全域 reusable primitive 共 9 組
  - **共用工具抽出**：[shared/util/avoid-four.ts](../frontend/src/app/shared/util/avoid-four.ts) `formatAvoidFour(n)`（pipe 內部改 delegate），讓 column accessor 可重用避4 邏輯
  - **驗證**：`tsc --noEmit` ✅；`ng build` ✅ 0 warnings；signup-list-page chunk 83 kB → 103 kB（virtual scroll + column defs 額外 +20 kB）；initial 291 kB → 310 kB（含 ScrollingModule）
  - **規格同步**：[visual-design.md DataGrid 實作段](design/visual-design.md#datagrid-實作2026-05-28d-virtual-scroll--欄寬持久化) 補完整實作說明 + 為何不用 `<table>` 的 trade-off

- [x] **報名維護 toolbar + 清單欄位緊湊版對齊舊系統** — Done 2026-05-28
  - **動機**：先前 ship 的 toolbar 是堆疊式（filter card / batch card / results 三層）+ 11 欄精簡清單，使用者反映「太佔空間 / 欄位太少」
  - **toolbar 改 3-panel 並排**（[search-pane 615px] + [print-pane 203px] + [action-pane 126px] 對齊舊 [SignupForm.Designer.cs:336-587](../reference/old/Ceremony/SignupForm.Designer.cs#L336-L587) 三 panel）：
    - **搜尋 pane** 3 列 6 欄 dense grid：Row1 ☑啟用/年份/scope4 連勾、Row2 ☑顯完整/法會/關鍵字/☑固定編號、Row3 類型/編號/匯出 Excel；右側「搜尋」按鈕縱跨 3 列（對齊舊 btnSearch 75×99）
    - **列印 pane** 2×3 grid：Row1 起編號~迄編號、Row2 reportType；右側「列印」按鈕縱跨 2 列（對齊舊 btnPrint 75×63）
    - **動作 pane** 縱向 stack：「+ 新增報名」「✎ 修改報名」（後者僅單選啟用，對應舊 btnEdit）
    - 控件高度統一 `--control-height` (28px)、font 12px、row gap 4px — 整條 toolbar ~110px 高
  - **清單欄位擴成 27 預設 / 32 含完整**（對齊舊 cbShowAll 邏輯）：
    - 預設 27：年份 / 法會 / 類型 / 編號 / 姓名 / 備註 / 往生 1,2,3,3-1,5 / 陽上 1,2,3,3-1,5 / 預繳年份 / 預繳法會 / 聯絡電話 / 寄件城市,區域,地址 / 文牒城市,區域,地址 / 編輯者 / 編輯日期
    - 顯完整 +5：費用 / 員工 / 堂號 / 往生6 / 陽上6（對齊 [SignupForm.cs:782-797](../reference/old/Ceremony/SignupForm.cs#L782-L797)）
    - 表頭 sticky / `width: max-content` / row 高 ≈ 26px (`padding 4px 6px`) / font 12px
    - 往生欄位背景 `--c-dead-name-bg` (#EFDCC4)；選取列覆蓋層 `--c-row-selected` (#F5E5DC) + `color-mix` 處理交集
  - **共用樣式抽到 [styles.scss](../frontend/src/styles.scss) 全域**（避免 SCSS budget 撞 4kB）：
    - `.dense-controls` — 統一 input/select 28px 高、`.lbl` 文字標、`.chk` checkbox
    - `.pane` — 緊湊式 fieldset 樣式
    - `.data-table.dense` — sticky thead、dead-cell 背景、selected row 規則、`color-mix` 交集
    - `.kebab-btn` — row 列尾 ⋮ 按鈕
  - **驗證**：`tsc --noEmit` ✅；`ng build` ✅ 0 warnings；signup-list-page chunk 83 kB；signup-list-page.scss 縮到 ~3.0 kB
  - **規格同步**：[visual-design.md 報名維護頁面](design/visual-design.md#報名維護頁面signups2026-05-28c-緊湊版對齊舊-signupform-三-panel) 全段重寫 + 欄位清單與表頭規則

- [x] **bug fix：SignupListItem 加 BelieverId 欄位，「代入新增」才能正確預填 form** — Done 2026-05-28
  - **問題**：先前 ship 的代入新增流程把 `?fromSignupId=` query 帶過去後，edit page 撈 signup 但拿不到 `believerId`（後端 DTO 沒有此欄位），導致表單卡在 `Validators.required('believerId')`
  - **修補**：
    - backend `SignupListItem` record 加 `BelieverId: Guid?` 欄位（位於 Employee 後、Name 前）
    - `SignupRepository` SearchAsync / GetByIdAsync / SearchByNumberRangeAsync 三條 SQL `SELECT` 加 `BelieverID`
    - inline mapper + `MapRow` 兩處補映射（`d["BelieverID"] as Guid?`）
    - 2 個 unit test mock 補參數（`CreateSignupHandlerTests` × 2、`BatchReportHandlerTests` × 1）
    - frontend `SignupListItem` interface 加 `believerId: string | null`
    - `signup-edit-page` `prefillFromSignup`：只帶信眾相關欄位（**不**含 year/ceremony/type，使用者要重選），`applyItem`（edit mode）也補 believerId
  - **驗證**：backend dotnet build 0 warnings；148 unit + 60 integration 全綠；frontend tsc + ng build 0 warnings

- [x] **報名維護 grid 右鍵 + 列印 UI 對齊舊系統 shipped** — Done 2026-05-28
  - **共用元件**：
    - `<app-context-menu>` ([shared/context-menu/](../frontend/src/app/shared/context-menu/)) — CDK Overlay-based、座標/element 兩種 origin、鍵盤導航 (↑↓Enter Esc)、`enabledWhen` 回傳 `{enabled, reason}` 自動 disable + tooltip
    - `<app-confirm-dialog>` ([shared/confirm-dialog/](../frontend/src/app/shared/confirm-dialog/)) — modal dialog 取代 `confirm()`；支援 danger 樣式；service-based promise API
    - Icon 庫補 6 個：`pencil / trash / history / file-plus / more / close`
    - styles.scss 加 `@import '@angular/cdk/overlay-prebuilt.css'`
  - **[signup-list-page](../frontend/src/app/features/signups/signup-list-page.ts) 改造**：
    - 列首 checkbox + header tri-state（all / partial / none，indeterminate 屬性手動同步）
    - 9 項 context menu：代入新增 / 修改 / 列印 5 種 / 刪除 / 瀏覽歷程（對齊 cmsSignups Designer.cs:236-313）
    - 「列印普桌」grey-out 規則：signupType filter ≠ 4 → disable + reason「僅普桌類型 (4) 可列印」
    - 右鍵（contextmenu event）+ 列尾 ⋮ kebab button + 上方「對選取項目」bulk button 三入口
    - 多筆列印走 min..max 區間 + 含非選取 warning dialog；單筆走 single endpoint
    - 刪除：danger confirm dialog（單筆顯示完整資訊；多筆顯示「N 筆」）
    - 批次列印獨立 panel（起 / 迄編號 + reportType）接 `POST /reports/batch`，沿用搜尋區 filter
    - 「代入新增」navigate `/signups/new?fromSignupId=:id`
  - **[signup-edit-page](../frontend/src/app/features/signups/signup-edit-page.ts) 補 query 預填**：讀 `?fromSignupId=` query 從現有 signup 撈出資料填入表單（不帶 number / year，使用者從 step1 重選）
  - **驗證**：`tsc --noEmit` ✅ exit 0；`ng build` ✅ 0 warnings；signup-list-page chunk 14kB → 77kB（含 CDK Overlay + 兩個 shared 元件）；initial 282kB → 291kB
  - **規格已記錄**：[blueprints/signup-management.md 右鍵段](blueprints/signup-management.md#grid-context-menucmssignups-等價新版重現) / [visual-design.md 報名維護頁面](design/visual-design.md#報名維護頁面signups2026-05-28-補右鍵--列印-ui-規格) / [frontend-design.md Grid Context Menu Pattern](design/frontend-design.md#grid-context-menu-pattern2026-05-28-補規格)

- [x] **Sidecar 部署架構決策**：Electron + .NET self-contained API 打成同一 .exe；DB 集中、認證走方案 C（純文字 JSON config）— Decided 2026-05-28
  - 取代原「server-side API + thin client」規劃；IT 維護門檻最低
  - Doc 同步：
    - [infrastructure.md 部署型態](design/infrastructure.md#部署型態2026-05-28-改為-sidecar-架構) — 全段重寫 + 環境變數 + 部署單元
    - [frontend-design.md Sidecar 部署模型](design/frontend-design.md#sidecar-部署模型2026-05-28-決策) — `electron/` 目錄補 `sidecar.ts` / `config.ts` / `ipc/setup.ts`
    - [security.md Sidecar 模式 DB 認證決策](design/security.md#sidecar-模式-db-認證決策2026-05-28) — 三方案對照 + 風險緩解（最小權限 DB 帳號 / LAN-only / 升級路徑保留）
    - status.md P3 backlog「Electron 包裝」展開實作項目 + 前置條件 + 估時

- [x] **Nav 命名：「報名查詢」→「報名維護」**（與「信眾維護」/「法會類型」一致；該頁同時含編輯/刪除/匯出，不只查詢）— Done 2026-05-28
  - 改動：[shell-layout](../frontend/src/app/core/layout/shell-layout/shell-layout.ts) sidebar、[dashboard-page](../frontend/src/app/features/dashboard/dashboard-page.ts) tile、[app.routes](../frontend/src/app/app.routes.ts) title + breadcrumb、[signup-list-page.html](../frontend/src/app/features/signups/signup-list-page.html) h1、[reports-preview-page.html](../frontend/src/app/features/reports/reports-preview-page.html) 返回連結
  - Doc 同步：[visual-design Nav 中文標籤對照](design/visual-design.md#nav-中文標籤對照2026-05-28-命名決策) + [frontend-design 側邊選單 active 規則](design/frontend-design.md#側邊選單-active-規則)

- [x] **信眾維護頁重新設計：single-column + side sheet 模式 + 全面 RWD（5 個斷點）+ 共用 sheet/empty-state 樣式抽到全域** — Done 2026-05-28
  - 廢除舊「左列表 + 右編輯欄」split-view，改為「single-column + 右側滑入 sheet」結構（[visual-design 信眾維護頁面](design/visual-design.md#信眾維護頁面believers2026-05-28-重新設計)）
  - RWD 5 斷點：1200/1000/700/600px，依寬度自動隱欄、堂號塞入姓名下、sheet 在 mobile 滿版、表單 2→1 欄、名單 3→2 欄
  - **新共用樣式**（[styles.scss](../frontend/src/styles.scss)）— 抽出 reusable primitive：
    - `.sheet-*`：抽屜元件（backdrop / sheet / header / form / body / actions / close-btn）+ fadeIn / slideIn 動畫 keyframes
    - `.empty-state` / `.empty-illustration` / `.empty-title` / `.empty-hint`（也讓 reports preview 共用）
    - `.hide-sm` / `.hide-md` / `.hide-lg` / `.show-md` 響應式 helper class
  - **新 doc 段**：[frontend-design CRUD 頁面排版模式](design/frontend-design.md#crud-頁面排版模式2026-05-28-標準化) + [響應式策略](design/frontend-design.md#響應式策略rwd) + [visual-design Side Sheet 規格](design/visual-design.md#side-sheet編輯抽屜2026-05-28-新增)（未來新增 CRUD 頁面照此模式）
  - **驗證**：`npm run build` ✅ 0 warning（信眾頁 SCSS 抽到全域後從 4.79kB 降到 ~3.0kB，未撞 4kB budget）

- [x] **前端 UI 打磨：SVG icon component、移除介面英文 form 名、列印預覽頁重新設計、sidebar active fix、API port 5050** — Done 2026-05-28
  - **API port 改 5050**：[backend launchSettings.json](../backend/src/Ceremony.Api/Properties/launchSettings.json)、[Ceremony.Api.http](../backend/src/Ceremony.Api/Ceremony.Api.http)、[frontend environment.ts](../frontend/src/environments/environment.ts) 同步從 `5084 → 5050`
  - **Sidebar active route fix**：在 `/signups/new` 不再誤亮「報名查詢」 — `NavItem` 加 `exact?: boolean` flag；`/signups` + `/signups/new` 開 exact，其他保持 prefix 比對（[frontend-design.md 側邊選單 active 規則](design/frontend-design.md#側邊選單-active-規則)）
  - **介面去英文**：sidebar / dashboard 不再顯示 `BelieverForm` / `SignupForm` 等英文 form 名（[visual-design.md UI 文字 vs 程式識別](design/visual-design.md#ui-文字-vs-程式識別2026-05-28-決策)）
  - **統一 icon 系統**：新增 [shared/icon/icon.component.ts](../frontend/src/app/shared/icon/icon.component.ts) — inline SVG / 24×24 / currentColor / `<app-icon [name]="..." [size]="20" />`；取代之前混用 emoji + Unicode 造成的大小不一；目前 8 個：`believer/plus/search/download/category/printer/settings/home`
  - **Dashboard 入口磚**改成 icon + label，移除英文小字
  - **列印預覽頁完全重新設計**（[visual-design.md 列印預覽頁面](design/visual-design.md#列印預覽頁面reportspreview2026-05-28-重新設計)、[frontend-design.md 列印預覽 / 匯出](design/frontend-design.md#列印預覽--匯出2026-05-28-重新設計)）
    - 從「左 320px 表單 + 右滿版預覽」改成**垂直堆疊**：上方 tab 切單筆 / 批次 + 緊湊水平表單，下方滿寬 PDF iframe
    - 表單列：水平 flex + 各欄 `min-width`，批次模式允許 wrap；submit 按鈕固定在最右
    - 預覽工具列三個按鈕：**新分頁開啟** / **下載** / **關閉**（關閉清掉 blob URL 回空狀態）
    - 空狀態：📄 + 「尚未產生 PDF」+ 提示文字
  - **新增 gotcha 紀錄**：CSS Grid `1fr` 被 iframe / nowrap 撐爆 → 用 `minmax(0, 1fr)` + `min-width: 0`；position: sticky + 100vh 造成「覆蓋」視覺（[gotchas.md](gotchas.md)）
  - **驗證**：`npm run build` ✅ 0 warning；TS 全綠

- [x] **Angular 前端 — 9 條 feature 路由全部串接完成（取代 PlaceholderPage）+ HTTP 基礎建設 + 共用樣式** — Done 2026-05-28
  - **HTTP 基礎建設**：
    - [environment.ts / environment.prod.ts](../frontend/src/environments/) — dev base URL `http://localhost:5050/api/v1`、prod `/api/v1`
    - [core/http/api-error.ts](../frontend/src/app/core/http/api-error.ts) — `ApiError` typed class，把 backend 的 `{errorCode, message, traceId}` payload 包裝為可讀錯誤；含 NETWORK_ERROR 路徑
    - [core/http/auth.interceptor.ts](../frontend/src/app/core/http/auth.interceptor.ts) — 對 `apiBaseUrl` 開頭 request 自動注入 `Authorization: Bearer`；遇 401 自動 `clearSession + /login` redirect（login endpoint 例外）
  - **AuthStore 真實串接**（[auth.store.ts](../frontend/src/app/core/auth/auth.store.ts)）：
    - `login` 改打 `POST /api/v1/auth/login`，model 對齊 backend `LoginRequest { username, password }` + `LoginResponse { token, user{id, username, name} }`
    - 新增 localStorage 持久化（key `ceremony.auth.v1`），reload 保留 session
    - `logout` 先打 `POST /api/v1/auth/logout`（token 撤銷），catch 後做本地清除（避免已過期 token 卡住）
    - 新增 `clearSession` method 給 interceptor 401 路徑使用
  - **typed API service 層** [core/api/](../frontend/src/app/core/api/)（依 backend Application contract 1:1 映射）：
    - `signups` (search / get / logs / create / update / delete / export-excel)
    - `believers` (search / get / create / update / delete)
    - `categories` (list / create / update / delete)
    - `admins` (list / create)
    - `prepay` (load)
    - `reports` (single 5 + batch；均 `responseType: 'blob'`，自動從 `Content-Disposition` 抽 filename)
  - **9 條 feature 路由全部串接**（取代 `PlaceholderPage`）：
    - `/login` → [LoginPage](../frontend/src/app/features/login/login-page.ts) 改用 `username` 欄位、顯示 backend verbatim 中文錯誤
    - `/categories` → [CategoriesPage](../frontend/src/app/features/categories/) — 兩層樹 + create-root/create-child/edit/delete
    - `/admins` → [AdminsPage](../frontend/src/app/features/admins/) — list table + create form 含確認密碼比對
    - `/believers` → [BelieversPage](../frontend/src/app/features/believers/) — 5 欄搜尋 + 右側編輯 pane + 6 元素名單（往生格底色 #EFDCC4）
    - `/signups` → [SignupListPage](../frontend/src/app/features/signups/signup-list-page.ts) — 11 個搜尋條件對齊舊 PredicateBuilder + Excel 匯出 + 編輯/歷程/刪除 action
    - `/signups/new` + `/signups/:id/edit` → [SignupEditPage](../frontend/src/app/features/signups/signup-edit-page.ts) 單元件雙用（route param 區分 mode）+ 信眾搜尋 modal picker
    - `/signups/:id/logs` → [SignupLogsPage](../frontend/src/app/features/signups/signup-logs-page.ts) — log card list
    - `/prepay` → [PrepayPage](../frontend/src/app/features/prepay/) — 5 個下拉 + 6 個分組 + KPI result + filled gaps
    - `/reports/preview` + `/reports/preview/:type` → [ReportsPreviewPage](../frontend/src/app/features/reports/) — 單筆 + 批次兩 form + `<iframe>` 嵌 PDF blob + 下載
  - **共用元件**：
    - [shared/pipes/avoid-four.pipe.ts](../frontend/src/app/shared/pipes/avoid-four.pipe.ts) — `4 → 3-1` 顯示 pipe
    - [shared/util/categories.ts](../frontend/src/app/shared/util/categories.ts) — `flattenCategories(tree)` 轉 dropdown
    - [shared/util/signup-type.ts](../frontend/src/app/shared/util/signup-type.ts) — 5 種報名類型常數
    - [shared/util/prepay-groups.ts](../frontend/src/app/shared/util/prepay-groups.ts) — 6 個分組常數
    - [shared/util/taiwan-year.ts](../frontend/src/app/shared/util/taiwan-year.ts) — `currentTaiwanYear()` helper
  - **全域樣式擴充**（[styles.scss](../frontend/src/styles.scss)）：新增 `.btn` / `.btn-primary` / `.btn-danger` / `.btn-sm` / `.alert` / `.hint` / `.data-table` / `.field` / `.card` / `.toolbar` utility classes（避免每頁 SCSS 撞 4kB budget）
  - **ShellLayout 側欄**：新增「列印預覽」入口
  - **依賴新增**：`@angular/animations` ^21.1.0（補上 `provideAnimationsAsync` 缺的 peer）
  - **驗證**：
    - `npx tsc -p tsconfig.app.json --noEmit` ✅ exit 0
    - `npm run build` ✅ 282.80 kB initial / 0 warnings；9 個 lazy chunk（每 feature 一個）
    - SCSS budget 全綠（最大 signup-edit-page 4.0 kB 內）
  - **已知 TODO（後續迭代）**：
    - signup edit 模式目前不重打 `/believers/:id` 取出完整 belief 資料；用 SignupListItem 投影回填，足夠顯示但不可變更
    - reports preview 用瀏覽器內建 `<iframe>` 直接渲染 PDF；PDF.js 套件升級可在後續加入註解/縮放需求時引入
    - 無 unsavedChangesGuard（signup edit 離開不警告）
    - 列表沒做分頁/虛擬滾動；現有 TOP 200 limit 已足
    - Material 元件目前 0 個（純自製 CSS）；如有 mat-table / mat-dialog 需求再導入
  - **進度看板**：前端 placeholder 9 → **0**；feature page shipped 0 → **9**（含登入登出整個 auth flow 全打通）

- [x] **`POST /api/v1/auth/logout` shipped — JWT 黑名單 (jti + TTL=剩餘壽命) + 7 unit + 4 integration tests** — Done 2026-05-27
  - **新 endpoint**：`POST /api/v1/auth/logout`（新需求；舊 WinForms close form 無 server 介入）
    - Request：空 body；token 由 `Authorization: Bearer` header 提供
    - Response：`{ "ok": true }`；後續同 token 請求 401
    - `[Authorize]` 保護；未帶 token / 已撤銷 token → 401
  - **新元件**：
    - `IJwtBlacklist` / `MemoryJwtBlacklist` — IMemoryCache 實作，與既有 `LoginFailureTracker` 同模式；前綴 `jwt-blacklist:`；空 jti 或 ttl ≤ 0 為 no-op
    - `LogoutHandler.Handle(ClaimsPrincipal)` — 從 jti + exp claims 讀取，TTL = exp - now（過期則跳過避免無謂寫入）；exp 缺失 fallback 30 分鐘
    - `JwtBearerEvents.OnTokenValidated` 攔截每筆請求查黑名單，命中 → `ctx.Fail("Token revoked")` → 401
  - **JWT 結構未動**：`JwtTokenService.Issue` 早就含 `jti` claim（隨機 Guid），這次只是利用它
  - **設計取捨**：
    - 黑名單儲存單機 `IMemoryCache`（多 instance 部署後再換 Redis）
    - 撤銷粒度：per-jti（單 token），不影響該使用者其他裝置
    - process 重啟：黑名單清空 → 已撤銷但未到期 token 暫恢復可用（window ≤ token 剩餘壽命，可接受）
    - 「全部裝置登出」不支援（如需要再加 `/logout-all` 黑名單整個 sub）
  - **Tests +11**（總 148 unit + 60 integration = **208**）：
    - `LogoutHandlerTests` 4 case (missing jti → no-op / expired token → no-op / valid token TTL ≈ remaining lifetime / missing exp → 30 min fallback)
    - `MemoryJwtBlacklistTests` 3 case (basic revoke/check / empty jti not stored / zero/negative ttl no-op)
    - Integration +4: 401 without token / **login → call /admins 200 → logout → same token /admins 401** end-to-end / idempotent logout / **two parallel sessions: logout t1 doesn't affect t2** (verify different jti per login)
  - **進度看板**：endpoints 27 → **28 shipped**；tests 197 → **208**
  - **Secret 驗證**：`grep` 仍 0 命中 ✅

- [x] **批次列印 `POST /api/v1/reports/batch` shipped — PdfSharp 6.2.4 合併 + ReportModelBuilders 共用層 + 6 unit + 5 integration tests** — Done 2026-05-27
  - **新 endpoint**：`POST /api/v1/reports/batch`（SignupForm.cs:447-653 + :1698-1722）
    - Request：`{ reportType, numberStart, numberEnd, year?, yearGte?, ceremonyCategoryId?, signupType? }`
    - 對齊舊 nudStart/nudEnd + txtSearchYear + cbIsScope + dlSearchCeremony + dlSearchSignupType
    - Response：合併後 PDF + `X-Signup-Count` header + `Content-Disposition: filename="batch-<type>-<start>-<end>.pdf"`
  - **新 Repository**：`SignupRepository.SearchByNumberRangeAsync(SignupRangeQuery)` 用 Dapper 動態組 WHERE；ORDER BY Number；走既有 `dbo.SignupView`
  - **新 PDF 合併**：`IPdfMerger` (Application) + `PdfSharpMerger` (Infrastructure)；PdfSharp 6.2.4 cross-platform 移植自舊 .NET Framework 的 CombinePDFs，逐頁 `AddPage` 邏輯不變
  - **新共用層** `ReportModelBuilders`（internal static）：5 個 `Build<Type>Model(SignupListItem)` helper；同步重構 5 個單筆 handler (`DataCard / Receipt / Tablet / Text / Worship`) 改用此 helper，消除 model 建構重複；avoid4 / hallName split / address join 仍走既有 `SignupReportContext`
  - **行為亮點**：
    - **Worship 防呆**：reportType=worship 強制 `SignupType=4` filter（即使呼叫端傳其他值）— 比舊系統嚴格（舊系統會崩潰）
    - **reportType 大小寫不敏感 + trim**（`"  TABLET "` → `tablet`）
    - **range 驗證 verbatim**：`numberEnd < numberStart` → 400「編號錯誤」對齊 SignupForm.cs:454 MessageBox
    - **空結果 404 `BATCH_NO_SIGNUPS`** 「查無符合條件的報名資料」（舊系統會印空 PDF 並崩潰）
    - **❌ 故意捨棄** CustomDialogForm「列印格式 PDF / 預覽列印」對話 — API 統一回 PDF byte，預覽由前端 PDF.js 處理
  - **新套件**：`PDFsharp` 6.2.4（+ `System.Security.Cryptography.Pkcs` 8.0.1 transitive）
  - **Tests +11**（總 141 unit + 56 integration = **197**）：
    - `BatchReportHandlerTests` 6 case (invalid range / 3× invalid reportType theory / no signups / datacard merges N pages / **worship 強制 SignupType=4** / reportType case-insensitive + trim / filter forwarding all 6 params)
    - Integration +5: 401 / 400 invalid range verbatim「編號錯誤」/ 400 invalid reportType verbatim「報表類型錯誤」/ 404 `BATCH_NO_SIGNUPS` / **datacard 真實 DB 多頁 PDF 含 X-Signup-Count header**
  - **Manual smoke (real DB) end-to-end**：
    - batch datacard `numberStart=1 numberEnd=3 year=115 signupType=1` → 239KB PDF 6 pages（命中 3×ceremonies = 6 signups）
    - batch receipt 同範圍 → 6 pages
    - batch worship `numberStart=1 numberEnd=200` → **2634-page PDF**（單一回應；強制 SignupType=4 filter）
    - invalid reportType → 400 verbatim「報表類型錯誤」
    - no signups → 404 `BATCH_NO_SIGNUPS`
  - **進度看板**：endpoints 26 → **27 shipped**；tests 183 → **197**；coverage SignupForm 37%→**42%**（rows 16, 33 ✅）
  - **Secret 驗證**：`grep` 仍 0 命中 ✅

- [x] **5 個新 endpoint shipped — 列印 4 變體 (Receipt/Tablet/Text/Worship) + Backup；PrintTemplateSelector 純函式 + 23 variant 測試** — Done 2026-05-27
  - **5 個新 endpoint**：
    - `GET /api/v1/reports/receipt?signupId=`（SignupForm.cs:1052-1146）
    - `GET /api/v1/reports/tablet?signupId=`（SignupForm.cs:273-321 + 1148-1333；9 變體選擇）
    - `GET /api/v1/reports/text?signupId=`（SignupForm.cs:323-378 + 1335-1552；2 變體）
    - `GET /api/v1/reports/worship?signupId=`（SignupForm.cs:380-403 + 1554-1696；6 變體；**僅 SignupType=4 開放**，其他 422 `WORSHIP_ONLY_TYPE_4`）
    - `POST /api/v1/backup`（MainForm.cs:95-113；`BACKUP DATABASE [Ceremony] TO DISK = N'...' WITH FORMAT, INIT, COMPRESSION`，路徑 `Backup:Directory` config + `Ceremony-{yyyyMMddHHmmss}.bak`；config 缺 → 500 `BACKUP_NOT_CONFIGURED`）
  - **新 Domain Service** `PrintTemplateSelector`（refactored from `SignupForm.cs:1148-1696` 共 ~550 行 switch）：
    - `ChooseTablet(deadNames, livingNames) → (TabletTemplate, paraFontSize)` 9 變體 + `dead.Length > 7` → 0.6cm 邏輯
    - `ChooseText(deadNames) → TextTemplate` (2 dead → Two，其他 Base)
    - `ChooseWorship(livingNames) → WorshipTemplate` 6 變體（按 LivingName 最高位）
    - 純函式、可單測；舊系統 switch 全部入 enum + tuple
  - **4 新 Renderer**（`Ceremony.Infrastructure.Reporting`）：
    - `ReceiptRenderer` 21×29.7cm 直；上+下聯各 ~10cm；14pt 主資訊 / 16pt 郵寄標籤 / 0.6cm Name
    - `TabletRenderer` 11.5×25.4cm 窄；ParaFontSize 從 selector 取（0.6 / 0.8cm）；HallNameFirst/Second at 6.10cm Top
    - `TextRenderer` 36.5×26.2cm 橫；Number 1cm Bold；PhotoAddress 暫以文字繪（PNG TODO）
    - `WorshipRenderer` 21×29.6cm 直；2×3 LivingName 矩陣（右至左）；One/Two/Three 變體字級 3cm，其餘 2cm；背景 worship2.png TODO
  - **新 Application 元件**：
    - `IReportRenderer` 介面擴充 4 個 method + 4 個 Model（ReceiptModel / TabletModel / TextModel / WorshipModel）
    - `GenerateReceiptHandler` / `GenerateTabletHandler` / `GenerateTextHandler` / `GenerateWorshipHandler` 共用 `SignupReportContext` helper（Extract + SplitHallName + AddressOf）
    - `Ceremony.Application.Backup`：`BackupRequest` / `BackupResponse` / `IBackupService` / `BackupHandler`
  - **新 Infrastructure** `SqlBackupService` + DI 註冊 5 個 handler + 4 個 renderer + 1 個 service
  - **API 擴充**：
    - `ReportsController` 5 actions（datacard / receipt / tablet / text / worship）回 `File(pdf, "application/pdf", "<prefix>-<signupId>.pdf")`
    - `BackupController` `POST /api/v1/backup` (`[Authorize]`)
    - `ExceptionMiddleware` 新增 `WORSHIP_ONLY_TYPE_4` → 422、`BACKUP_NOT_CONFIGURED` → 500
  - **Tests +26**（總 132 unit + 51 integration = **183**）：
    - `PrintTemplateSelectorTests` 18+ case (9 Tablet variants 含 0.6/0.8cm 切換 + 5 Text + 5 Worship 含 sparse high-end)
    - `ReportsEndpointsTests` 重構 → 共用 `AssertReportEndpoint` helper；新增 receipt / tablet / text 各 1 PDF magic byte 測試 + worship non-type-4 → 422 verbatim `WORSHIP_ONLY_TYPE_4`
  - **Manual smoke (real DB)**：
    - receipt 11479 byte PDF / tablet 12463 byte / text 34940 byte / worship-on-type-1 → 422 / worship-on-real-type-4 → 845 byte PDF
    - 全部以 `%PDF` magic byte 驗證為 real PDF
  - **已知 TODO（變體精修）**：
    - Tablet 9 variant、Text 2 variant、Worship 6 variant 目前都用 base variant 座標；variant-specific 細部 layout 待客戶實機印表測試後精修
    - worship2.png 嵌入背景；PhotoAddress 25×605px 垂直地址 PNG（暫以文字繪）
  - **進度看板**：endpoints 21 → **26 shipped**；tests 160 → **183**；coverage SignupForm 12%→**37%**（8 個新 ✅ + AvoidFourFormatter row 41）/ MainForm 0%→**13%**（backup row 8 ✅）
  - **Secret 驗證**：`grep` 仍 0 命中 ✅

- [x] **列印 PoC 完成 — RDLC 否決、QuestPDF 定案、`GET /reports/datacard` shipped** — Done 2026-05-27
  - **RDLC 評估（cli 實測）**：在 `/tmp/rdlc-eval` 建獨立 .NET 10 console，試 3 個套件：
    - `ReportViewerCore.NETCoreRuntime`：NuGet 上找不到（已下架）
    - `AspNetCore.Reporting` 2.1.0：可裝，但 runtime throw `FileNotFoundException: System.Security.Permissions` (.NET Framework GDI+ 相依)
    - `FastReport.OpenSource`：裝得起來但僅支援 .frx 不支援 .rdlc
    - **結論：RDLC 重用在 .NET 10 不可行**
  - **QuestPDF 路徑實作**：
    - 新 namespace `Ceremony.Infrastructure.Reporting` + `DataCardRenderer`（1:1 還原 `tmpDataCard.rdlc` 的 25 個 TextBox + 2 lines）
    - 座標單位 cm（QuestPDF 原生支援）；fontSize cm → pt 轉換
    - `IReportRenderer` 抽象介面 (Application) + `QuestPdfReportRenderer` 適配 (Infrastructure)
    - DI 註冊 + `QuestPDF.Settings.License = LicenseType.Community`（寺方營收 < $1M USD/yr 適用）
    - 新 handler `GenerateDataCardHandler` 整合 SignupView 資料 → DataCardModel
    - 新 controller `ReportsController` + `GET /api/v1/reports/datacard?signupId=...` 回 PDF binary
  - **行為亮點**：
    - **避4 顯示**：Number 4 → "3-1" 用既有 `AvoidFourFormatter`
    - **地址組合**：MailCity + MailZone + MailAddress（對齊舊版顯示）
    - **預繳顯示**：「預繳 {year} {ceremonyTitle}」
  - **已知 trade-off (記錄)**：
    - QuestPDF 2026.5.0 收回 SkiaSharp Canvas 公開 API（用 `object` 取代 `SKCanvas`）；dashed line 暫用 solid，待後續引入 PdfSharp/SVG image 或 QuestPDF 更新
    - 印表機實機對位需客戶實際印 1 張驗收（PoC PDF 已產出可印）
  - **新套件**：`QuestPDF` 2026.5.0、`SkiaSharp` 3.119.4
  - **Tests +3**：`ReportsEndpointsTests` (401 / 404 unknown / **real PDF check 含 PDF magic byte %PDF**)
  - **Manual smoke**：
    - 真實 signup 4219f98d... (黃耀章 115 春季 No-1)
    - 產 PDF 34849 bytes，`file` 識別 "PDF document, version 1.4, 1 pages"
    - `pdftotext` 抽出真實資料「台北市文山區汀洲路四段31號3樓」「備註：」確認 Chinese 渲染 + 正確 join 資料
  - **進度看板**：endpoints 20 → **21 shipped** (+ PoC datacard)；tests 157 → **160**
  - **回填 [printing-reports.md](blueprints/printing-reports.md)** 決策表格（v3 PoC 結論：QuestPDF）；剩 18 個 RDLC 模板照 DataCardRenderer 範本逐個實作

- [x] **6 個新 endpoint shipped — Signup write/delete + Categories CRUD + Excel export（剩下只有列印 PoC）** — Done 2026-05-27
  - **6 個新 endpoint + 6 blueprints**：
    - `PUT /api/v1/signups/:id`（EditSignupForm.cs:186-368，全欄位覆寫 + SignupLog + 同步 Believer 部分欄位）
    - `DELETE /api/v1/signups/:id`（SignupForm.cs:405-426，硬刪除）
    - `POST /api/v1/categories`（CeremonyCategoryForm.cs:94-114，含**兩層深度限制**）
    - `PUT /api/v1/categories/:id`（115-127，Title + Sort）
    - `DELETE /api/v1/categories/:id`（143-165，**雙重檢查**：Signups + 子分類）
    - `POST /api/v1/signups/export`（655-728，**32 欄 Excel xlsx 用 ClosedXML 取代舊 NPOI HSSF**）
  - **新 Repository 方法**：
    - `ISignupRepository`: `NumberExistsExcludingAsync` (excludeSignupId 排除自己) + `UpdateWithLogAsync` (transaction + Believer 部分欄位 + Signup 全欄位 + SignupLog) + `DeleteAsync`
    - `ICategoryRepository`: `GetByIdAsync` + `InsertAsync` + `UpdateAsync` + `HasDependencyAsync` (Signups COUNT + child COUNT) + `DeleteAsync`
  - **新套件**：`ClosedXML` 0.105.0（Application project）
  - **新 handlers**：`UpdateSignupHandler` / `DeleteSignupHandler` / `ExportSignupsHandler` / `CreateCategoryHandler` / `UpdateCategoryHandler` / `DeleteCategoryHandler`
  - **新 errorCode**：`CATEGORY_HAS_DEPENDENCY` (409) / `CATEGORY_DEPTH_LIMIT` (422)
  - **行為亮點**：
    - PUT signup 含「同步寫 Believer 部分欄位 + Signup 全欄位 + SignupLog audit」單一 transaction
    - DELETE signup 硬刪除，**SignupLog 仍保留**（audit 設計）
    - POST category 含 depth check：parent 已是第 2 層 → 422 `CATEGORY_DEPTH_LIMIT`
    - DELETE category 雙重檢查 verbatim「已有報名或還有下層法會，無法刪除」
    - Excel 用 ClosedXML 取代舊 NPOI HSSF（.xls → .xlsx）；32 欄順序對齊舊 line 670-700
  - **Tests +18**（總 113 unit + 44 integration = **157**）：
    - `CreateCategoryHandlerTests` 6 case (empty/too-long/unknown parent/depth limit/valid root/valid child)
    - `DeleteCategoryHandlerTests` 3 case (not found / dependency / success)
    - Integration: PUT 404 / DELETE 404 / **Excel xlsx 驗證 (content-type + 檔名 + ZIP header bytes)** / category CRUD lifecycle 含 POST→PUT→add child→DELETE parent 應 409→DELETE child→DELETE parent / depth limit 422
  - **Manual smoke (real DB)**：
    - Excel: 41851 byte xlsx，`file` 識別為 "Microsoft Excel 2007+"
    - Category CRUD：POST/PUT/DELETE 全綠
    - DELETE 春季根 → 409 + verbatim "已有報名或還有下層法會，無法刪除"
    - PUT signup → SignupLog audit 從 1 增至 2
  - **進度看板**：endpoints **14 → 20 shipped**；coverage：EditSignup 5%→**75%**, Signup 7%→**12%**, Category 36%→**64%**；tests 139 → **157**
  - **Secret 驗證**：仍 0 命中 ✅

- [x] **POST /api/v1/prepay/load shipped — 核心業務 780 行 switch 重構成 strategy table + idempotency + 真實 DB 驗證** — Done 2026-05-27
  - **這是整個系統最具業務價值的單一 endpoint，也是最複雜的舊邏輯重構**
  - **新 Domain Service**：`PrepayGroups` strategy table — 6 個 case data-driven（`(Code, Name, SignupType, EmployeeType?)`），取代舊系統 780 行 switch；可 mock / 可單測
  - **新 Repository**：`IPrepayRepository` + `PrepayRepository`（Dapper）
    - `GetCeremonyCategoryAsync`：一次查 title + sort
    - `GetPrepaySourcesAsync`：含 Believer + PrepayCeremonyCategorys 已 join 的全欄位；對齊舊 LINQ filter (PrepayYear=target AND prepay.Sort>=targetSort OR PrepayYear>target)；ORDER BY IsFixedNumber DESC, Number
    - `GetMaxNumberAsync`：起始號
    - `SignupExistsAsync`：per-believer dedup（idempotency）
    - `InsertBatchAsync`：transaction 內逐筆插 Signup + SignupLog
  - **PrepayLoadHandler 演算法（refactored from 780 行）**：
    - 驗證 (year/ceremony/group)
    - 查 target title+sort（一次）
    - 查 source (含 join + filter)
    - 兩階段配號：fixed 先（preserve number, 收集 gaps）→ non-fixed 後（先填 gaps，再從 max+1）
    - PrepayYear **carry-forward** 邏輯逐條對齊舊行為：`prepayYear > target` OR `(== target AND prepay.Sort > target.Sort)`
    - 每筆插入前 idempotency check（per believer × year × ceremony × signupType）
  - **行為改善（vs 舊系統）**：
    - **idempotency 補強**（per-believer dedup）— 對齊 [business-rules-implicit.md](business-rules-implicit.md) 提到的舊系統缺陷
    - **SignupLog 同步寫入** — 載入也產生審計紀錄（舊系統無）
    - **transaction 包整批** — 任一失敗 rollback
  - **Tests +14**：
    - `PrepayGroupsTests` 9 case (count + 6 valid + 3 invalid)
    - `PrepayLoadHandlerTests` 10 case (year 0 / cat empty / invalid group / cat not found / no sources / single non-fixed allocates / single fixed preserves / **fixed gap filled by non-fixed** / dedup skip / **future prepay carries forward**)
    - Integration +6 case (401 / year 0 / cat empty / invalid group / unknown cat / no sources)
  - **Manual smoke (real Ceremony DB)**：
    - Load 114 春季 → 121 春季 case 1 (非員工一般): **30 個信眾載入**（黃耀章 No-1、廖德強 No-6 等）
    - Re-run: **0 loaded, 30 skipped** ✅ idempotency 完美
    - Load 114 春季 → 122 春季 case 1: **21 載入, 20 carry-forward, 1 已結算**（謝佳璋 prepay 落在當前 target 春季 → 不 carry，對齊舊 sort > 比較邏輯）
  - **Bug fix during smoke**：原本 carry-forward 太寬鬆 `==target AND HasValue`，修為 `==target AND prepay.Sort > target.Sort`（對齊 LoadPrepayForm.cs:113）
  - **進度看板**：endpoints 13 → **14 shipped**；coverage forms **2 個 complete 100%** (SignupLogForm + LoadPrepayForm)；tests 113 → **139 (104 unit + 35 integration)**
  - **Secret 驗證**：`grep` 仍 0 命中 ✅

- [x] **3 個新 endpoint shipped (GET /signups/:id + GET /signups/:id/logs + POST /signups) + 2 Domain Services + UPDLOCK 編號分配** — Done 2026-05-27
  - **3 blueprints**：[get-signup-by-id.md](blueprints/api-endpoints/get-signup-by-id.md) / [get-signup-logs.md](blueprints/api-endpoints/get-signup-logs.md) / [post-signups.md](blueprints/api-endpoints/post-signups.md)
  - **2 Domain Services**（新增 `Ceremony.Domain.Services` namespace）：
    - `NumberTitleResolver`：5-case switch (1→No / 2→寺 / 3→觀 / 4→普 / 5→郵) + `SignupTypeName` helper；驗證錯誤 → `VALIDATION_INVALID` 「報名類型錯誤」
    - `AvoidFourFormatter`：個位 4 → "3-1"（例 14 → "13-1", 24 → "23-1"）；display only，DB 仍存 int
  - **`SignupRepository` 擴充**：
    - `GetByIdAsync`：用 SignupView 已 join 的欄位
    - `NumberExistsAsync`：keepNumber 重複檢查
    - `InsertWithLogAsync`：**含 transaction + UPDLOCK + HOLDLOCK**；自動編號路徑 `SELECT MAX(Number)+1 WITH (UPDLOCK, HOLDLOCK)` + Insert Signup + Insert SignupLog 同交易；keepNumber 路徑用 explicit number
    - `GetCeremonyCategoryTitleAsync`：給 SignupLog 快照用
  - **`SignupLogRepository` 新增**：`GetBySignupIdAsync` 排序 Createdate ASC
  - **3 handlers**：`GetSignupHandler` / `ListSignupLogsHandler` / `CreateSignupHandler`
  - **`CreateSignupHandler`** 行為亮點：
    - SignupType → NumberTitle 用 Domain Service
    - **TextAddress fallback**：空時 copy MailAddress（沿用舊 NewSignupForm.cs:246-247）
    - **TextZipcodeId fallback**：textAddress 空時 copy mailZipcodeId
    - **Phone 全→半形**：U+FF01–U+FF5E → ASCII
    - **6 元素名單** normalize（**不 trim 開頭/結尾**，保留排版空格；僅純空白 → null）
    - **Zipcode -1 → null** 正規化
    - **❌ 故意捨棄 inline 新建 Believer**：API 要求前端 orchestration（POST /believers 後 POST /signups）
  - **`SignupsController` 新增 3 actions** + `ExtractCaller` helper 從 JWT claim 取 AdminId / Name
  - **`CreateSignupRequest` + `CallerContext` DTOs**
  - **`ExceptionMiddleware`** 新增 mapping：`SIGNUP_NOT_FOUND` → 404、`SIGNUP_NUMBER_CONFLICT` → 409
  - **Tests +43**：
    - `AvoidFourFormatterTests` 11 case (theory 8 個避4 / 8 個 unchanged / negative)
    - `NumberTitleResolverTests` 13 case (5 valid types / 4 invalid → throws / 5 type names)
    - `CreateSignupHandlerTests` 8 case (invalid type / 空 name / 空 mailAddress / keepNumber 空 / keepNumber 重複 verbatim「{year} {ceremony} {type} 編號重複，請重新確認！」/ 未知 believer / valid path 含 normalize / textZipcode fallback)
    - Integration +6 (signup 404 / logs unknown 200空 / POST 空 name / POST signupType=99 / POST unknown believer / **full lifecycle: create→read→logs→duplicate 409→UPDLOCK +1**)
  - **Manual smoke (curl) 全 pass**：
    - GET /signups/{id} 真實 No-1 黃耀章 春季 admin=地藏殿
    - GET /signups/{id}/logs 真實 1 筆 audit log
    - POST /signups year=999 普桌 → NumberTitle="普" Number=3 (自動分配) phone "0912" (半形)
    - GET 新建的 logs 顯示 admin="Administrator" (backdoor JWT name claim) ceremony="春季" (snapshot)
  - **進度看板**：endpoints 10 → **13 shipped**；coverage forms in-progress 5 → 6 (**SignupLogForm 100% complete — 第一個完整 form**)；tests 70 → **113 (84 unit + 29 integration)**
  - **Secret 驗證**：`grep` 仍 0 命中 ✅

- [x] **2 個新 endpoint shipped (GET /categories + GET /signups) + PredicateBuilder→Dapper 動態 SQL** — Done 2026-05-27
  - **GET /api/v1/categories** ([blueprint](blueprints/api-endpoints/get-categories.md))
    - 取兩層樹狀結構（依 Sort 排）
    - `CategoryRepository.GetAllAsync` 單次 query，handler in-memory 組樹（單層 by-ParentID lookup，不用遞迴）
    - 「法會維護」假根節點**不再回**（前端可自由決定是否顯示）— 故意行為差異
    - **驗證真實 DB**：返回 3 個固定根 GUID（春季 / 中元 / 秋季），每個含 children 陣列
  - **GET /api/v1/signups** ([blueprint](blueprints/api-endpoints/get-signups.md))
    - 11 個 query params 對齊舊系統 11 個 UI 控件
    - 用既有 `dbo.SignupView` 已 join Believer/Category/Admin
    - `SignupRepository.SearchAsync` 用 StringBuilder + Dapper DynamicParameters 動態組 WHERE
    - AND 群組（Year/Ceremony/SignupType/Number）+ OR 群組（scope* + IsFixedNumber）逐條對齊 PredicateBuilder
    - Handler 做 sentinel normalize（`-1` / `0` / `Guid.Empty` / 空白 trim → `null`）
    - `TOP 200` 限制 + LIKE wildcard escape
    - ORDER BY `Year, CeremonySort, NumberTitle, Number`
    - **驗證真實 DB**：year=115 回 200 列；OR group `scopeName + searchKey=陳` 過濾出 4 筆陳姓報名
  - **Tests +12**：
    - `ListCategoriesHandlerTests` 3 case (empty / two-level tree / orphan root)
    - `SearchSignupsHandlerTests` 5 case (normalize Guid.Empty / -1 / 0 / trim / preserve)
    - Integration +2 categories (401 + tree) +3 signups (401 + year filter + sentinel normalize)
  - **覆蓋率影響**：
    - [ceremony-category-form.md](blueprints/legacy-coverage/ceremony-category-form.md) rows 1, 7, 8, 9 → ✅；audit_status `pending → in-progress`；覆蓋率 0% → **36%**
    - [signup-form.md](blueprints/legacy-coverage/signup-form.md) rows 1, 2, 24 → ✅；audit_status `pending → in-progress`；覆蓋率 0% → **7%**（仍待大量 print + edit endpoints）
  - **進度看板**：endpoints 8 → **10 shipped**；coverage forms in-progress 3 → **5** (Login 67%, Admins 43%, Believer 47%, Category 36%, Signup 7%)；tests 56 → **70 (47 unit + 23 integration)**
  - **Secret 驗證**：`grep -rE "<dev-sa-pwd>|<prod-sa-pwd>" backend/`（實際值見 user memory db-credentials）仍 0 命中 ✅

- [x] **Believer CRUD 全部 shipped (4 個新 endpoint：GET/POST/PUT/DELETE) + 56 tests** — Done 2026-05-27
  - **4 blueprints** 全寫好：[get-believer-by-id.md](blueprints/api-endpoints/get-believer-by-id.md) / [post-believers.md](blueprints/api-endpoints/post-believers.md) / [put-believer.md](blueprints/api-endpoints/put-believer.md) / [delete-believer.md](blueprints/api-endpoints/delete-believer.md)
  - **Repository 擴充**：`GetByIdAsync` / `InsertAsync` (Guid.NewGuid + OUTPUT INSERTED.BelieverID) / `UpdateAsync` (UPDATE 22 欄) / `HasSignupsAsync` (Signups join check) / `GetNameAsync` / `DeleteAsync` (**硬刪除**，沿用舊 Believers 表無 IsEnabled)
  - **共用 `BelieverWriteValidator`**：trim + 必填 (Name/MailAddress) + EmployeeType (1/2/3) + length checks + **全→半形 phone (U+FF01–U+FF5E → ASCII)** + zipcode -1→null + 6 元素名單檢查
  - **4 個 handlers** 對齊舊 BelieverForm.cs:57-185, 211-250
  - **`BelieverUpsertRequest` DTO** POST + PUT 共用結構
  - **`BelieversController`** 4 個 action + `[Authorize]`
  - **Tests +22**：
    - `BelieverWriteValidatorTests` 11 case (空值/長度/EmployeeType/phone 全形/-1 zipcode/名單)
    - `DeleteBelieverHandlerTests` 3 case (404 / 409 conflict / 成功)
    - Integration +8: GET/:id 404 / POST 空 name / POST 空 mail / **full CRUD lifecycle** (create→read→update→delete + 驗證 phone 半形 + zipcode null) / PUT 404 / DELETE 404
  - **`ExceptionMiddleware`** 新增 mapping: `BELIEVER_NOT_FOUND` → 404, `BELIEVER_HAS_SIGNUPS` → 409
  - **行為差異記錄**：硬刪除（沿用舊行為，無 IsEnabled）；批次刪除衝突由前端 UX 處理
  - **InternalsVisibleTo** 設定讓 test project 看到 `internal BelieverWriteValidator`
  - **進度看板**：endpoints 4 → **8 shipped**；coverage forms in-progress 3 → 3 (Believer 12% → **47%**)；tests 23+11 → **39+17 = 56**
  - **Secret 驗證**：`grep -rE "<dev-sa-pwd>|<prod-sa-pwd>" backend/`（實際值見 user memory db-credentials）仍 0 命中 ✅

- [x] **2 個新 endpoint shipped (POST /admins + GET /believers) + IntegrationTests 框架 + 共 23 unit / 11 integration tests** — Done 2026-05-27
  - **IntegrationTests 框架建立**：[tests/Ceremony.Api.IntegrationTests](../backend/tests/Ceremony.Api.IntegrationTests/) (WebApplicationFactory + Microsoft.AspNetCore.Mvc.Testing)，`CeremonyApiFactory` 強制 Development env 載入 user-secrets，允許 ENV 覆蓋（CI 友善）
  - **`POST /api/v1/admins` shipped**：
    - blueprint [post-admins.md](blueprints/api-endpoints/post-admins.md) 含 5 邊界 case + 舊 code line ref (88-122, 160-205)
    - 新增 `CreateAdminHandler` (trim + 4 種驗證 + uniqueness check) + `CreateAdminRequest` DTO + `IAdminRepository.UsernameExistsAsync` / `InsertAsync`
    - SQL 用 `INSERT ... OUTPUT INSERTED.AdminID` 回傳新 ID
    - Controller 回 201 + `Location` header
    - **覆蓋率影響**：[admins-form.md](blueprints/legacy-coverage/admins-form.md) rows 3,6,9,10 ✅，覆蓋率 14% → 43%
  - **`GET /api/v1/believers` shipped**：
    - blueprint [get-believers.md](blueprints/api-endpoints/get-believers.md) 含 6 邊界 case + 5 個 query params + SQL 動態 WHERE
    - 新增 `IBelieverRepository.SearchAsync` + `BelieverRepository`（Dapper + DynamicParameters + LEFT JOIN Zipcodes 兩次）+ `BelieverSearchQuery` / `BelieverListItem` (含 6 元素 LivingNames/DeadNames array)
    - **SQL injection safety**：LIKE wildcard `%` `_` `[` 在應用層 escape
    - 「請輸入搜尋條件」verbatim 訊息對齊舊系統
    - **覆蓋率影響**：[believer-form.md](blueprints/legacy-coverage/believer-form.md) rows 2,13 ✅，覆蓋率 0% → 12%
  - **ExceptionMiddleware 改良**：用 `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`，中文錯誤訊息以 UTF-8 直接輸出（不 escape 成 `\uXXXX`），前端/curl/test 都受益
  - **測試成果**（dotnet test 全 pass）：
    - 23 unit tests (LoginHandler 11 + CreateAdminHandler 7 + SearchBelieversHandler 5)
    - 11 integration tests (health 1 + auth/login 3 + admins GET 2 / POST 2 + believers 3)
    - 整合測試共用 (local) Ceremony DB；POST 測試用時間戳 username 避免衝突
  - **Manual smoke (curl)**：name=陳 真實查回多筆信眾，含 6 欄 LivingNames 陽上名單與 Zipcodes JOIN
  - **進度看板**：endpoints 2 → **4 shipped**；coverage forms 2 → 3 in-progress (Login 67%, Admins 43%, Believer 12%)
  - **Secret 驗證**：`grep -rE "<dev-sa-pwd>|<prod-sa-pwd>" backend/`（實際值見 user memory db-credentials）仍 0 命中 ✅

- [x] **第 2 個 endpoint `GET /api/v1/admins` shipped + xUnit 測試專案 + LoginHandler 11 個測試** — Done 2026-05-27
  - **xUnit 測試專案**：[tests/Ceremony.Application.Tests](../backend/tests/Ceremony.Application.Tests/) (xunit 2.9.3 / FluentAssertions 8.10 / Moq 4.20)
  - **LoginHandlerTests 11 case 全 pass** (86ms)：empty username / empty password / unknown user / disabled user / backdoor / backdoor-disabled / wrong password / correct credentials / lockout after threshold / counter reset on success / case-sensitive password
  - **第 2 endpoint forward 規則 A**：[get-admins.md](blueprints/api-endpoints/get-admins.md) blueprint shipped；驗證項含「不外洩 Password」「需 JWT auth」
  - **Read-model pattern**：新增 `Ceremony.Application.Admins.AdminListItem` DTO（不含 Password）；`IAdminRepository.GetAllEnabledAsync` 回傳 `IReadOnlyList<AdminListItem>`；SQL 明確 select 4 個欄位不含 Password
  - **第 2 endpoint reverse 規則 A'**：[admins-form.md](blueprints/legacy-coverage/admins-form.md) row 1, 12 勾 ✅（覆蓋率 14% — 2/14 個方法；CRUD 其餘 12 個待後續）
  - **AdminsController** + `[Authorize]` + ListAdminsHandler
  - **手動驗證**（3 case 全 pass）：
    - `GET /api/v1/admins` 無 token → 401 ✅
    - `GET /api/v1/admins` 帶 backdoor JWT → 200 + 6 個真實 admin 從 (local) Ceremony DB 取出（judy / 地藏殿 / M / hui / itadmin / yun）
    - Response 不含 `password` 字串 ✅
  - **進度看板**：endpoints shipped 1 → 2；legacy-coverage forms in-progress 1 → 2 (Login 67% / Admins 14%)

- [x] **ASP.NET Core API 骨架 + 首個 endpoint `POST /api/v1/auth/login` shipped** — Done 2026-05-27
  - **Solution**：[backend/](../backend/) 4 個 project (.NET 10 LTS / C# 14) Clean Architecture
    - `Ceremony.Domain` (Admin entity, DomainException)
    - `Ceremony.Application` (LoginHandler + JwtTokenService + LoginFailureTracker + AuthOptions/JwtOptions)
    - `Ceremony.Infrastructure` (SqlConnectionFactory, AdminRepository — Dapper)
    - `Ceremony.Api` (AuthController, HealthController, ExceptionMiddleware, JWT bearer, Serilog, CORS for 4200)
  - **套件**：Dapper 2.1.79 / Microsoft.Data.SqlClient / Serilog.AspNetCore / JwtBearer / IMemoryCache
  - **首個 endpoint forward 規則 A**：[post-auth-login.md](blueprints/api-endpoints/post-auth-login.md) blueprint 完整，含舊 code line ref / 驗證對照 / 邊界 case
  - **首個 endpoint reverse 規則 A'**：[login-form.md](blueprints/legacy-coverage/login-form.md) row 2-3 已勾 ✅，覆蓋率 67% (3 個方法中 2 個實作)
  - **Secret 規則 D**：connection string + JWT signing key 透過 `dotnet user-secrets`；appsettings.json 只放 `__OVERRIDE_VIA_USER_SECRETS_OR_ENV__` 占位
  - **手動驗證**（4 case，皆 pass）：
    - `GET /health` → 200 `{"status":"healthy","db":"up"}`（Dapper SELECT 1 from `(local)` MSSQL）
    - `POST /api/v1/auth/login` backdoor `sa@system.local/Admin@123` → 200 + JWT (HS256, exp 30min)
    - `POST /api/v1/auth/login` empty username → 400 `VALIDATION_REQUIRED` + 「請輸入帳號！」verbatim
    - `POST /api/v1/auth/login` wrong password → 401 `AUTH_INVALID_CREDENTIALS` + 「帳號或密碼錯誤！」verbatim（含 DB 查詢 → null → 鎖定計數 +1）
  - **更新 backend-design.md**：.NET 8 → .NET 10、C# 12 → C# 14、新增「已落地骨架」章節
  - **下一步**：tests project 加入後寫 LoginHandler 5 case 整合測試；接續第 2 個 endpoint（建議 `GET /api/v1/admins`）

- [x] **完成 10 份 legacy-coverage 初始稽核 baseline** — Done 2026-05-27
  - 用 3 個 Explore agent 並行枚舉 10 個舊 Form (共 5964 行 code)
  - 共枚舉 **160 個方法/事件/邏輯區塊**：SignupLogForm 2 / LoginForm 3 / MainForm 8 / CeremonyCategoryForm 11 / AdminsForm 14 / BelieverForm 17 / EditSignupForm 20 / LoadPrepayForm 8 (核心是 780 行的 `btnConfirm_Click`) / NewSignupForm 34 / SignupForm 43 (含 RDLC 變體選擇 30/31/32)
  - 每行含：舊方法/事件 + line ref + 行為摘要 + 新系統狀態 (`⏳ 缺口待補`) + 候選對應 endpoint + 備註
  - 標註出 **2 個候選 ❌ 故意捨棄**（`AdminsForm.ProcessCmdKey` Enter→Tab、`NewSignupForm.btnNextStep_Click` 兩步驟）
  - 標註出 **5 個高風險業務邏輯區塊**：LoginForm 後門帳號 / LoadPrepayForm 無 idempotency / NewSignupForm UPDLOCK / SignupForm RDLC 變體 / CategoryForm 雙重刪除
  - 同步更新 [api-endpoints/README.md](blueprints/api-endpoints/README.md)：把所有 23 個 endpoint 的舊 Form line ref 從 `?` 填成具體行號 + Legacy Coverage 連結到具體 row
  - 同步更新 [legacy-coverage/README.md](blueprints/legacy-coverage/README.md) 索引：方法數欄、`last_audited: 2026-05-27`、候選決策摘要
  - Output: 10 份 [legacy-coverage/<form>.md](blueprints/legacy-coverage/) 完整 baseline，可隨 endpoint 實作逐條 ✅/❌

- [x] **後端 API 開發前置規範（5 條規則含反向覆蓋稽核 + 既有密碼洩漏清理）** — Done 2026-05-27
  - **新增 5 條規則**：
    - A (forward)：每個 endpoint 必查舊 Form code，寫入 [docs/blueprints/api-endpoints/](blueprints/api-endpoints/) `<verb>-<resource>.md`
    - A' (reverse)：每個舊 Form 維護 [docs/blueprints/legacy-coverage/](blueprints/legacy-coverage/) `<form>.md` 覆蓋表；上線前 100% 解釋每一行
    - B：dev=`(local)` / prod=`192.168.1.151`，走 `appsettings.{Env}.json` 三層覆蓋
    - C：列印 PoC 先評估 RDLC 重用，不行才 QuestPDF（文件雙路徑並存）
    - D：Secret 永不入 repo；dev 走 `dotnet user-secrets`、prod 走 ENV vars
  - **新增 2 個目錄**：
    - `docs/blueprints/api-endpoints/` (含 `_template.md` + `README.md`，README 預列 21+ 個 endpoint 對照表)
    - `docs/blueprints/legacy-coverage/` (含 `_template.md` + `README.md` + 10 個 Form stub 檔，狀態先全 `pending`)
  - **既有密碼洩漏清理**：
    - `.scratch/explore/01-auth-main.md` 1 處 → 改為 `__REDACTED__`
    - `reference/old/Ceremony/App.config` 2 處、`Ceremony.Models/App.config` 1 處、`DataTrans/App.config` 3 處 → 全改 `__REDACTED__` + 檔首加 redacted comment
    - 驗證：`grep -rE "Password=[^_<]"` 0 命中（任何明文密碼皆不應出現）
  - **新增 repo root `.gitignore`**：cover `.scratch/` + `appsettings.Production.json` + `**/secrets.json` + `**/.env*` + `bin/` `obj/` `node_modules/`
  - **真實 DB 帳密存放**：`~/.claude/projects/-Users-tim-agents-ceremony/memory/db-credentials.md`（auto-memory，repo 外）+ 更新 `MEMORY.md` 索引
  - **更新 9 份文件**：CLAUDE.md（規則 10/11 + 同步表 3 列 + 索引）/ conventions.md（API 實作約定段）/ feature-development.md（§1.5 + §6.5 + DoD 3 條）/ qa-testing.md（Legacy coverage 稽核段）/ api-design.md（實作規範段）/ backend-design.md（業務邏輯表加 coverage 連結 + 檢核清單）/ infrastructure.md（三層覆蓋範本 + Secret 管理規則 + 環境表 DB 行）/ database-design.md（dev/prod 表）/ printing-reports.md（決策歷程）/ printing-reports-positions.md（適用範圍 callout 雙路徑都遵守）
  - 輸出：完整 plan 紀錄在 `~/.claude/plans/api-1-endpoint-mellow-sprout.md`

- [x] **Angular SPA 骨架完成（frontend/）** — Done 2026-05-27
  - 工具：Angular CLI 21.1.4、Angular 21.1、TypeScript 5.9、SCSS、Vitest（取代 Karma）
  - 安裝：`@angular/material` ^21.2.12（custom theme rose/orange palette + density -2）、`@ngrx/signals` ^21.1.0
  - **預設 zoneless**：Angular 21 預設無 Zone.js，`app.config.ts` 顯式加 `provideZonelessChangeDetection()`；router 用 `withHashLocation()`（為 Electron 預備）
  - **Claude 配色 theme**：`src/styles.scss` 完整搬入 mockup 的 18 個 `--c-*` token + 6 個字級 + 5 個間距 + 控件 / sidebar / modal 尺寸 + `--font-ui` / `--font-print`，與 [visual-design.md §色彩](design/visual-design.md) 一致
  - **資料夾結構**（對齊 [frontend-design.md](design/frontend-design.md) 桌面結構，但前期略過 `electron/`）：
    - `core/auth/`：`AuthStore`（signalStore，含 `isLoggedIn` / `adminId` / `displayName` computed + `login` / `logout` methods）、`authGuard` (CanActivateFn)、`auth.models.ts`
    - `core/layout/shell-layout/`：MainForm 對應的左側 sidebar（6 個 nav 項目：信眾維護 / 新增報名 / 報名查詢 / 載入預繳 / 法會類型 / 管理者）+ logout 按鈕 + content router-outlet
    - `features/login/`：LoginPage（Reactive Form + signal-based submitting / errorMessage，目前直連 AuthStore mock）
    - `features/dashboard/`：DashboardPage（4 個快捷入口 tile，inline template + styles）
    - `shared/placeholder-page/`：通用 stub，從 route `data` 取 `breadcrumb` / `form` 名稱顯示「尚未實作」
  - **路由**：11 條 lazy-loaded route 全部通；`/login` 為獨立頁、其餘走 `ShellLayout` + `authGuard`；含 wildcard `'**' → ''`。每個 route 含 `title` (browser tab) + `data.breadcrumb` / `data.form`（mapping 至舊 WinForms Form 名）
  - **驗證**：`ng serve --port 4200 --host 127.0.0.1` 在背景啟動，bundle 0.948s 生成（lazy chunks: shell-layout 10.88kB / login-page 10.71kB / dashboard-page 7.16kB / placeholder-page 6.49kB）；curl HTTP 200 + styles.css 含 5 處 `--c-primary`/`#CC785C` token
  - **取捨**：
    - 暫不建 11 個 feature 個別 component，全用一個 `PlaceholderPage` + route data 驅動，避免「先建好十個空殼」的死碼；實作期一條條換 loadComponent
    - `provideHttpClient(withInterceptors([]))` 預留 interceptor 陣列（auth / error 之後再填）
    - 暫不導入 ESLint：Angular 21 預設無 lint，等定下 frontend-coding-style 細節再 `ng add @angular-eslint/schematics`
  - 輸出：[frontend/](../frontend/) 完整 Angular workspace；可在 http://127.0.0.1:4200/ 跑

- [x] **RDLC 規格文件強化（嚴格 1:1 規範 + audit 修正錯誤 + 變體選擇從推測改為 code ground truth）** — Done 2026-05-27
  - 起因：客戶強調列印實作必須與舊系統一模一樣；audit [printing-reports-positions.md](blueprints/printing-reports-positions.md) 對照 19 個 RDLC 原始 XML 發現多項錯誤
  - **重大錯誤修正**：
    - **紙張尺寸 3 處錯標**：v1 doc 把 `tmpTabletOne / tmpTablet_One / tmpTablet_Two` 標為「36.5×26.2cm 橫向超寬」，**實際全部都是 11.5×25.4cm 窄長**（與其他 6 個牌位變體相同）。所有 9 個牌位變體皆 11.5×25.4cm
    - **PhotoAddress 錯置**：v1 doc 寫 `tmpTabletOne 含 PhotoAddress`，**實際 PhotoAddress 只在 tmpText / tmpTextTwo**
    - **tmpWorship 漏記 3 個 Textbox**：v1 doc 只列 4 個 LivingName 位置；實際 6 個（Textbox11/12/13 對應 LivingNameSix/Four/Three），構成 2 列 × 3 欄矩陣
    - **變體選擇邏輯**：v1 doc 是「推測，需確認」表格；**已從 [SignupForm.cs:1148-1228](../reference/old/Ceremony/SignupForm.cs#L1148-L1228) / :1335-1357 / :1554-1593 完整擷取 code ground truth**，含 `if/else if` 順序、`name?.Trim() != ""` 判定、`DeadNameOne.Length > 7` 動態 ParaFontSize 邏輯
  - **新增章節**：
    - **「⚠️ 嚴格執行條款」**（檔首 CRITICAL section）：7 條零容忍規則（座標 ±0.05cm 容差、不得改字型/字色、變體選擇不得優化合併、必跑 checklist）；含 v1.x 因「字級 0.8→0.75cm 看起來更精緻」導致 200 張牌位錯位重印的歷史 incident
    - **「紙張尺寸總覽（必查表）」**：19 個 RDLC 的 page size 一覽，避免實作時誤判
    - **「對位驗收 Checklist」**：A 靜態檢查 / B 動態量測（產 PDF + mutool/pdftotext 抽座標 + Python diff）/ C 實體列印對位（紋飾紙疊放透光）/ D 57 組 fixture matrix（45 牌位 + 6 文牒 + 6 普桌）。每個列印 PR 必跑並貼結果
    - **「已知陷阱與邊界 case」**：10 條實作陷阱（`null vs Trim()==""`、Textbox 名 ≠ Field 名、ParaFontSize 對 3+ 亡者場景無動態、tmpReceipt Tablix 跨頁、HallName「．」全形占位等）
  - **強化 QuestPDF 還原指引**：補上「禁止用 Row/Column 自然流」「字型不可 fallback 微軟正黑體」「PhotoAddress 保留舊 Library.DrawText 邏輯」「背景圖不可四捨五入」「禁用 CanGrow」「Fee 不加千分位」等具體禁則
  - **frontmatter 強化**：`status: CRITICAL — single source of truth；任何偏差需 PR review 通過`；`related_agents` 加 qa-test-engineer
  - 文件規模 467 行 → 664 行（+42%）
  - 輸出：[docs/blueprints/printing-reports-positions.md](blueprints/printing-reports-positions.md) 為新版 QuestPDF 列印實作的唯一規格來源

- [x] **UI Mockup v4（樣式統一、信眾/管理者改 Modal、新增報名單表、5 份列印 1:1 對齊 RDLC）** — Done 2026-05-27
  - **CSS 變數化**：新增 `--sidebar-width / --believer-edit-width / --grid-max-height / --modal-width-{sm,md,lg} / --print-color / --print-bg`；既有 `--button-large-height` 從 99px 改為 64px
  - **Utility classes** 取代常見 inline 樣式：`.req .cb-end .w-full .mt-sm/.mt-md .panel-flush .panel-header .panel-footer .field-w-sm .search-form .modal-md .modal-lg .stat-num .dialog-title .text-center .prepay-arrow`
  - **信眾維護頁改造**：
    - 搜尋條件改用 `.search-form`（`grid-template-columns: repeat(auto-fit, minmax(160px, 1fr))`）整齊化，搜尋按鈕改 `.btn-sm`（高度從 99px → control-height 28px）
    - 右側「信眾資料」panel 移除，改為 `#believer-edit-modal`（`.modal.modal-lg` = 720px）；雙擊列或「＋ 新增」開啟
  - **新增報名合併單一表單**：移除 `.wizard` 兩步驟拆分，所有欄位放同一 `<div class="panel">`：上方 `.search-form` 含法會/年份/類型/信眾搜尋/預繳；下方為基本資料 → 寄件/文牒地址（雙欄）→ 往生/陽上（雙欄）→ 備註 → 按鈕列
  - **管理者維護頁改造**：右側 panel 移除，改為 `#admin-edit-modal`（標準 380px modal）；雙擊列或「＋ 新增」開啟
  - **5 份列印預覽 1:1 對齊 RDLC**（核心改動）：
    - 新增 `.rdlc-paper` 系統：以 **cm 為 CSS 單位**直接給 width/height（CSS 原生支援 `cm`）
    - 5 種紙張尺寸：`.size-datacard` (21×14.8cm) / `.size-receipt` (21×59.4cm Tablix 全長) / `.size-tablet` (11.5×25.4cm) / `.size-text` (36.5×26.2cm) / `.size-worship` (21×29.6cm + worship2.png 背景)
    - 每個 `.rdlc-field` 用 `position: absolute` + `top/left/width/height (cm)` 對應 RDLC TextBox 座標（取自 [printing-reports-positions.md](blueprints/printing-reports-positions.md)）
    - 字級 class：`.rdlc-fs-{6,7,8,10,20,30,14pt,16pt}` 對應 RDLC FontSize；`.rdlc-bold .rdlc-center .rdlc-right .rdlc-vertical` 對應對齊
    - 牌位/普桌/文牒的窄長直書欄位用 `writing-mode: vertical-rl`（對應 RDLC 直書文字）
    - DataCard 兩條 Line 用 `.rdlc-line-dashed / .rdlc-line-solid` class
    - 普桌背景圖直接引用 `../reference/extracted-images/worship2.png` 並用 cm 定位（top 0.261cm, left 0.42cm, 20.047 × 28.884cm）
    - Zoom 控制改用 CSS `zoom` 屬性（會改變 layout box，避免 `transform: scale` 留白問題）；預設 50%
  - **取捨記錄**：
    - mockup 中 5 個列印模板只挑「基本版」實作（datacard/receipt/tablet/text/worship），舊系統 19 個變體（tabletOne~TwoTwo / worshipOne~Five）的模板選擇邏輯放實作期處理
    - tmpReceipt Tablix 跨頁 59.4cm 在 mockup 用 single paper 完整呈現（實際 PDF 會分頁）
    - tmpText 的 PhotoAddress（25×605px 垂直地址 PNG）在 mockup 用虛框 placeholder 模擬，實作期由 QuestPDF Canvas 重畫
  - inline styles 由 178 → 152，其中 RDLC 必要的 cm 絕對座標佔 63 個（每欄唯一，無法 class 化）
  - 輸出：[mockup/index.html](../mockup/index.html) 12 sections + 3 modals + 5 份 cm 對齊的列印預覽

- [x] **UI Mockup v3（補齊舊系統 12 個 Form 對應頁 + 3 處位置修正）** — Done 2026-05-27
  - 起因：盤點發現 mockup 只有 8 頁，舊系統 12 個 Form 中有 4 個沒對應頁、3 個位置不符
  - 補頁（皆為獨立 `data-page` section，sidebar 子流程新增入口）：
    - `edit-signup` → EditSignupForm（五列式：編號/年份/法會/類型/費用 → 信眾/堂號/員工/姓名/電話 → 寄件/文牒地址雙欄 → 往生/陽上雙欄 → 預繳+備註）
    - `signup-log` → SignupLogForm（全頁單一 DataGridView，對齊舊版 800×450 唯讀快照清單）
    - `print-dialog` → CustomDialogForm（列印格式 PDF / 預覽列印 兩按鈕並排，346×171 dialog 樣式）
    - `message-dialog` → CustomMessageForm（通用訊息 + 單一關閉按鈕；新版功能流程多改 snackbar，本頁保留作對位參考）
  - 位置修正：
    - **MainForm 按鈕順序**改回舊版 Y=12/63/114/165/216/267：信眾→新增(藍底)→報名→載入→備份→管理者（原 mockup 把新增搬到第一格、備份搬到最後）
    - **AdminsForm** 加「＋ 新增」按鈕於 grid 右上（對齊舊版 btnInsert (334,48) 位於 grid 與編輯 panel 之間）
    - **LoadPrepay modal** 加紅色「至」分隔 + 來源/目標 fieldset 分組（對齊舊版 label4 紅字置中 Y=106）
  - 取捨記錄：
    - **NewSignupForm 兩步驟**：用戶要求改回橫向，但實測 mockup `.wizard` CSS 已是 `grid-template-columns: 200px 1fr`（橫向），不需動
    - **LoadPrepay 仍為 modal**（不改回獨立 Form）：舊版是 dialog (337×259) 本就 modal-like，符合 Web 慣例
    - **CustomDialog/Message 作為獨立頁**：方便 design review 比對舊版位置；實際運行時仍可用 modal/snackbar 實作
  - 輸出：[mockup/index.html](../mockup/index.html) 共 12 個 `data-page` section（+1 個 prepay-modal）對應舊系統 12 個 Form
  - 一致性原則沿用 [visual-design.md §驗收標準](design/visual-design.md)：欄位位置誤差 ≤ 8px、按鈕文字 verbatim

- [x] **UI Mockup v2（Claude 配色 + 列印預覽頁）** — Done 2026-05-26
  - 套用 Claude 暖米/珊瑚橘配色（取代原 WinForms 灰藍）：[visual-design.md](design/visual-design.md) 色彩段全面改寫
  - 加入列印預覽頁（[mockup/index.html](../mockup/index.html) `#print-preview`）：含 5 種報表（資料卡/收據/薦牌/文牒/普桌）切換、zoom toolbar、A4/A5/牌位紙模擬
  - 決策：**僅打包 Windows**、**桌面 icon 沿用舊系統**、**先 Angular 後 Electron** → 寫入 [infrastructure.md](design/infrastructure.md) + [frontend-design.md](design/frontend-design.md)
  - 新增 [reference/icons/README.md](../reference/icons/README.md) 待客戶上傳 .ico

- [x] **UI Mockup v1（單頁多 tab 純 HTML/CSS）** — Done 2026-05-26
  - 7 視圖 + 載入預繳 modal：[mockup/index.html](../mockup/index.html)
  - 按鈕文字 verbatim；避4 顯示「3-1」；右鍵 context menu 示意

- [x] **第 7 輪 — 建立待確認清單 + 進度看板** — Done 2026-05-26
  - 新增 [docs/pending-business-input.md](pending-business-input.md)：27 項待業務/DBA/客戶確認項目，分 A/B/C/D 四類含確認進度看板
  - 更新 [status.md](status.md) Backlog：P0 兩項（DB 數據回填 + 業務需求會議）、P1 兩項（實作骨架 + 列印 PoC）
  - 本檔加「目前文件化進度」區塊，會話開始即可看到全貌與下一步
- [x] **第 6 輪營運層補完** — Done 2026-05-26
  - [workflows/qa-testing.md](workflows/qa-testing.md) 大幅擴充：4 層測試金字塔（靜態 / 單元 / 整合 / E2E）+ 列印對位驗收 + 效能測試 + 安全測試 + 跨平台 + UX 回歸 + 業務驗收 + 測試資料策略
  - [infrastructure.md](design/infrastructure.md) 補完：Electron 簽章（Win/Mac/Linux）、auto-update（electron-updater）、Sentry self-hosted、Log 結構標準、災難復原演練（季度/年度）、Rollback 流程（schema 凍結→零資料風險）、環境設定速查表
  - [security.md](design/security.md) 補完：個資法（台灣 PDPA）合規、資料保留與銷毀政策、每月加密與授權審計 review
  - [performance.md](design/performance.md) 補完：Cache 失效策略（IMemoryCache invalidate 範例）、多 client 失效未來方案
  - 新增 [workflows/user-training.md](workflows/user-training.md)：上線前訓練計畫（4 個 module、教材、上線後支援、訓練回饋）
- [x] **第 5 輪缺口補完（業務語意 + 變體邏輯 + 隱含規則）** — Done 2026-05-26
  - 新增 [docs/glossary.md](glossary.md)：法會 / 信眾 / 陽上 / 往生 / 文牒 / 薦牌 / 普桌 / 觀音會 / 寺方 / 郵撥 / NumberTitle / 民國年 / 避4 / 預繳 / 載入預繳 / 變更紀錄 等業務術語完整解釋（含宗教文化背景與 13 條常見誤解）
  - 新增 [docs/business-rules-implicit.md](business-rules-implicit.md)：16 條舊系統 code 內隱含但未文件化的業務規則（編號生成、避4 邊界、Believer/Signup 兩級欄位、年份限制、法會雙重刪除限制、信眾刪除整批中止、LoadPrepay 無 idempotency 等）
  - 補完 [docs/blueprints/printing-reports.md](blueprints/printing-reports.md)：19 個 RDLC 變體選擇邏輯（薦牌 3×3 矩陣 / 文牒 2 變體 / 普桌依最高 LivingName 位置 / 字級 ParaFontSize 規則）— **完整反推自 SignupForm.cs**
  - 更正 [docs/blueprints/printing-reports-positions.md](blueprints/printing-reports-positions.md)：6 個 worship RDLC 全用同一張 `worship2`（不是 worship2/3/4/5 分別對應）
  - 補完 [docs/blueprints/prepay-loading.md](blueprints/prepay-loading.md)：舊系統無 idempotency 的具體行為；新版必修
  - 補完 [docs/design/database-design.md](design/database-design.md)：上線前要對 DB 跑的 6 段 SQL（取 view DDL、資料量、既有索引、跨年法會、密碼長度、SQL Server 版本、備份策略）
  - 提取 RDLC 內嵌圖檔到 [reference/extracted-images/](../reference/extracted-images/)：worship2.png（普桌背景）+ 其他死資源備查
  - 探索報告 [scratch/07](../.scratch/explore/07-rdlc-variants-avoid4-loadprepay-rules.md) 完整保存
- [x] **重構決策 Iteration 4（最終）** — Done 2026-05-26
  - 客戶最終裁定：(1) DB 結構**完全不動** (2) 密碼**用明碼**（不加密）(3) **無 migration**
  - JWT 仍保留（應用層 token，不動 DB）；Dapper 仍保留（無需 migration）；Signal-first 仍保留
  - 全面回退：[database-design.md](design/database-design.md)（完全凍結，無索引）、[backend-design.md](design/backend-design.md)（移除 DbUp / Ceremony.Migrations 專案）、[security.md](design/security.md)（明文比對）、[auth-and-admin.md](blueprints/auth-and-admin.md)、[performance.md](design/performance.md)（移除所有 index migration，純應用層手段：分頁/cache/debounce/UPDLOCK/限縮搜尋組合）、[data-migration.md](blueprints/data-migration.md)（重新標記 deprecated）、[infrastructure.md](design/infrastructure.md)（DB 區塊改「完全不動」）、[signup-management.md](blueprints/signup-management.md)（效能要點純應用層）
- [x] **重構決策 Iteration 3** — Done 2026-05-26
  - 新決策：(1) RDLC 完整規範入文件（位置/形狀/字體/字大小）(2) DB 結構大致不變，密碼改 Argon2id 加密 + JWT (3) ORM 用 **Dapper + DbUp migration** (4) 效能優先（索引、虛擬滾動、UPDLOCK 編號） (5) Angular 全 **Signal-first**（signalStore）
  - 新增 [docs/design/performance.md](design/performance.md)：效能基準、索引、查詢策略、DataGrid 虛擬化、Signal 優化
  - 強化 [docs/blueprints/printing-reports-positions.md](blueprints/printing-reports-positions.md)：補上每個 RDLC 的字體（標楷體）、字級（0.6cm-3cm）、Bold/Italic/Align/Padding/Border/Line/Rectangle、EmbeddedImage（worship2-5）、ReportParameter (ParaFontSize)、QuestPDF 還原指引
  - 大幅改寫：[database-design.md](design/database-design.md) (Dapper + DbUp migration scripts 全寫好)、[backend-design.md](design/backend-design.md) (Dapper repo pattern)、[security.md](design/security.md) (Argon2id + JWT 漸進升級)、[auth-and-admin.md](blueprints/auth-and-admin.md) (hash 比對 + 後門保留)、[frontend-design.md](design/frontend-design.md) (signalStore + 反模式)、[signup-management.md](blueprints/signup-management.md) (效能要點)
  - 新增 [data-migration blueprint](blueprints/data-migration.md) 改寫為 DbUp migration 工具（非 ETL）
- [x] **DB schema 凍結決策 + RDLC 位置完整萃取** — Done 2026-05-26
  - 決策：新系統直接連既有 `Ceremony` DB，schema 完全凍結（含明文密碼欄位）；連 sa 改為應用專用帳號、Pooling=False 改 enabled 等屬於設定變更而非 schema 變更
  - 影響的 docs：database-design.md（重寫為 schema 凍結版）、backend-design.md（Database First / Scaffold，無 Migration）、security.md（明文密碼列為已知接受風險、後門保留）、auth-and-admin.md（明文比對、無 RBAC）、signup-management.md（無 action 欄位）、data-migration.md（標記 deprecated，僅留切換流程）、infrastructure.md
  - 新增 [printing-reports-positions.md](blueprints/printing-reports-positions.md)：19 個 RDLC 模板逐欄位精確 cm 座標（Top/Left/Width/Height/FontSize），為 QuestPDF 還原版面的 single source of truth
  - 列印重要性：牌位 / 普桌 / 文牒印在預印格式紙上，欄位偏差 ±0.2cm 內否則套印錯位
- [x] **舊系統完整文件化 → 新系統重構藍圖** — Done 2026-05-26
  - Outcome: 完成 7 份 design doc 更新（database/backend/frontend/api/visual/security/infrastructure）+ 7 個 blueprint（auth-and-admin / believer / category / signup / prepay / printing / data-migration）+ 6 份 scratch explore 報告（[.scratch/explore/](../.scratch/explore/)）；目標技術棧 C# ASP.NET Core + Electron + Angular/Vue + MSSQL；UI 編排對齊原 WinForms（誤差 ≤ 8px、列印 ≤ 0.2cm）
- [x] **對齊 OpenAI Harness Engineering 四項關鍵能力** — Done 2026-05-07
  - Outcome: 新增 [evals.md](evals.md)、[research-plan-execute-verify.md](workflows/research-plan-execute-verify.md)、[permissions-sandbox.md](design/permissions-sandbox.md)、[tools-and-skills.md](design/tools-and-skills.md)；CLAUDE.md 加 RPEV 規則與 4 條同步規則
- [x] **Coding Style 詳盡指南** — Done 2026-05-07
  - Outcome: [frontend-coding-style.md](design/frontend-coding-style.md) + [backend-coding-style.md](design/backend-coding-style.md) + conventions.md 工具基礎設施段
- [x] **Status 追蹤規則** — Done 2026-05-07
  - Outcome: 本檔（status.md）+ CLAUDE.md 狀態追蹤規則
- [x] **Memory 與專案隔離 + 討論結果自動記錄規則** — Done 2026-05-07
  - Outcome: CLAUDE.md 兩個新區塊
- [x] **Harness 文件骨架建立** — Done 2026-05-07
  - Outcome: 完整 [docs/](.) 結構（design / blueprints / workflows）+ CLAUDE.md TOC

## 🗄 Archive

> 早期完成項目摘要；詳細紀錄保留在對應 blueprint

- 已上線功能：見 [blueprints/README.md](blueprints/README.md) 中 status=shipped 的項目
