---
title: LoadPrepayForm Legacy Coverage
purpose: 反向稽核 — LoadPrepayForm 所有方法/事件的新系統對應狀態（925 行，核心業務）
applicable_when: 完成 POST /prepay/load endpoint 後勾選；月度稽核；上線前 gate
legacy_form: LoadPrepayForm.cs
legacy_path: reference/old/Ceremony/LoadPrepayForm.cs
legacy_lines: 925
audit_status: complete
coverage_percentage: 100
last_audited: 2026-05-27
baseline_completed: 2026-05-27
total_methods: 8
related_agents:
  - backend-engineer
  - qa-test-engineer
related_docs:
  - ../api-endpoints/README.md
  - ../prepay-loading.md
  - README.md
keywords: [legacy, coverage, load-prepay, 預繳載入]
last_updated: 2026-07-17
---

> ✅ **完成 (2026-05-27)**：8 個方法全對應實作或前端化；100% 覆蓋。**第 2 個 complete form**。
> 🎯 已 ship POST /api/v1/prepay/load — 780 行 switch 重構成 data-driven `PrepayGroups` strategy table（6 → 1 個 case），並補強 idempotency（per-believer dedup）+ SignupLog 同步寫入。
> 🔧 **2026-07-04 對齊稽核修正**：(1) Name/Phone 曾誤從來源複製 → 改回 null（舊 LoadPrepayForm 不設此兩欄）；(2) 配號往回設 `nextNo = 固定號+1` 完全對齊舊系統（原 `Math.Max` 偏離）；(3) **並行鎖真正落地**——舊文件/註解聲稱有 UPDLOCK/applock 但實作缺，已重構為「讀 MAX（UPDLOCK/HOLDLOCK）→ 配號 → insert」單一 transaction + `sp_getapplock`；(4) 預覽模式確認**不實作**（對齊舊系統單鍵載入）。配號抽為純函式 `PrepayNumberAllocator`。詳見 [prepay-loading.md](../prepay-loading.md)、[gotchas.md](../../gotchas.md)。
> 🔧 **2026-07-17 修正 7/4 稽核引入的 regression**：(1) 的「Name 留 null」被過度套到 SignupLog 快照，但 `dbo.SignupLogs.Name` 為 NOT NULL（舊系統載入預繳根本不寫 log，此欄無「對齊」問題）→ 真實載入必 500。已改 log Name＝`Believers.Name` 快照；Signup.Name 維持 null。整合測試補上真實 source 的載入回歸鎖。

## 稽核總覽

| 項目 | 值 |
|---|---|
| 總方法 / 事件 / 邏輯區塊數 | 8 |
| ✅ 已實作 | 8 |
| ❌ 故意捨棄 | 0 |
| ⏳ 缺口待補 | 0 |
| 🤔 待確認 | 0 |
| 覆蓋率 | 100% |

## 方法 / 事件 / 邏輯區塊清單

| # | 舊方法/事件 | 舊 code line | 行為摘要 | 新系統狀態 | 對應 endpoint | 備註 |
|---|---|---|---|---|---|---|
| 1 | `LoadPrepayForm()` constructor | 25-34 | 初始化表單、DB 服務、日曆 | ✅ 已實作 | 前端 form init | API 端無對應，UI 由前端 |
| 2 | `LoadPrepayForm_Load` event | 36-43 | 載入信眾、年份、法會資料 | ✅ 已實作 | `GET /api/v1/believers` / `categories` / 前端 year logic | 已有 endpoints 可組合 |
| 3 | `btnConfirm_Click` | 45-824 | **複合邏輯：驗證 + 6 類信眾預繳載入**（780 行大方法） | ✅ 已實作 | `POST /api/v1/prepay/load` | `PrepayLoadHandler` + `PrepayGroups` strategy table (6→1 case)；含 UPDLOCK + transaction + **新版 idempotency** (per-believer dedup) + **新版 SignupLog 同步寫入**；carry-forward 邏輯逐條對齊舊行為 |
| 4 | `LoadBeliever()` helper | 826-865 | 初始化信眾下拉清單 (6 項 hard-coded) | ✅ 已實作 | 前端 enum | 6 個 group 在 `PrepayGroups.All` 為 source of truth；前端可拿來 build dropdown |
| 5 | `LoadSelectCeremony()` helper | 867-878 | 載入預繳法會下拉清單 | ✅ 已實作 | `GET /api/v1/categories` | 已 ship，前端用 |
| 6 | `LoadCeremony()` helper | 880-891 | 載入目標法會下拉清單 | ✅ 已實作 | `GET /api/v1/categories` | 同上 |
| 7 | `LoadSelectYear()` helper | 893-908 | 載入過去 5 年年份清單 | ✅ 已實作 | 前端 logic | TaiwanCalendar.GetYear()-5 至本年 |
| 8 | `LoadYear()` helper | 910-923 | 載入本年與明年清單 | ✅ 已實作 | 前端 logic | TaiwanCalendar.GetYear() / +1 |

## 驗證紀錄

- **2026-05-27 真實 DB smoke test**：
  - 114 春季 → 121 春季 case 1: 載入 30 個非員工一般 signup（全 non-fixed，全 carry-forward）
  - 重跑 → 0 載入、30 skipped（idempotency 驗證）
  - 114 春季 → 122 春季 case 1: 21 載入，20 carry-forward + 1 已結算（謝佳璋 prepay 落在當前 target）— carry-forward 邏輯對齊舊系統
- **15 個 case 自動測試**（unit + integration）通過
