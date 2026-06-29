---
title: 認證與管理員維護
purpose: 登入、登出、Token 管理、管理員 CRUD；對應舊 LoginForm、MainForm、AdminsForm
status: draft
applicable_when: 要修改認證流程、新增管理員角色、調整 MainForm 主導覽
related_agents:
  - software-architect-blueprint
  - backend-engineer
  - frontend-architect
related_docs:
  - ../design/security.md
  - ../design/api-design.md
  - ../design/visual-design.md
keywords: [login, auth, admin, 管理員, JWT, MainForm, AdminsForm, LoginForm]
last_updated: 2026-06-29 (DB 解除凍結：密碼雜湊/RBAC/鎖定表改列為待評估 migration；認證實作維持現況)
---

## 背景與動機

舊系統用明文密碼、硬編碼後門 `weypro/weypro12ab`、無 session 過期、無權限分級。（新版已移除 weypro，改用系統 SuperAdmin `sa@system.local`）

**現況**（DB 已於 2026-06-29 解除凍結、可走 DbUp migration；下列認證實作目前維持現狀，雜湊化/RBAC/持久化鎖定列為待評估）：

| 議題 | 現況處理 | 解凍後可行（待評估） |
|---|---|---|
| 密碼儲存 | **明文**（`Admins.Password` nvarchar(20)） | migration 擴欄 + 雜湊化 |
| 密碼比對 | 常數時間明文比對 `FixedTimeEquals` | 改雜湊驗證 |
| 認證方式 | **JWT bearer**（應用層 token） | （與 DB 無關，維持）|
| 系統 SuperAdmin | `sa@system.local/Admin@123`（非 DB；取代舊 weypro 後門，可關閉）| （維持）|
| 失敗鎖定 | IMemoryCache（重啟清空） | migration 加 `login_attempts` 表持久化 |
| RBAC | 無（現況不加 role 欄位） | migration 加 `Admins.Role` 欄位 |
| Audit log | Serilog 結構化檔案 log（不加 DB 表） |
| 介面編排 | 保留 LoginForm / MainForm / AdminsForm 編排 |

## 範圍

### 做什麼
- 登入頁（取代 LoginForm）
- 主視窗 ShellLayout 與導覽（取代 MainForm 的 6 個功能按鈕）
- 管理員列表與編輯（取代 AdminsForm）
- 系統備份觸發（保留 MainForm「資料備份」按鈕）
- 登出與密碼變更

### 不做什麼
- SSO / OAuth（暫不需要）
- 多租戶（單一寺院）
- 密碼提示問答（用 admin 重設代替）

## 使用者流程

### 登入

```
1. 啟動 Electron app
2. 路由 redirect → /login
3. 輸入帳號/密碼 → POST /api/v1/auth/login
4. 後端流程：
   - 若 username/password == "sa@system.local"/"Admin@123" → 給 AdminID=0 token（系統 SuperAdmin，取代舊 weypro）
   - 否則查 Admins WHERE Username = @u AND IsEnabled = 1
   - 常數時間明文比對 FixedTimeEquals(input, admin.Password)
5. 成功：簽發 JWT access token (30 min) + refresh token (7 days)
   失敗：訊息 verbatim「帳號或密碼錯誤！」
6. 連續 5 次失敗 → 423 Locked 15 分鐘（in-memory 鎖定）
7. **無強制變更密碼**（沿用舊系統行為）
```

### 主畫面（MainForm 對應）

ShellLayout 提供 6 個功能入口（順序對齊舊 MainForm）：
1. 信眾維護 → `/believers`
2. 新增報名（**主動作 highlight**）→ `/signups/new`
3. 報名維護 → `/signups`
4. 載入預繳 → `/prepay`
5. 資料備份 → `/backup` 頁觸發 `POST /api/v1/backup`；成功 dialog 顯示備份檔路徑 / 大小
6. 管理者維護 → `/admins`

頂部右側顯示登入者 username + 版本號 + 登出按鈕。

### 管理員 CRUD（AdminsForm 對應）

兩欄式：左 DataGrid（列出 admins）+ 右編輯區（4 欄位 + 4 按鈕）。

操作流程：
- 點列 → 載入該 admin 至右側、username 變唯讀
- 點「新增」→ 清空、username 可編輯
- 「確認」→ 驗證 → POST/PUT
- 右鍵列 →「刪除」→ 軟刪（is_enabled=false），訊息「確認刪除 {username} 嗎？」

