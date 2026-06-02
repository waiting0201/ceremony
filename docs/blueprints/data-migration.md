---
title: 資料遷移 — N/A（DB 完全凍結，無 migration）
purpose: 標記「資料遷移」議題：客戶要求 DB 完全不動，新系統直連既有 Ceremony DB；無 schema migration、無資料 ETL
status: deprecated
applicable_when: 有人問「新系統要怎麼遷移資料」「要不要跑 migration」時，導向此檔說明「不需要」
related_agents:
  - software-architect-blueprint
  - backend-engineer
related_docs:
  - ../design/database-design.md
keywords: [migration, 資料遷移, datatrans, 沿用, deprecated]
last_updated: 2026-05-26
---

## ⚠️ 此 blueprint 範圍為「無」

**客戶決策（2026-05-26）**：DB **完全凍結**、密碼**沿用明碼**、**不需 migration 工具**。因此：

- ❌ **無 schema migration**（不擴 Password 欄位、不加索引、不加新表）
- ❌ **無 DbUp / EF Migration / Flyway** 等 migration runner
- ❌ **無 Ceremony.Migrations 專案**
- ❌ **無資料 ETL**（沿用既有 Ceremony DB，零搬遷）

詳見 [database-design.md](../design/database-design.md) §「DB 完全凍結」。

## 仍適用的「準資料」場景

雖無 migration 工具，以下情況仍涉及資料處理：

### 1. 舊 DataTrans console（CeremonyNO / CeremonyON → Ceremony）

舊系統的 DataTrans 程式從 `CeremonyNOEntities`（年 110 現場）+ `CeremonyONEntities`（年 119 郵撥）寫入 `Ceremony`。目前 `Main()` 僅啟用 `SyncBeliever()`；`SyncNOs()` / `SyncONs()` 已 comment out。

**新系統不重寫**：若未來再需從 NO/ON 匯入，沿用舊 DataTrans .exe 即可。

### 2. Zipcodes 資料補充

若 Zipcodes 表內容不完整，可用 SQL `INSERT INTO Zipcodes ...` 補資料 — 屬 DML 而非 DDL，**允許**（不算 schema 變更）。

### 3. 法會分類（CeremonyCategorys）新增

業務新增法會 → 應用層 `POST /categories` 寫入。三個根 GUID（春/中/秋）已存在，新分類給 newsequentialid 即可。

### 4. 備份與還原

備份策略不變（既有 SQL Server Agent 排程）。

## 切換上線流程（無資料遷移）

```
1. 部署新後端（Ceremony.Api）
   - 連線字串指向既有 Ceremony DB
   - sa 改為應用專用帳號
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

> **零 migration / 零 ETL** 是「DB 完全凍結」的最大利得：可隨時並行運行新舊版本、零停機切換、無資料遺失風險。

## 風險

- 並行期間舊系統若有 bug 寫壞資料，新系統會看到不一致 — 建議切換前先確認舊系統穩定，或關閉舊系統寫入
- 舊系統 `BaseService.Dispose()` 遞迴 bug — 並行期間若觸發會崩潰；切換前不再大量使用舊系統
- 無索引在大資料量下搜尋慢 — 應用層緩解（分頁/cache/debounce）；無法根本解，需業務同意才能解凍加索引

## 參考資料

- [database-design.md](../design/database-design.md) §「DB 完全凍結」
- [scratch/06-data-layer-migration.md](../../.scratch/explore/06-data-layer-migration.md) — 舊 DataTrans 完整邏輯（備查）
