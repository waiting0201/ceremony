---
title: Performance Design
purpose: 效能策略 — 資料規模成長下的查詢/列印/搜尋/批次/前端渲染效能基準與優化手法
applicable_when: 新增查詢、設計 endpoint、跑慢查詢、規劃索引、批次列印效能、大量資料 grid 顯示
related_agents:
  - backend-engineer
  - frontend-architect
related_docs:
  - backend-design.md
  - database-design.md
  - frontend-design.md
  - ../blueprints/signup-management.md
  - ../blueprints/printing-reports.md
keywords: [performance, 效能, 索引, index, cache, dapper, query, paging, virtualization, signals]
last_updated: 2026-06-29 (DB 解除凍結：索引改為可走 migration 的待評估選項)
---

## ⚠️ 重要前提

**DB 可變更（2026-06-29 解除凍結）**：schema 變更走 DbUp migration（見 [database-design.md](database-design.md)）。效能策略**以應用層為主、索引為輔**：

- ✅ 應用層分頁、cache、debounce、虛擬滾動（**第一線、永遠先做**）
- ✅ Dapper 直接 SQL（無 EF 翻譯地雷）
- ✅ `UPDLOCK / HOLDLOCK` query hint
- ✅ 連線池（連線字串設定）
- ✅ 善用既有 view `SignupView`/`BelieverView`
- ☐ **可加 nonclustered index**（走 migration，熱點欄位；待評估，見本檔末節）
- ☐ 可擴欄位 / 可加新表（如 audit log；走 migration，待個別決定）

> 原則：先用應用層手段壓低成本，索引作為資料量成長後的**互補優化**而非第一手段；任何 schema 變更須**向後相容**（並行運行期舊系統仍讀寫同一 DB）。

> 若未來業務同意加索引，建議清單見本檔末「未來可能的索引（需業務同意）」。

## 預期資料規模

| 資料表 | 5 年內預估 | 10 年內預估 |
|---|---|---|
| Signups | 50,000 列 / 年 → 250k | 500k+ |
| SignupLogs | 3-5× Signups（每筆有多次編輯）| 1.5M～2.5M |
| Believers | 累積 20-30k 信眾 | 40-60k |
| CeremonyCategorys | < 50 | < 100 |
| Zipcodes | ~3,700（固定，台灣行政區劃） | 同 |
| Admins | < 30 | < 50 |

「資料越來越多」核心壓力來自 **Signups 與 SignupLogs**，所有效能設計以此為中心。

## 效能預算（SLA）

| 操作 | 目標 P50 | 目標 P95 | 失敗閾值 |
|---|---|---|---|
| 報名搜尋（單頁 50 筆）| < 200ms | < 500ms | > 2s |
| 新增報名（含寫 SignupLogs）| < 300ms | < 700ms | > 3s |
| 編輯報名 | < 300ms | < 700ms | > 3s |
| 載入預繳（單 case ~200 筆）| < 1s | < 3s | > 10s |
| 批次列印 100 筆 PDF | < 5s | < 15s | > 60s |
| Excel 匯出 5k 列 | < 3s | < 8s | > 30s |
| 登入 | < 200ms | < 500ms | > 2s |
| Electron app 啟動到登入 | < 2s | < 4s | > 8s |

超過失敗閾值要寫警告 log + Grafana alert。

## 後端策略

### 1. ORM：Dapper（非 EF Core）

詳見 [backend-design.md](backend-design.md)。重點：

- **手寫 SQL** — 對效能可預測，免 EF 翻譯地雷
- **避免 N+1** — 一律 JOIN 取齊，或多次 query + 應用層 group
- **AsNoTracking 預設**（Dapper 本來就沒 tracking）
- **Multi mapping** 處理 join：

```csharp
var sql = @"
    SELECT s.*, b.HallName, b.IsFixedNumber, cc.Title AS CeremonyTitle
    FROM Signups s
    LEFT JOIN Believers b ON b.BelieverID = s.BelieverID
    INNER JOIN CeremonyCategorys cc ON cc.CeremonyCategoryID = s.CeremonyCategoryID
    WHERE s.Year = @Year AND s.CeremonyCategoryID = @CeremonyId
    ORDER BY s.Number
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

var result = await conn.QueryAsync<Signup, Believer, CeremonyCategory, SignupRow>(
    sql, (s, b, c) => new SignupRow(s, b, c),
    new { Year = 115, CeremonyId, Offset, PageSize },
    splitOn: "HallName,Title");
```

