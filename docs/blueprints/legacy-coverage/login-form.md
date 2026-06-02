---
title: LoginForm Legacy Coverage
purpose: 反向稽核 — LoginForm 所有方法/事件的新系統對應狀態
applicable_when: 完成 auth/login endpoint 後勾選；月度稽核；上線前 gate
legacy_form: LoginForm.cs
legacy_path: reference/old/Ceremony/LoginForm.cs
legacy_lines: 83
audit_status: complete
coverage_percentage: 100
last_audited: 2026-06-02
baseline_completed: 2026-05-27
total_methods: 3
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../api-endpoints/README.md
  - README.md
keywords: [legacy, coverage, login]
last_updated: 2026-06-02
---

> ✅ **完成 (2026-06-02)**：3 個方法全部已實作。登入流程 + 版本標籤全 ship。

## 稽核總覽

| 項目 | 值 |
|---|---|
| 總方法 / 事件 / 邏輯區塊數 | 3 |
| ✅ 已實作 | 3 |
| ❌ 故意捨棄 | 0 |
| ⏳ 缺口待補 | 0 |
| 🤔 待確認 | 0 |
| 覆蓋率 | 100% |

## 方法 / 事件 / 邏輯區塊清單

| # | 舊方法/事件 | 舊 code line | 行為摘要 | 新系統狀態 | 對應 endpoint | 備註 |
|---|---|---|---|---|---|---|
| 1 | `LoginForm()` constructor | 21-29 | 初始化 db、service；設定版本標籤 | ✅ 已實作 | 前端 `LoginPage` + footer | `login-page.ts` `appVersion = environment.version`；`login-page.html` 渲染 `<div class="version">{{ appVersion }}</div>` |
| 2 | `btnConfirm_Click` event handler | 31-55 | 驗證帳密；空值檢查；關閉視窗或顯示錯誤 | ✅ 已實作 | `POST /api/v1/auth/login` | `LoginHandler.HandleAsync` 含空值檢查 + verbatim 錯誤訊息「請輸入帳號！」/「請輸入密碼！」/「帳號或密碼錯誤！」 |
| 3 | `ValidateUser()` private method | 58-81 | 檢查硬編碼帳密或查詢 DB；設定 Global 登入狀態 | ✅ 已實作 | `POST /api/v1/auth/login` | 後門帳號走 `Auth:BackdoorEnabled` config；DB 查詢用 `AdminRepository.GetByUsernameAsync` + `CryptographicOperations.FixedTimeEquals` 常數時間比對；補強：失敗鎖定 in-memory 5 次/15 分 |

## 驗證紀錄

- 2026-05-27：手動 curl 測試通過 4 case（backdoor login 200 / 空 username 400 / 錯密碼 401 / DB SELECT 1 健康）
- TODO：xUnit 整合測試（含 5 種 case：empty / unknown user / disabled user / backdoor / lockout）
