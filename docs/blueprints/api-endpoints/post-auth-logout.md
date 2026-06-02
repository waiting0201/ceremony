---
title: POST /api/v1/auth/logout
purpose: 撤銷當前 JWT token；後續同 token 請求被拒（401）
status: shipped
endpoint: post-auth-logout
http_method: POST
route: /api/v1/auth/logout
legacy_form: N/A
legacy_lines: N/A
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../../design/api-design.md
  - ../../design/security.md
  - ../legacy-coverage/login-form.md
keywords: [auth, logout, jwt, blacklist, jti]
last_updated: 2026-05-27
---

## 規格

### Route + Method

`POST /api/v1/auth/logout`

### Auth

需有效 JWT bearer token；無 token 或 token 已被撤銷 → 401。

### Request DTO

空 body（撤銷對象由 Authorization header 的 token 自決定）。

### Response DTO

```jsonc
// 200 OK
{ "ok": true }
```

### 錯誤碼

| HTTP | errorCode | message | 觸發條件 |
|---|---|---|---|
| 401 | – | – | 無 token / token 已被撤銷 / token 簽章錯誤 |

## 舊系統對照（規則 A — forward）

### 對應 Form / 事件

舊系統 WinForms 無「登出」endpoint — 關閉 MainForm 即等於結束 session（無 server state）。本 endpoint 為**新需求**。

### 為什麼新版需要

- **Web SPA 多裝置**：使用者可能在公用電腦登入後忘記關瀏覽器 → server-side 撤銷能立刻失效
- **JWT 無狀態**：缺少「撤銷」機制就只能等 token 自然過期（30 分鐘）；遭竊持續可用 30 分鐘
- **稽核需求**：登出事件可記入 log；舊系統 close form 不留紀錄

## 業務邏輯

1. **Token 解析**：從 `HttpContext.User.Claims` 讀取
   - `jti` (JWT ID) — 撤銷的 key
   - `exp` (expiration epoch sec) — 黑名單 TTL = exp - now
2. **黑名單寫入**：`IJwtBlacklist.Revoke(jti, ttl)`；底層用 `IMemoryCache`，TTL = remaining lifetime（防止永久膨脹）
3. **後續驗證**：`JwtBearer.OnTokenValidated` event 攔截每筆請求，若 `jti` 在黑名單 → `context.Fail(...)` → 401
4. **回應**：永遠 200 `{ "ok": true }`（即使 token 已過期 / 已撤銷，登出操作無副作用）

## 設計取捨

| 取捨 | 決策 | 理由 |
|---|---|---|
| 黑名單儲存位置 | **`IMemoryCache`** | 單機部署、與既有 `LoginFailureTracker` 同模式；多 instance 後續換 Redis 或 DB |
| TTL 策略 | **=token 剩餘壽命** | 過期後 token 本身會被 `ValidateLifetime` 擋掉，黑名單條目可釋放 |
| 撤銷粒度 | **per-jti（單一 token）** | 不影響該使用者其他裝置的有效 session；如要「全部登出」另設 endpoint |
| Logout idempotency | **多次呼叫皆 200** | 第二次 logout 時 token 已被撤銷 → 進不了 controller；無 token 也是同效果 |
| 撤銷後 GET 行為 | **401 + JWT bearer challenge** | 不額外加自訂錯誤碼，沿用 `JwtBearer` 預設行為 |

## 資料存取

無 DB；純 in-memory（IMemoryCache）。

## 驗收標準

- [x] 規格段所有欄位有 DTO 型別
- [x] Logout 後同 token 的後續請求回 401
- [x] 多次 logout idempotent
- [x] 黑名單 TTL = token 剩餘壽命（不會永久膨脹）
- [x] 含整合測試（login → call protected → logout → call protected 應 401）
- [x] login-form.md row 1 不適用；本 endpoint 為新需求，coverage 不影響舊 Form

## 風險與未解問題

- **多 instance 部署**：IMemoryCache 不跨 process；黑名單在單一 instance 內有效。未來改 Redis 解決（見 [infrastructure.md](../../design/infrastructure.md) Secret 管理段 + 後續部署 doc）
- **process 重啟**：黑名單清空 → 已撤銷但未到期的 token 暫時恢復可用；window 最大為 token 剩餘壽命（≤30 分）；可接受風險
- **「全部裝置登出」**：目前不支援；如需要可加 `POST /auth/logout-all` 黑名單整個 `sub`（adminId）

## 參考

- 既有 JWT 設定：[backend/src/Ceremony.Api/Program.cs](../../../backend/src/Ceremony.Api/Program.cs) `AddJwtBearer`
- JWT 發行：[backend/src/Ceremony.Application/Auth/JwtTokenService.cs](../../../backend/src/Ceremony.Application/Auth/JwtTokenService.cs)（已含 jti claim）
- IMemoryCache 範本：[LoginFailureTracker.cs](../../../backend/src/Ceremony.Application/Auth/LoginFailureTracker.cs)