### 2. 應用層優化（**第一線**）

在加索引之前，應用層必須先以下列手段壓低成本，避免大表搜尋 full scan（即使日後加了索引，這些仍是基本盤）：

1. **強制 server-side 分頁**：所有 list endpoint 接 `page` + `pageSize`，預設 50、max 200。`OFFSET / FETCH NEXT`
2. **善用既有 view**：`SignupView`、`BelieverView` 在 DB 已存在，可能 DBA 為其加過內部 index（EDMX 看不到）— 用 view 取代手寫 JOIN，可能命中既有 plan cache
3. **限縮可搜尋組合**：UI 上必填年份 + 法會（限縮 result set），再用其他條件補
4. **限縮可排序欄位白名單**：避免 user 在無 index 欄位 sort
5. **In-memory cache 靜態資料**：CeremonyCategorys / Zipcodes / 當前 admin → 用 `IMemoryCache`
6. **Debounce 搜尋輸入**：前端 300ms，避免每次輸入打 DB
7. **小批次查詢**：例如預繳載入 100 筆而非一次 1000；分多次 commit
8. **歸檔長期不查的資料**：未來業務若同意，把 N 年前資料移到 `Ceremony_Archive` DB（屬資料變更非 schema 變更）

> 沒有索引的 search 在 50k 列以下尚可，500k 列以上明顯變慢。屆時可走 DbUp migration 加索引（DB 已解除凍結；先確認應用層手段不足再加）。

### 3. 分頁與排序

- **強制 server-side 分頁**：所有 list endpoint 接 `page` + `pageSize`，預設 `pageSize = 50`，max 200
- **OFFSET / FETCH NEXT**（SQL Server 2012+）
- 大 OFFSET（> 10000）效能下滑 → 改 **keyset pagination**（依 last seen ID）
- 排序：限制可排序欄位白名單，避免使用者 sort 在無 index 欄位

### 4. 查詢層級緩存

`IMemoryCache` for 變動少的查詢：

| 資料 | TTL | 重置時機 |
|---|---|---|
| CeremonyCategorys 樹 | 5 min | POST/PUT/DELETE 時 invalidate |
| Zipcodes 全表 | 24 hr | 啟動載入 + 手動 reload |
| Believer dropdown（autocomplete 用）| 不快取 | 改用 typeahead query API |
| 「今年以前最新報名」for believer | 1 min | Signups 寫入時 invalidate by believerId |

不快取：
- Signups 搜尋結果（變動高、組合多）
- SignupLogs（直接讀）
- 個別 Signup / Believer 詳情（修改即時可見）

### Cache 失效策略

```csharp
// CategoryService.cs
public async Task<Result> CreateAsync(CreateCategoryCommand cmd)
{
    var id = await repo.CreateAsync(cmd);
    _cache.Remove(CategoriesCache.TreeKey);  // invalidate
    _cache.Remove(CategoriesCache.ListKey);
    return Result.Success(id);
}

public async Task<Result> UpdateAsync(UpdateCategoryCommand cmd)
{
    await repo.UpdateAsync(cmd);
    _cache.Remove(CategoriesCache.TreeKey);
    _cache.Remove(CategoriesCache.ListKey);
    return Result.Success();
}

public async Task<Result> DeleteAsync(Guid id)
{
    await repo.DeleteAsync(id);
    _cache.Remove(CategoriesCache.TreeKey);
    _cache.Remove(CategoriesCache.ListKey);
    return Result.Success();
}
```

### 多 client 失效（單實例就不必）

法會報名是內網單實例後端，**不需要 distributed cache 同步**。若未來多實例：

- 方案 A：Redis pub/sub 廣播失效訊號
- 方案 B：SignalR 推送至所有 client（client 自己 invalidate 前端 store）

