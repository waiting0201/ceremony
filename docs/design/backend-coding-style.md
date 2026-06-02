---
title: Backend Coding Style
purpose: 後端各語言詳細風格指南，作為 PR / code review 的客觀依據
applicable_when: 撰寫後端程式碼、code review 後端 PR、設定 lint rule、新人 onboarding
related_agents:
  - backend-engineer
  - code-review-optimizer
related_docs:
  - backend-design.md
  - api-design.md
  - database-design.md
  - security.md
  - ../conventions.md
keywords: [coding-style, backend, csharp, python, php, style-guide, lint]
last_updated: 2026-05-07
---

## 0. 元規則

四條原則優先於後續所有具體規則：

1. **可驗證 > 願望**：每條規則應對應到 lint rule、test、或 review checklist；無法自動驗證的標 `[guideline]`
2. **一致 > 個人偏好**：要改就改規則本身（PR 改本檔）
3. **清楚 > 簡短**：除非簡短就清楚
4. **入口驗證、內部信任**：邊界檢查集中於 controller / API 層，domain 內部不重複防禦

## 1. 跨語言通則

### 結構
- **Early return** 勝過多層 nesting（≥ 3 層觸發拆分檢討）
- 函式單一職責；超過 ~50 行考慮拆分（`[guideline]`）
- 檔案 ≥ 300 行考慮拆分（`[guideline]`）
- 偏好純函式（無副作用）；副作用集中於 application / infrastructure 層

### 命名
- 表達意圖、避免無意義縮寫
- Boolean：`is` / `has` / `can` 前綴；避免雙重否定
- async 方法在語言慣例下加後綴（C# `XxxAsync`、Python 不加）

### 註解
- 預設**不寫**；只寫 **why**，不寫 what
- TODO / FIXME 必含 owner 或 issue 連結
- 公開 API 寫 docstring / XML doc

### Magic numbers / 設定值
- 抽常數，命名表達意圖
- 環境差異值放環境變數或 config（不寫死）

### 日期 / 時區 / 金額
- 內部一律 **UTC**；DB 也存 UTC
- C# 用 `DateTimeOffset`、Python 用 timezone-aware `datetime`、PHP 用 `DateTimeImmutable` + UTC
- 金額用 `decimal` / `Decimal` / `BCMath`；**不**用 `float` / `double`
- 單位明確（`durationMs`、`amountCents`）

### Logging
- 結構化（JSON 或 logfmt），含 `traceId` / `userId` / `level`
- level：`DEBUG` / `INFO` / `WARN` / `ERROR`
- **不** log PII / token / password / 信用卡（cross-link [security.md](security.md)）

### Test 風格
- AAA pattern（Arrange-Act-Assert）
- Test 名稱表達意圖：`Should_<behavior>_When_<condition>` 或 `test_<behavior>_when_<condition>`
- 避免共享 fixture 改動全域狀態；用 per-test setup
- 整合測試用真實 DB / 外部服務（不過度 mock）

### Error message
- **對使用者**（API response）：友善、可行動、不洩漏內部結構（stack、SQL、檔案路徑）
- **對開發者**（log）：詳細、含 traceId
- 錯誤碼遵循 [api-design.md](api-design.md)

### Defensive programming 邊界
- 入口（controller / API handler）做完整輸入驗證
- domain / service 層 trust 上層，不重複 null check
- 對外部呼叫（DB、第三方 API）必做 retry / timeout 設定

## 2. C# / .NET

### 基礎
- **Nullable reference types** 開啟（`<Nullable>enable</Nullable>`）
- 偏好 `record` 用於不可變資料；`class` 用於有行為的物件
- `var` 政策：當右側型別明顯時用 `var`；否則寫明型別

### Naming
- Async 方法：`XxxAsync`
- Interface：`IXxx`
- 私有欄位：`_camelCase`（依專案決定，全專案一致）
- 常數：`PascalCase`（C# 慣例，不用 SCREAMING_SNAKE）

### LINQ
- 偏好可讀性；複雜 query 拆多步
- 避免在熱路徑 `ToList()` / `ToArray()`（會 enumerate 整個序列）
- IEnumerable vs IQueryable 區分清楚（前者記憶體、後者 DB）