## 設計決策

### 關鍵選擇

- **JWT + Refresh token** 取代 Global.Islogin 全域變數
- **明文密碼比對**（現況；雜湊化待評估，見上表）
  - 用 `CryptographicOperations.FixedTimeEquals` 防時序攻擊
  - 緩解：TLS only、最小權限 DB 帳號、connection string 入 secret store
- **系統 SuperAdmin `sa@system.local/Admin@123`（取代舊 weypro 後門）**
  - 業務未要求移除；只在 code 內檢查，不寫入 DB
- **失敗鎖定走 IMemoryCache**
  - 重啟清空，不擴 schema
- **「新增報名」按鈕保留 light blue 強調**
- **AdminsForm 的 Enter→Tab 行為改為標準 Enter=submit**
  - 可由設定開關開回舊行為

### 取捨

- 取了：JWT 認證、應用層完整補強、零 DB 變更（部署簡單）
- 捨了：密碼加密、強密碼策略、密碼過期、密碼歷史、RBAC — 沿用舊行為

## 跨層影響

| 層級 | 是否影響 | 變動摘要 |
|---|---|---|
| 視覺 | 是 | LoginForm / MainForm / AdminsForm 對應頁面；訊息文字 verbatim |
| 前端 | 是 | login / shell / admins feature；AuthGuard interceptor |
| 後端 | 是 | Auth + Admins endpoints（明文比對）+ JWT issuer |
| API | 是 | `/auth/*`、`/admins/*`、`/backup` |
| 資料庫 | 現況否 | 目前連 Admins 既有 nvarchar(20) Password 欄位；DB 已可變更，雜湊化/RBAC/鎖定表為待評估的 migration |
| 基礎建設 | 是 | JWT 私鑰存放；備份目錄設定；應用專用 DB 帳號（runtime 無 DDL，migration 用獨立帳號） |
| 安全 | 部分修正 | 詳見 [security.md](../design/security.md) — 明文密碼為現況、可走 migration 改善（待評估） |

## 驗收標準

- [ ] 登入頁全部訊息 verbatim（含「帳號或密碼錯誤！」）
- [ ] 系統 SuperAdmin `sa@system.local/Admin@123` 可登入（AdminID=0）
- [ ] `Admins.Password` 欄位仍為 nvarchar(20)（**DB 未動**）
- [ ] 一般 admin 用 DB 內明文密碼可登入；錯密碼回 401
- [ ] 連續 5 次失敗後鎖定 15 分鐘（in-memory）
- [ ] JWT access token TTL 30 分鐘；refresh token TTL 7 天；rotation on use
- [ ] AdminsForm 對應頁兩欄式佈局與舊版誤差 ≤ 8px
- [ ] 軟刪 admin（`IsEnabled = false`）後該帳號無法登入
- [ ] 主畫面 6 按鈕順序與舊 MainForm 一致
- [x] 備份按鈕成功後彈出檔案路徑（**2026-05-29 完成**：`backup-page` 成功 dialog 顯示 fileName / fullPath / size）
- [ ] 強制 HTTPS；連線字串於 secret store
- [ ] Audit 行為寫至 Serilog 結構化檔案 log
- [ ] 通過 [code-review](../workflows/code-review.md) 與 [security-review](../workflows/security-review.md)

## 風險與未解問題

- 明文密碼是已知接受風險，**業務需簽核確認**
- In-memory 失敗鎖定在多實例部署下失效；目前單實例可接受
- Emergency access：DBA 透過 SSMS 直接 UPDATE Admins.Password
- Electron 重新啟動時 token 持久化策略：keytar（OS keychain） vs encrypted localStorage？建議用 keytar

## 參考資料

- [scratch/01-auth-main.md](../../.scratch/explore/01-auth-main.md)：舊 LoginForm/MainForm/AdminsForm 完整控件清單與訊息
- [scratch/06-data-layer-migration.md](../../.scratch/explore/06-data-layer-migration.md)：BaseService.Dispose 遞迴 bug、Pooling=False
- 舊原始碼：[reference/old/Ceremony/LoginForm.cs](../../reference/old/Ceremony/LoginForm.cs)、[MainForm.cs](../../reference/old/Ceremony/MainForm.cs)、[AdminsForm.cs](../../reference/old/Ceremony/AdminsForm.cs)