> 目前單實例不必設計這層；列在 [後續可選增強](infrastructure.md#後續可選增強)。

### 5. 連線管理

- 連線池 enabled（覆寫舊 `Pooling=False`）
- `Max Pool Size = 100`
- 連線從 DI 注入 `IDbConnectionFactory.Create()`，using-scope 確保 dispose
- 長查詢設定 `CommandTimeout = 30s`，列印批次可拉長到 120s

### 6. 編號生成（race condition 處理）

舊 `MAX(Number)+1` 兩人同時做會碰撞。新版用：

```csharp
// 重試 5 次內，採序列化交易
await using var tx = conn.BeginTransaction(IsolationLevel.Serializable);
try {
    var nextNumber = await conn.ExecuteScalarAsync<int>(@"
        SELECT ISNULL(MAX(Number), 0) + 1
        FROM Signups WITH (UPDLOCK, HOLDLOCK)
        WHERE Year = @Year AND CeremonyCategoryID = @CeremonyId AND SignupType = @SignupType;",
        new { ... }, tx);

    await conn.ExecuteAsync(@"INSERT INTO Signups (... Number ...) VALUES (... @Number ...);",
        new { ..., Number = nextNumber }, tx);
    tx.Commit();
}
catch (SqlException ex) when (IsDeadlock(ex) && retryCount < 5) {
    // 退避重試
}
```

`UPDLOCK + HOLDLOCK` 在 (Year, CeremonyCategoryID, SignupType) 上阻擋他人 SELECT MAX 同位置。短交易 + 索引 → 衝突可控。

### 7. 批次寫入（LoadPrepay 6 case 各 ~200 筆）

- 用 `Dapper.Contrib` 或自寫批次 INSERT
- 或單一 SQL：
```sql
INSERT INTO Signups (SignupID, Year, ...)
SELECT NEWID(), @Year, ...
FROM @Buffer; -- TVP
```
- 配合 `SqlBulkCopy` 對 10k+ 筆有顯著速度差距

### 8. 列印 PDF 批次

QuestPDF 內建 multi-page document：

```csharp
Document.Create(container => {
    foreach (var signup in signups) {
        container.Page(page => RenderTablet(page, signup));
    }
}).GeneratePdf(stream);
```

- 100 筆 PDF 預期 < 5s
- 1000 筆預期 < 30s（測試後決定是否拆批）
- 預載字型至 process（避免每次重 load）
- PDF 串流到 HTTP response（不寫暫存檔）

### 9. Excel 匯出

- ClosedXML 預設行為夠快（5k 列 < 3s）
- > 10k 列改用 **OpenXmlWriter** streaming（避免整份 in-memory）

## 前端策略

### 1. DataGrid 虛擬滾動

Angular Material `cdk-virtual-scroll-viewport` for 大列表：

```html
<cdk-virtual-scroll-viewport itemSize="24" class="signup-grid">
  <div *cdkVirtualFor="let row of rows()"></div>
</cdk-virtual-scroll-viewport>
```

- 單頁載 50 筆 → 不必虛擬
- 載入全部 500 筆 → 必虛擬（DOM 只渲染可視範圍）

### 2. Signal-first state（取代 RxJS）

Angular 17+ Signals 為主，僅在需要 stream 處理（如 debounce search input）才用 RxJS：

```typescript
// signup-search.store.ts
export class SignupSearchStore {
  // 篩選條件
  readonly year = signal(getCurrentTaiwanYear());
  readonly isScope = signal(false);
  readonly ceremonyId = signal<string | null>(null);
  readonly signupType = signal<number>(-1);
  readonly key = signal('');
  readonly scopeName = signal(true);

  // 結果
  readonly results = signal<SignupRow[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);

  // 衍生
  readonly hasResults = computed(() => this.total() > 0);
  readonly isSearchable = computed(() =>
    this.scopeName() || this.scopeLivingName() || this.scopeDeadName() || this.scopePhone());

  search = async () => {
    this.loading.set(true);
    try {
      const data = await this.api.search(this.toQuery());
      this.results.set(data.items);
      this.total.set(data.total);
    } finally {
      this.loading.set(false);
    }
  };
}
```

- 避免 BehaviorSubject + observable + async pipe 三層轉換
- effect() 連動：當 ceremonyId 變 → 自動觸發 search
- 元件用 `<div>{{ results() }}</div>` 直接讀 signal，OnPush 自動更新

### 3. Search debounce

```typescript
// 搜尋輸入 debounce 300ms
readonly keyInput = signal('');
effect(() => {
  const v = this.keyInput();
  clearTimeout(this.timer);
  this.timer = setTimeout(() => this.store.key.set(v), 300);
});
```

### 4. HttpClient + 分頁載入

- 點分頁 → 載對應 page，不重撈整份
- 切篩選 → reset page = 1
- 列印整批 → 後端 batch endpoint 一次回 PDF（不前端逐筆）

### 5. Service Worker 預載

Electron 主程序預載：
- Zipcodes 全表
- CeremonyCategorys 樹
- 當前 admin 資訊

避免每次新表單都 query 一次。

### 6. Image 與字型預載

- BiauKai 字型 bundle 在 Electron assets 內
- `<link rel="preload">` 確保字型在報表預覽前就 ready

## DB 監控與調校

### 慢查詢追蹤

- Serilog log 任何 > 200ms query（含 SQL 模板 + 參數）
- 每週 review log → 找出熱點
- DBA 用 SQL Server `sys.dm_exec_query_stats` 分析

### Index 健康

- 每月跑 `sys.dm_db_index_usage_stats` 看哪些 index 沒用 → 評估 drop
- 跑 `sys.dm_db_missing_index_details` 看 SQL Server 建議
- fragmentation > 30% 重建（線上 rebuild SQL Server Enterprise；Standard 則 maintenance window）

### 預期成長下的策略

| 階段 | Signups 量 | 動作 |
|---|---|---|
| 啟動 | < 100k | 上述索引足夠 |
| 中期 | 100k - 500k | 監控 OFFSET 大頁數性能；考慮 keyset pagination |
| 長期 | > 500k | 評估按年份分割（partitioned view 或 partitioned table，走 migration） |
| 極長 | > 2M | 歸檔舊年資料至獨立庫 |

## 反模式（禁止）

- ❌ `.ToList()` 沒分頁直接撈
- ❌ 應用層 `.Where()` 過濾大 result set（應在 SQL 完成）
- ❌ 每筆 grid row 觸發單獨 API call
- ❌ 全表 cache 大資料（如 Signups）
- ❌ `SELECT *` 在大表
- ❌ 在 hot path 用 reflection / dynamic
- ❌ Electron renderer 直接連 DB（必經後端 API）

## 可加的索引（走 DbUp migration，待評估）

> DB 已可變更（2026-06-29 解除凍結）。本節索引為**待評估**選項：先確認應用層手段已不足、且資料量確實造成遲滯，再以 migration 導入。

若資料量超過 50k 開始遲滯，可規劃以下索引（純效能優化、向後相容）：

| Table | 索引 | 目的 |
|---|---|---|
| Signups | `(Year, CeremonyCategoryID, SignupType, Number)` INCLUDE (...) | 編號重複檢查、搜尋 |
| Signups | `(BelieverID)` filtered | 預繳查詢 |
| Signups | `(Name)`, `(Phone)` filtered | 關鍵字搜尋 |
| Signups | `(PrepayYear, PrepayCeremonyCategoryID)` filtered | LoadPrepay |
| SignupLogs | `(SignupID, Createdate DESC)` | 歷程查詢 |
| Believers | `(Name)` | 信眾搜尋 |
| Zipcodes | `(City, Area)` | 地址下拉 |
| Admins | UNIQUE `(Username)` | 帳號唯一性 |

加索引時用 `CREATE NONCLUSTERED INDEX ... WITH (ONLINE = ON)`（Enterprise）避免鎖表。

## 驗收

- [ ] 所有 search endpoint 強制分頁
- [ ] **DB 無變更**（無新 index、無新表、無新欄位）
- [ ] 50k 筆 signups 下搜尋 P95 < 1s（應用層分頁緩解）
- [ ] 編號生成在 10 concurrent client 下無碰撞（UPDLOCK + HOLDLOCK）
- [ ] DataGrid 顯示 1000 筆無 lag（virtual scroll）
- [ ] 100 筆批次列印 PDF < 5s
- [ ] Excel 匯出 5k 列 < 3s
- [ ] 慢查詢 log 有寫入
- [ ] CeremonyCategorys / Zipcodes 走 IMemoryCache 載入
