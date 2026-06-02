---
title: <FormName> Legacy Coverage
purpose: 反向稽核 — 把 <FormName> 所有方法/事件/業務邏輯列出，逐條標記在新系統的對應狀態
applicable_when: 完成一個 endpoint 後回頭勾選、每月稽核、上線前最終 gate
legacy_form: <FormName>.cs
legacy_path: reference/old/Ceremony/<FormName>.cs
legacy_lines: 0
audit_status: pending  # pending / in-progress / complete
coverage_percentage: 0  # 計算：(已實作 + 故意捨棄) / 總方法數 × 100
last_audited: YYYY-MM-DD
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../api-endpoints/README.md
  - ../../workflows/qa-testing.md
keywords: [legacy, coverage, audit, <form-keyword>]
last_updated: YYYY-MM-DD
---

## 稽核總覽

| 項目 | 值 |
|---|---|
| 總方法 / 事件 / 邏輯區塊數 | 0 |
| ✅ 已實作 | 0 |
| ❌ 故意捨棄 | 0 |
| ⏳ 缺口待補 | 0 |
| 🤔 待確認 | 0 |
| 覆蓋率 | 0% |

## 方法 / 事件 / 邏輯區塊清單

> 規則：上線前每一行必須是 `✅` 或 `❌`（含明確理由）；`⏳` / `🤔` 都是上線 blocker。

| # | 舊方法/事件 | 舊 code line | 行為摘要 | 新系統狀態 | 對應 endpoint | 捨棄理由 / 缺口備註 |
|---|---|---|---|---|---|---|
| 1 | `<Form>.ConstructorMethod` | `<Form>.cs:?-?` | <一句話> | ⏳ 缺口待補 | – | – |

## 私有 helper / utility 函式

> 通常不需 1:1 對應 endpoint，但行為要在新系統某處重現

| # | 函式 | line | 用途 | 新系統落地處 |
|---|---|---|---|---|

## UI 行為（顯示 / 顯隱 / 啟用）

> 多數是前端範疇；列出讓 frontend agent 對照

| # | 行為 | line | 觸發 | 新系統落地處 |
|---|---|---|---|---|

## 已知與舊系統行為差異（刻意改動）

> 列出新系統**故意**不沿用舊行為的決策；每條附理由

- 例：舊 Enter 鍵當 Tab；新版改為標準 Enter=submit（理由：符合 Web 慣例，見 [frontend-design.md](../../design/frontend-design.md)）

## 參考

- 舊 Form：`reference/old/Ceremony/<FormName>.cs`
- 對應 API endpoints：[api-endpoints/README.md](../api-endpoints/README.md)
- 對應前端頁面：見 [frontend-design.md](../../design/frontend-design.md) 路由表
