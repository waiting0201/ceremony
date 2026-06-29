---
title: 資料遷移與 schema migration（DbUp）
purpose: 新系統直連既有 Ceremony DB（零 ETL 搬遷）；schema 變更走 DbUp migration（2026-06-29 解除凍結）
status: active
applicable_when: 有人問「新系統要怎麼遷移資料」「schema 變更怎麼管理」「要不要跑 migration」時看此檔
related_agents:
  - software-architect-blueprint
  - backend-engineer
related_docs:
  - ../design/database-design.md
keywords: [migration, 資料遷移, datatrans, 沿用, DbUp, schema]
last_updated: 2026-06-29 (解除 DB 凍結，導入 DbUp schema migration)
---

## 範圍：零 ETL 搬遷 + DbUp schema migration

**決策沿革**：2026-05-26 客戶曾裁定「DB 完全凍結、無 migration」；**2026-06-29 解除此限制**，schema 變更改以 DbUp 版本化管理（見 [database-design.md](../design/database-design.md) §「DB schema 可變更」）。

- ✅ **零資料 ETL**：新系統**直連既有 Ceremony DB**，不搬遷資料（此點不變，仍是最大利得）
- ✅ **schema migration 走 DbUp**：`Ceremony.Migrations` 專案承載版本化 `.sql` 腳本，部署時冪等執行
- ✅ **可變更項**（待各自評估後實作）：擴 `Password` 欄位 / 雜湊化、加索引、加新表（audit_logs 等）、加/改 view
- ⚠️ **向後相容原則**：並行運行新舊版期間，schema 變更須只加不破壞，確保舊系統仍可讀寫同一 DB
- ❌ **不導入 EF Core Migrations / Flyway**：選 DbUp（SQL 腳本式）以與現行 Dapper 相容、避免換 ORM

## 仍適用的「準資料」場景

除 schema migration 外，以下情況涉及資料（DML）處理：

### 1. 舊 DataTrans console（CeremonyNO / CeremonyON → Ceremony）

舊系統的 DataTrans 程式從 `CeremonyNOEntities`（年 110 現場）+ `CeremonyONEntities`（年 119 郵撥）寫入 `Ceremony`。目前 `Main()` 僅啟用 `SyncBeliever()`；`SyncNOs()` / `SyncONs()` 已 comment out。

**新系統不重寫**：若未來再需從 NO/ON 匯入，沿用舊 DataTrans .exe 即可。

### 2. Zipcodes 資料補充

若 Zipcodes 表內容不完整，可用 SQL `INSERT INTO Zipcodes ...` 補資料 — 屬 DML 而非 DDL，**允許**（不算 schema 變更）。

### 3. 法會分類（CeremonyCategorys）新增

業務新增法會 → 應用層 `POST /categories` 寫入。三個根 GUID（春/中/秋）已存在，新分類給 newsequentialid 即可。

### 4. 備份與還原

備份策略不變（既有 SQL Server Agent 排程）。

## 切換上線流程（零資料遷移）

```
1. 部署新後端（Ceremony.Api）
   - 連線字串指向既有 Ceremony DB
   - 執行 DbUp migration（若有待套用的 schema 變更；冪等、向後相容）
   - sa 改為應用專用帳號（DDL 權限視 migration 需求授予）
2. 部署新 Electron client
3. 並行運行（pilot）
   - 1 位使用者試新版，其他人續用舊版
   - 兩版本共用同一 DB，資料即時一致
4. 全體切換
   - 確認新版穩定 → 全員裝新 client
   - 舊系統下架（保留 .exe 30 天以備緊急回退）
5. 觀察 1 週
6. 移除舊系統
```

> **零 ETL** 是直連既有 DB 的最大利得：可並行運行新舊版本、零停機切換、無資料遺失風險。導入 DbUp 後仍維持此利得 —— 前提是 schema 變更**向後相容**（只加不破壞），讓舊系統在並行期仍能讀寫同一 DB。

## 風險

- 並行期間舊系統若有 bug 寫壞資料，新系統會看到不一致 — 建議切換前先確認舊系統穩定，或關閉舊系統寫入
- 舊系統 `BaseService.Dispose()` 遞迴 bug — 並行期間若觸發會崩潰；切換前不再大量使用舊系統
- **schema 變更破壞並行相容**：若 migration 改動既有欄位型別/刪欄，舊系統可能崩潰 — 並行期只做「新增」型變更，破壞性變更留到舊系統完全下架後
- 大資料量下搜尋慢 — 先靠應用層緩解（分頁/cache/debounce），必要時走 migration 加索引（見 [performance.md](../design/performance.md)）

## 參考資料

- [database-design.md](../design/database-design.md) §「DB schema 可變更，導入 DbUp migration」
- [scratch/06-data-layer-migration.md](../../.scratch/explore/06-data-layer-migration.md) — 舊 DataTrans 完整邏輯（備查）
