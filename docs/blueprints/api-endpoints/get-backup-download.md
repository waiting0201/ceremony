---
title: GET /api/v1/backup/{fileName}/download
purpose: 串流下載備份檔（.bak/.trn）供 client 端「另存新檔」（Electron sidecar 模式）
status: shipped
endpoint: get-backup-download
http_method: GET
route: /api/v1/backup/{fileName}/download
legacy_form: N/A (新需求)
legacy_lines: N/A
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../../design/api-design.md
  - ../../design/security.md
  - ../../design/infrastructure.md
  - ../electron-packaging.md
keywords: [backup, download, 備份下載, 另存, traversal, sidecar]
last_updated: 2026-06-02
---

## 規格

### Route + Method

`GET` `/api/v1/backup/{fileName}/download`（`[Authorize]`）

### Request

- Path param `fileName`：要下載的備份檔名（如 `20260602143521123456.bak` 或 `*.trn`）。
- Header `Authorization: Bearer <jwt>`。

### Response

- `200`：`application/octet-stream`，body 為檔案串流（`FileStreamResult`，`enableRangeProcessing=true`，可斷點續傳；`CanSeek` 時帶 `Content-Length`）。下載檔名為原 `fileName`。

### 錯誤碼

| HTTP | errorCode | message | 觸發條件 |
|---|---|---|---|
| 400 | `VALIDATION_BACKUP_FILENAME` | 備份檔名格式不正確 | 檔名未通過 `IsValidBackupFileName`（含 `..` / 路徑分隔符 / 非 `.bak`/`.trn` / 非 ASCII 等） |
| 404 | `BACKUP_FILE_NOT_FOUND` | 找不到備份檔（請確認備份目錄可由應用程式讀取；sidecar 模式建議使用 UNC 共用路徑） | `Backup:Directory` 下無此檔，或 API process 讀不到該目錄 |
| 500 | `BACKUP_NOT_CONFIGURED` | Backup:Directory 未設定 | config 無 `Backup:Directory` |
| 401 | — | — | 無 / 過期 token |

詳見 [api-design.md 業務錯誤碼表](../../design/api-design.md)。

## 舊系統對照（規則 A — forward）

| 項目 | 對照 | 說明 |
|---|---|---|
| 對應 Form / 事件 | `N/A (新需求)` | 舊 WinForms 系統的備份（[MainForm.cs:95-113](../../../reference/old/Ceremony/MainForm.cs#L95-L113)）直接由 SQL Server 寫到本機 `D:\Backup\`，桌面程式與 DB 同機，**無「下載另存」概念**。新版 sidecar 為 client/DB 分離 + 瀏覽器無法選伺服器路徑，故新增此 endpoint。 |

### 業務邏輯區塊

1. **檔名驗證**（新需求）：`SqlBackupService.IsValidBackupFileName` — `^[0-9A-Za-z._-]+\.(bak|trn)$` 且不含 `..`。拒絕路徑穿越，避免讀到 `Backup:Directory` 以外檔案。
2. **路徑組裝**：`JoinForSqlServer(Backup:Directory, fileName)`（沿用備份寫檔的同一 helper，依目錄分隔符判斷 Windows/Unix）。
3. **讀檔串流**：`File.Exists` 為 false → `BACKUP_FILE_NOT_FOUND`；否則 `FileStream(..., FileShare.Read)` 回 `FileStreamResult`，由 controller 在回應結束後自動 dispose。

### 邊界 case

| 場景 | 新版行為 | 對應測試 |
|---|---|---|
| `../secret.bak` / `sub\dir.bak` / `C:\...` / UNC | 400 `VALIDATION_BACKUP_FILENAME` | `IsValidBackupFileName_RejectsTraversalAndForeignExtensions` |
| `file.exe` / 無副檔名 / 非 ASCII | 400 同上 | 同上 |
| `*.bak` / `*.trn` 合法時戳名 | 通過驗證 | `IsValidBackupFileName_AcceptsBakAndTrn` |
| API 與 DB 不同機、目錄非共用 | 404（讀不到） | （限制，文件記載；非自動測試） |

## 資料存取

不查 DB（純檔案串流）。檔案來源 = `Backup:Directory`（由 `POST /backup` 產出的 `.bak`/`.trn`）。

## 驗收標準

- [x] 檔名 traversal 防護 pure helper + 單元測試（合法 4 + 非法 14）
- [x] 不合法 400、找不到 404、未設定 500、未授權 401
- [x] `application/octet-stream` 串流回應
- [ ] Windows 實機：Electron 原生另存下載 .bak 成功（待驗）
- [ ] 通過 [code-review](../../workflows/code-review.md) / [qa-testing](../../workflows/qa-testing.md)

## 風險與未解問題

- `Backup:Directory` 對 API process 的可讀性是前置條件（prod 走 UNC；dev docker 容器路徑讀不到 → 404）。
- 無 rate limit 細分（沿用全域）；下載大檔以串流處理，記憶體可控。

## 參考

- 實作：[BackupController.cs](../../../backend/src/Ceremony.Api/Controllers/BackupController.cs)、[SqlBackupService.cs](../../../backend/src/Ceremony.Infrastructure/Backup/SqlBackupService.cs)、[BackupHandler.cs](../../../backend/src/Ceremony.Application/Backup/BackupHandler.cs)
- Electron 端：[frontend/electron/download.ts](../../../frontend/electron/download.ts)
- 上層藍圖：[electron-packaging.md](../electron-packaging.md)