### Async
- `async Task` 或 `async ValueTask`，**不**用 `async void`（除 event handler）
- **禁** `.Result` / `.Wait()`（會 deadlock）
- `CancellationToken` 從入口傳到底
- 函式庫程式碼考慮 `ConfigureAwait(false)`（依專案政策）

### 錯誤處理
- Domain 例外（自訂）vs Result pattern（依專案選一）
- **禁** catch `Exception` 通殺（除非最外層 logging middleware）
- 例外用於異常情境，**不**用於控制流程

### DI / 生命週期
- Singleton 慎用（thread-safe 風險）
- Scoped 為預設
- 抽象介面放在 Application / Domain；實作放 Infrastructure

### EF Core / Dapper
- 詳見 [database-design.md](database-design.md)
- 避免 `Include` 過深 → N+1
- 大量讀取用 `AsNoTracking()`
- 寫入用交易（`UseTransaction`）

## 3. Python

### 基礎
- PEP8 + **ruff**（lint + format 一站式）
- type hints 全覆蓋（含參數、回傳）
- `from __future__ import annotations`（lazy evaluation 型別）

### Imports
- isort 群組：標準庫 → 第三方 → 本地
- 偏好絕對 import；同套件內可用相對

### Naming
- `snake_case` 變數 / 函式
- `_protected` / `__private`
- `CONSTANT_CASE` 常數
- `PascalCase` 類別

### Async
- `asyncio` 為主；**不**混用 thread + event loop
- 不在 sync 函式裡 `asyncio.run`（除頂層）
- 用 `asyncio.gather` / `TaskGroup` 平行化

### 錯誤處理
- 明確例外類型；**禁** bare `except:`
- 自訂例外繼承自 domain 基底類別
- `try / finally` 釋放資源（或 context manager）

### Dataclass / Pydantic
- 內部資料：`@dataclass`（無驗證需求）或 `pydantic.BaseModel`（需驗證）
- 邊界（API request/response）：Pydantic
- 全專案一致選一個方向

### 註解 / docstring
- Google / NumPy / Sphinx 三選一，全專案一致
- 公開 API 必寫 docstring；內部依需求

## 4. PHP

### 基礎
- PSR-12 + PHPStan level（`max` 為目標，最低 level 5）
- `declare(strict_types=1);` 每檔案開頭

### Naming
- PSR-4 autoload；namespace 對應目錄
- camelCase 方法
- PascalCase 類別 / interface
- SNAKE_UPPER 常數

### 強型別
- 參數、回傳值**全標型別**
- 偏好 nullable 型別（`?string`）勝過 union 預設
- 用 enum（PHP 8.1+）取代 const class

### 錯誤處理
- 偏好 exception；**避免** false return 表錯誤
- **禁** `@` 抑制錯誤
- 自訂 domain exception 繼承基底類別

### Laravel / Symfony 慣例（如適用）
- 控制器薄：呼叫 service，不寫商業邏輯
- Form Request / DTO 做輸入驗證（不在 controller 內 inline）
- Eloquent / Doctrine 慣例見 [database-design.md](database-design.md)

## 5. SQL（小節）

> SQL schema 設計詳見 [database-design.md](database-design.md)；本段僅為**寫 query 風格**。

- 關鍵字大寫（`SELECT` / `FROM` / `WHERE`），表 / 欄位 snake_case
- JOIN 條件寫 `ON`，**不**寫 `WHERE`
- 大查詢用 CTE（WITH）拆步驟
- 避免 `SELECT *`；明列欄位
- 分頁用 keyset / cursor，避免大 OFFSET

## 6. 與其他文件的關係

- **架構決策**（分層、模組職責）：[backend-design.md](backend-design.md)
- **API 契約**（endpoint、錯誤碼）：[api-design.md](api-design.md)
- **DB schema / 索引**：[database-design.md](database-design.md)
- **流程約定**（commit、branch、檔名）：[../conventions.md](../conventions.md)
- **安全相關**（PII、認證、授權）：[security.md](security.md)
- **常見踩雷**：[../gotchas.md](../gotchas.md)
