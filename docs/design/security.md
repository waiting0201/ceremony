---
title: Security Design
purpose: 法會報名系統重構版的安全規範：密碼、認證、授權、加密、PII 保護
applicable_when: 要新增認證/授權邏輯、要處理 PII、要 review 安全議題、要修補舊系統漏洞
related_agents:
  - backend-engineer
related_docs:
  - backend-design.md
  - database-design.md
  - api-design.md
  - infrastructure.md
keywords: [security, 安全, 密碼, 加密, JWT, OWASP, PII]
last_updated: 2026-06-18 (前端 session 改記憶體 only → 強制每次登入；移除 weypro 後門 → 系統 SuperAdmin sa@system.local；出廠連線種子)
---

## 安全策略總覽

> **前提**：DB **完全凍結**（客戶需求，見 [database-design.md](database-design.md)），含 `Admins.Password` 明文 nvarchar(20)、無 audit_logs / login_attempts 表、無任何索引或欄位變更。所有安全議題分兩類：
> - **應用層可修**：直接實作（佔多數）
> - **需動 DB 才能修**：列為**已知接受風險**，客戶接受

### 已知接受風險（DB 凍結，無法在新系統解）

| # | 議題 | 嚴重度 | 應用層緩解 |
|---|---|---|---|
| 1 | `Admins.Password` 明文儲存 | Critical | TLS only、secret store、DB 帳號最小權限、log 不寫密碼、常數時間比對 |
| 2 | 系統 SuperAdmin `sa@system.local`（非 DB，取代舊 weypro 後門）| High | code 內檢查、不寫入 DB；可由 `Auth:SuperAdminEnabled` 關閉 |
| 3 | 缺少 RBAC（無 role 欄位） | High | 沿用舊行為（全 admin 同權限） |
| 4 | 缺登入失敗鎖定 schema | Medium | IMemoryCache 紀錄（重啟清空，弱化版本） |
| 5 | 缺 audit log 表 | Medium | Serilog 結構化檔案 log |

### 應用層必修（**不動 DB**）

| # | 議題 | 嚴重度 | 應用層處理 |
|---|---|---|---|
| 6 | DB 用 `sa` 帳號連線 | Critical | 建立應用專用 SQL 帳號（最小權限只能 SELECT/INSERT/UPDATE 本 DB 表 + EXEC backup proc）— DB 帳號變更非 schema 變更 |
| 7 | 連線字串明文於 `App.config` | High | dotnet user-secrets（dev）/ DPAPI（prod）/ Key Vault |
| 8 | 無 session 過期 | Medium | **JWT** TTL 30 分鐘 + refresh token TTL 7 天（in-memory store） |
| 9 | 備份路徑硬編碼 `D:\Backup\` | Low | 設定檔可配置 |
| 10 | 明文密碼在傳輸層 | High | 強制 TLS（後端 HTTPS only） |

## 認證（Authentication）

### 密碼（明文 + JWT，DB 完全凍結）

- **儲存**：`Admins.Password` 明文 nvarchar(20)（**客戶接受**）
- **比對**：常數時間比對 `CryptographicOperations.FixedTimeEquals`（防時序攻擊）
- **欄位變更**：**無**（DB 凍結）
- **複雜度**：無 schema 支援，**僅前端建議性檢查**（無強制）
- **過期**：無
- **歷史**：無
- **重設**：admin 透過 admins API 直接更新 `Password` 欄位

### 登入實作

```csharp
public async Task<LoginResult> LoginAsync(string username, string password)
{
    // 系統 SuperAdmin 帳號（不寫入 DB；取代舊 weypro 後門）
    if (username == "sa@system.local" && password == "Admin@123")
        return Success(IssueJwt(adminId: 0, "sa@system.local"));

    var admin = await repo.GetByUsernameAsync(username);
    if (admin == null || !admin.IsEnabled) return Failure("AUTH_INVALID_CREDENTIALS");

    // 常數時間明文比對
    if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(admin.Password),
            Encoding.UTF8.GetBytes(password)))
        return Failure("AUTH_INVALID_CREDENTIALS");

    return Success(IssueJwt(admin.AdminID, admin.Username));
}
```

### JWT（應用層 token，不動 DB）

- **Access token**：JWT, RS256 簽章，TTL 30 分鐘
- **Refresh token**：opaque GUID，**in-memory store**（重啟清空；不寫 DB），TTL 7 天
- **Claims**：`sub` (admin_id), `name`, `iat`, `exp`, `jti`（無 role，沿用舊行為）
- **撤銷**：登出、密碼變更時 revoke
- **前端 session 存放（2026-06-18）**：`AuthStore` token **僅存記憶體、不寫 localStorage/sessionStorage**。理由：Electron 每次啟動 / DB 連線成功後 main 會帶 `apiBase` 重新載入 renderer（`loadAppWithApi`），記憶體狀態隨之清空 → `authGuard` 看不到 token → 強制回 `/login`。確保「必須登入才能進首頁」，並避免殘留舊 token（可能來自不同 DB / 已失效）造成跳過登入直接進首頁。代價：每次開 App 都要重新登入（temple kiosk 場景可接受）。

> **降低明文密碼風險的應用層措施**：
> 1. **TLS only**（傳輸層加密）— 明文密碼 wire 不外洩
> 2. **DB 帳號最小權限** — `sa` 改為應用專用帳號
> 3. **連線字串於 secret store** — DB 連線本身不外洩
> 4. **登入頁全程 HTTPS** — 阻止 MITM
> 5. **Log 不寫密碼**（即便 input 也 mask）
> 6. **常數時間比對** — 防時序攻擊

### Token

- **Access token**：JWT, RS256 簽章，TTL 30 分鐘
- **Refresh token**：opaque GUID, store in DB hashed, TTL 7 天，rotation on use
- **Claims**：`sub` (admin_id), `name`, `roles`, `iat`, `exp`, `jti`
- **撤銷**：登出、密碼變更時 revoke 所有 refresh token

### Login 流程

1. 收 `{username, password}`
2. 若 username/password == `sa@system.local`/`Admin@123`（系統 SuperAdmin，取代舊 weypro）→ 給 AdminID=0 token
3. 否則查 `Admins WHERE Username = @u AND IsEnabled = 1`
4. 常數時間明文比對 `FixedTimeEquals(input.Password, admin.Password)`
5. 失敗計數 +1（IMemoryCache），達 5 次鎖 15 分鐘
6. 成功：reset counter、issue JWT access token + refresh token、寫 Serilog file log

> Emergency access：DBA 透過 SSMS 直接 UPDATE `Admins.Password`（明文）。

## 授權（Authorization）

沿用舊系統：**無角色分級，全 admin 同權限**。schema 不加 role 欄位。

- 預設所有 `/api/v1/*` 要 `[Authorize]`（JWT 驗證 only，無 role 細分）
- 全認證使用者皆可：CRUD 信眾、CRUD 報名、列印、備份、管理其他 admin
- 業務級限制（如「不可刪除有報名的信眾」「不可刪除有子分類的法會」）由 service 層 enforce
- **備份「清交易紀錄檔」（`clearLog=true`，2026-05-29）屬半破壞性 DBA 級操作**：會 `BACKUP LOG`（FULL）截斷並 `DBCC SHRINKFILE` 收縮交易紀錄檔。無 role 分級下任何 admin 可執行，故前端以 **opt-in checkbox（預設關）+ 勾選時 confirm 警語** 降低誤觸；後端只在完整備份成功後才清、且不破壞還原鏈（不使用 `TO DISK='NUL'`）
- **備份下載 `GET /backup/{fileName}/download`（2026-06-02，Electron 另存用）**：`[Authorize]`，串流 `Backup:Directory` 下的 `.bak`/`.trn`。**路徑穿越（path traversal）防護**：`SqlBackupService.IsValidBackupFileName` 僅放行 `^[0-9A-Za-z._-]+\.(bak|trn)$` 且拒絕 `..` / 任何路徑分隔符 / 磁碟代號 / UNC 前綴 → 不合法回 400 `VALIDATION_BACKUP_FILENAME`，避免讀到目錄外檔案。檔案以 `FileShare.Read` 唯讀開啟。Electron 端以 JWT Bearer 經 `net.request` 取檔（非把 token 寫進 URL）。

> 未來若有需要加 RBAC：新增 DbUp migration 加 `admins.role` 欄位 + 用 `[Authorize(Roles = "Admin")]`

## 加密

| 範疇 | 方式 |
|---|---|
| 連線 | TLS 1.2+；自簽憑證在內網部署，Electron auto-trust |
| DB at rest | SQL Server TDE（Transparent Data Encryption） |
| 連線字串 | dev: dotnet user-secrets；**prod (sidecar): 純文字 JSON 存 `%APPDATA%/Ceremony/config.json`**（方案 C，見下） |
| 備份檔 | 啟用 `BACKUP DATABASE ... WITH ENCRYPTION` 或 OS 層加密 volume |
| Token 簽章 | HS256（短期）/ RS256（升級時改）。**prod sidecar：每台 client 隨機 key**（`config.ts` 首次寫檔以 `crypto.randomBytes(32)` 產生 `jwtKey` 存 `config.json`，啟動時 `Jwt__SigningKey` ENV 注入），避免用 `appsettings.json` 內可預測的 placeholder 當簽章 key |

### Sidecar 模式 DB 認證決策（**2026-05-28**）

採方案 **C：純文字 JSON config**，**不加 DPAPI / Windows Authentication**。

**理由**：寺方 IT 資源有限，優先簡化部署與排錯流程；接受降低本機 secret 防護等級。

**風險與緩解**：

| 風險 | 緩解措施 |
|---|---|
| 本機 admin 權限者可讀到 DB 密碼 | DB 帳號**最小權限**：只能 DML + EXEC `backup proc`，無 DDL（schema 凍結；不開放 `sysadmin` / `db_owner`） |
| 密碼以純文字存 user profile | LAN-only 部署，無公網暴露；SQL Server 開 Windows Firewall 限制來源網段 |
| 多 client = 多份 config.json | 各 client 都是 user-profile 路徑（隨使用者 Windows 帳號）；**2026-06-02 起改出廠預寫**（見下），不再逐台手填 |
| 升級安全等級 | 架構保留方案 A（Windows Authentication）/ 方案 B（DPAPI 加密）的升級路徑，現有 Electron `config.ts` 模組可換實作不動其他層 |

**出廠預寫連線（2026-06-02，取代「首次啟動手填」）**：寺方為**同機部署**（程式裝在 DB 主機上），連線固定，改為把連線烘進安裝檔。打包機放 `frontend/build/default-config.json`（`dbHost=192.168.1.151` + sa 密碼）→ electron-builder `extraResources` 打進 `resources/default-config.json`；`main.ts` **每次啟動**以種子連線覆寫 `config.json`（種子為連線權威，jwtKey 仍每機隨機保留）。**安全等級與方案 C 相同**（密碼仍明文存 client `%APPDATA%`，且現在也明文存於安裝檔內），只是輸入時機從「使用者首次填」改為「出廠預寫」。

- **Secret 不入 repo（CLAUDE.md 規則 11）**：`default-config.json` 已 gitignore，只 commit `default-config.example.json`（密碼 `<from-secrets>` 占位）；真實值僅打包機本地持有。
- **新風險**：安裝檔（.exe / resources）內含明文 sa 密碼，任何拿到安裝檔者可解出 → 安裝檔須限內部交付，勿外流；緩解仍靠「DB 帳號最小權限 + LAN-only + 防火牆網段限制」。升級路徑：未來可改 DPAPI 加密種子或安裝時才注入。

**評估三方案對照**：

| 方案 | 優點 | 缺點 | 採用 |
|---|---|---|---|
| A. Windows Authentication（Integrated Security） | 不需在 client 存任何密碼 | 寺方要設 AD 或本機帳號授權，IT 門檻高 | ❌ |
| B. DPAPI 加密 config | 即使檔案被複製到他機也不能解 | 換機要重填；首次啟動引導體驗 +1 步 | ❌（保留升級路徑） |
| **C. 純文字 JSON config** | 部署最簡單 | 本機讀檔即拿到 | ✅ |

## PII 保護

| 資料 | 分類 | 處理 |
|---|---|---|
| Name | PII | 一般保護，log 時 mask 為「王*明」 |
| Phone | PII | log 時 mask 為「09****5678」 |
| Mail/Text Address | PII | log 時 mask 為「台北市***」 |
| LivingName / DeadName | PII（含逝者） | 同上 |
| Password | Secret | 永不寫 log；DB 為明文（凍結） |

實作：
- Serilog enricher 對特定欄位 hash / mask
- DTO 用 `[Sensitive]` attribute 標記，序列化時轉 placeholder（log 用）
- API 回應不做 mask（前端需要明文顯示）
- Sentry `beforeSend` hook 過濾敏感欄位再上傳

### 個資法（台灣 PDPA）合規

| 議題 | 處理 |
|---|---|
| 法源 | 《個人資料保護法》及施行細則 |
| 蒐集告知 | 信眾報名時應有「個資使用告知書」(目前舊系統無 — 業務評估) |
| 蒐集目的 | 法會報名管理、文牒/牌位列印、收據開立 |
| 利用範圍 | 內部使用，不對外揭露；不行銷 |
| 當事人權利 | 查詢 / 更正 / 刪除（透過 admin） |
| 安全措施 | TLS、最小權限 DB 帳號、log mask、定期備份 |
| 國際傳輸 | **無**（純本地部署） |
| 資料外洩通報 | 24 小時內通報業務 + 評估是否符合通報主管機關門檻 |

### 資料保留 / 銷毀

| 資料 | 保留期 | 銷毀方式 |
|---|---|---|
| Signups（含 PII） | 永久（業務需求 — 法會歷史紀錄）| 信眾申請刪除 + admin 批准後軟刪 + 30 日後實體刪 |
| SignupLogs | 5 年 | 滿期歸檔至離線冷儲存 |
| Believers | 永久 | 同上信眾申請刪除流程 |
| Audit log（Serilog files） | 90 天 | 自動輪替 + 加密歸檔 |
| Sentry 事件 | 30 天 | Sentry 自動清理 |
| 備份 | 30 天 hot + 90 天 cold | 過期銷毀 |

> ⚠️ **業務確認項**：目前舊系統「信眾申請刪除」流程未文件化。新系統應加：
> - admin 後台「信眾刪除申請」工作流
> - 信眾若有 Signups 不能直接刪 → 改為「停用 + 標記刪除申請」
> - 30 日後 admin 確認後 hard delete

### 加密與授權審計

每月 review：
- DB 連線 audit log（誰連 DB、執行什麼）
- Admins 表變更紀錄（誰新增 / 啟停帳號）
- 異常登入時段（深夜、非辦公時段）

## OWASP Top 10 對照

| OWASP | 對應防護 |
|---|---|
| A01 Broken Access Control | RBAC + endpoint-level [Authorize] |
| A02 Cryptographic Failures | Argon2id + TLS + TDE |
| A03 Injection | EF Core 參數化查詢、輸入驗證 |
| A04 Insecure Design | Clean Architecture + 業務不變式集中 Domain |
| A05 Security Misconfiguration | 不出 dev 設定到 prod；Production checklist |
| A06 Vulnerable Components | Dependabot / NuGet Audit |
| A07 Identification & Auth Failures | 鎖定、強密碼、token rotation |
| A08 Software & Data Integrity | DLL 簽章（Authenticode）、binary supply chain |
| A09 Logging Failures | 結構化 log + audit log + 中央化 |
| A10 SSRF | 後端不對外送 request（除 OS 內 DB），無此風險 |

## 審計日誌（File-based，schema 凍結）

**無新 DB 表**，改用 Serilog 結構化檔案 log：

```jsonc
{
  "Timestamp": "2026-05-26T14:23:12Z",
  "Level": "Information",
  "EventType": "Audit",
  "AdminId": 5,
  "Operation": "signup.create",
  "TargetType": "signup",
  "TargetId": "...",
  "Ip": "192.168.1.100",
  "UserAgent": "Mozilla/...",
  "Payload": { "name": "王*明", "phone": "09****5678" },  // mask 過
  "Result": "success"
}
```

寫入時機：所有 write API + `/backup` + `/auth/login`。

保留：≥ 1 年；檔案輪替按月，gzip 歸檔。

> 既有 `SignupLogs` 表續用於報名變更歷程（非通用 audit）。

## 部署 / 環境分離

- **dev**：本機 user-secrets，Seq UI 顯示 log
- **staging**：類 prod 設定，acceptance test
- **prod**：DPAPI / Key Vault；無 Swagger；HTTPS only

## 應急響應

- DB 異常：DBA 介入，restore 最近備份
- 帳號外洩：DBA 直接 disable / 重設 hash
- API 大量錯誤：reverse proxy 暫停服務 + maintenance page

## 驗收

- [ ] `Admins.Password` 欄位仍為 nvarchar(20)（**DB 未動**）
- [ ] 連線字串脫離明文（無 `sa.*twvsjp` 字串）
- [ ] 無 `Pooling=False` 設定
- [ ] 系統 SuperAdmin `sa@system.local/Admin@123` 可登入（AdminID=0）
- [ ] 一般 admin 用 DB 內明文密碼可登入；錯密碼回 401
- [ ] 所有 `[Authorize]` endpoint 在無 token / 過期 token 下回 401
- [ ] JWT access token TTL 30 分鐘；refresh token TTL 7 天
- [ ] 5 次失敗登入後第 6 次回 423 Locked（in-memory 鎖定）
- [ ] App 啟動 / DB 連線成功重載 renderer 後 **未登入則導向 `/login`，不可直接進首頁**（token 不持久化）
- [ ] PII 在 log 中為 mask 形式
- [ ] 強制 HTTPS（HTTP 自動轉 HTTPS）
- [ ] DB 連線用應用專用帳號（非 sa）
- [ ] 已知接受風險清單與業務同步確認
