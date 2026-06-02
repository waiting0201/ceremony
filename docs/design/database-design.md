---
title: Database Design
purpose: 直接沿用本機既有 Ceremony 資料庫（schema 完全凍結）；本檔記錄欄位、關聯、索引、查詢策略
applicable_when: 要寫 EF Core entity 對映、要寫查詢、要看資料關聯、要找索引/約束
related_agents:
  - backend-engineer
  - system-analyst
related_docs:
  - backend-design.md
  - api-design.md
  - security.md
  - ../blueprints/signup-management.md
keywords: [database, db, schema, 資料庫, 索引, 法會, ceremony, signup, believer, MSSQL, EF Core, Database First]
last_updated: 2026-05-29
---

## ⚠️ 重要決策：DB 完全凍結（**客戶需求**）

> 新系統**直接使用本機既有 `Ceremony` 資料庫**，**完全不做任何 DB 變更**：
>
> - 不改欄位名稱（保留 PascalCase：`BelieverID`、`MailZipcodeID`、`Createdate` 等）
> - 不改型別 / 長度 / nullable（**`Admins.Password` 維持 nvarchar(20) 明碼**）
> - 不新增欄位、不刪除欄位（含 `CeremonyDate` 死欄位、`MailZipcode` / `TextZipcode` 純字串）
> - 不新增表（無 audit_logs / login_attempts / migration_progress）
> - **不新增索引**（無 migration 工具、無 DDL 操作）
> - 不改 schema 預設名稱（保留 `dbo`）
> - 不改既有 view 定義（保留 `BelieverView`、`SignupView`）
> - **無 migration 工具**（無 DbUp、無 EF Migration）
>
> 此決策含義：
> 1. **無 DB 變更腳本** — 部署 = 只部署應用程式；DB 動都不動
> 2. **`Admins.Password` 明碼存取**（nvarchar(20)） — 客戶接受
> 3. **JWT 認證** — 應用層產 token，**不需 DB 變更**
> 4. **Dapper ORM**（非 EF Core） — SQL-first，對既有 schema 直接；不需 migration
> 5. **效能優化全在應用層** — 無法靠加索引；用分頁、cache、UPDLOCK 等手法（見 [performance.md](performance.md)）
> 6. **業務規則由應用層 enforce** — 例如「NumberTitle 由 SignupType 推導」「Username 唯一性」全部 service 層檢查

## 技術選型

| 面向 | 選擇 | 理由 |
|---|---|---|
| 主資料庫 | **既有 Microsoft SQL Server**（dev=`(local)` / prod=`192.168.1.151`） | 不動現有環境 |
| ORM | **Dapper 2.x + 手寫 SQL** | 效能可預測、無 N+1、SQL-first；**不需 migration 工具**（POCO 直接對映既有欄位） |
| Migration 工具 | **無** | 客戶要求不動 DB；無 DDL 變更需求 |
| 連線池 | enabled（覆寫舊 `Pooling=False`） | 連線字串設定變更，**不動 DB** |
| 連線字串 | dotnet user-secrets（dev）/ ENV vars（prod） | 不在 App.config 明文，密碼**永不入 repo**（見下方） |
| DB 帳號 | 沿用 `sa`（舊系統慣例，後續可選改為應用專用最小權限） | 客戶需求；改帳號**不算 schema 變更**，可逐步遷移 |
| 備份 | 既有 `BACKUP DATABASE` + 排程；UI 觸發走 API | 路徑改為設定檔可配 |
| 全文檢索 | 不需要 | – |

### 連線環境差異

| 環境 | Server | User | Password 來源 |
|---|---|---|---|
| dev | `(local)` | `sa` | `dotnet user-secrets`（不入 repo） |
| prod | `192.168.1.151` | `sa` | ENV var `ConnectionStrings__Ceremony`（不入 repo） |

> 實際密碼參見 user auto-memory `~/.claude/.../memory/db-credentials.md`。完整 secret 管理規則見 [infrastructure.md Secret 管理規則](infrastructure.md)。

> Dapper 對應既有欄位的方式（無 migration 也能 work）：

```csharp
// Entities/Believer.cs — POCO 對應既有 Believers 表
public class Believer
{
    public Guid BelieverID { get; set; }
    public int EmployeeType { get; set; }
    public string? HallName { get; set; }
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public int? MailZipcodeID { get; set; }
    public string? MailZipcode { get; set; }  // 既有欄位，雖 UI 未填仍對映
    public string? MailAddress { get; set; }
    public int? TextZipcodeID { get; set; }
    public string? TextZipcode { get; set; }
    public string? TextAddress { get; set; }
    public string? LivingNameOne { get; set; }
    public string? LivingNameTwo { get; set; }
    public string? LivingNameThree { get; set; }
    public string? LivingNameFour { get; set; }
    public string? LivingNameFive { get; set; }
    public string? LivingNameSix { get; set; }
    public string? DeadNameOne { get; set; }
    // ... DeadNameTwo..Six
    public bool IsFixedNumber { get; set; }
}

// Dapper 自動以 property name 對應 column name；屬性名與既有 column 完全一致
```

## 命名約定（既有，不改）

| 物件 | 命名 | 範例 |
|---|---|---|
| Schema | `dbo`（預設） | `dbo.Believers` |
| 資料表 | PascalCase 複數 | `Signups`、`SignupLogs`、`CeremonyCategorys`（注意：原 schema 拼 `Categorys`，非標準英文，**不改**） |
| 欄位 | PascalCase | `BelieverID`、`MailZipcodeID`、`Createdate` |
| 主鍵 | `{Entity}ID` | `SignupID` |
| 外鍵 | `{Entity}ID` | `BelieverID`、`MailZipcodeID` |
| Navigation property | EF 自動命名 | `Zipcodes`、`Zipcodes1`（多 FK 區分） |
| Index / constraint | EDMX 既有 | 不新增 |

> **歷史拼字 quirk**：表名是 `CeremonyCategorys`（少了 e/y 互換），非 `CeremonyCategories`。EF Core entity 對映時用 `[Table("CeremonyCategorys")]` 明標。

## 資料表與關聯（既有現況）

### ER 圖

```
┌──────────┐       ┌──────────────────────┐       ┌──────────┐
│ Admins   │       │ CeremonyCategorys    │       │ Zipcodes │
│ AdminID  │       │ self-FK ParentID     │       │ ZipcodeID│
│ Identity │       │ (兩層樹)              │       │ Identity │
└────┬─────┘       └──┬─────────┬─────────┘       └────┬─────┘
     │ (邏輯 1:N)     │ 1:N      │ 1:N (prepay)         │ 1:N ×4
     │  ※無 FK        │           │                       │
     │               ▼           ▼                       │
     │       ┌───────────────────────┐                  │
     │       │      Signups          │◄─FK Mail/Text───┘
     │       │ SignupID (Guid)       │
     │   ◄───┤ BelieverID (nullable) │
     │       │ AdminID (int, no FK)  │
     │       └────┬───────────────────┘
     │            │ 1:N (邏輯)
     │            ▼
     │       ┌───────────┐
     └ ─ ─ ─►│ SignupLogs│ ← 反正規化快照（無 FK）
             └───────────┘
   ┌──────────┐
   │Believers │ ──┐
   │BelieverID│   │ 1:N
   └──────────┘   ▼
                  Signups
```

### 1. `Admins`

| 欄位 | 型別 | NULL | 約束 | 說明 |
|---|---|---|---|---|
| `AdminID` | int IDENTITY | NOT NULL | PK | 管理者編號 |
| `Name` | nvarchar(50) | NULL | – | 姓名 |
| `Username` | nvarchar(50) | NOT NULL | – | 帳號（**無 UNIQUE constraint，由應用層 enforce**） |
| `Password` | **nvarchar(20)** | NOT NULL | – | **明文密碼**（客戶接受） |
| `IsEnabled` | bit | NOT NULL | – | 啟用旗標 |

登入邏輯：應用層直接做 **常數時間明文比對**

```csharp
public async Task<LoginResult> LoginAsync(string username, string password)
{
    // 後門帳號（不寫入 DB）
    if (username == "weypro" && password == "weypro12ab")
        return Success(IssueJwt(adminId: 0, "weypro"));

    var admin = await repo.GetByUsernameAsync(username);
    if (admin == null || !admin.IsEnabled) return Failure();

    // 常數時間明文比對
    if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(admin.Password),
            Encoding.UTF8.GetBytes(password)))
        return Failure();

    return Success(IssueJwt(admin.AdminID, admin.Username));
}
```

軟刪除：刪除 = `IsEnabled = false`（不硬刪）

Username 唯一性檢查（應用層 in service）：
```csharp
public async Task<Result> CreateAsync(string username, ...)
{
    var exists = await conn.ExecuteScalarAsync<int>(
        "SELECT COUNT(1) FROM Admins WHERE Username = @Username", new { username });
    if (exists > 0) return Result.Failure("ADMIN_DUPLICATE_USERNAME");
    // ... insert
}
```

### 2. `CeremonyCategorys`（自我參照樹）

| 欄位 | 型別 | NULL | 約束 | 說明 |
|---|---|---|---|---|
| `CeremonyCategoryID` | uniqueidentifier | NOT NULL | PK | |
| `Title` | nvarchar(50) | NOT NULL | – | 名稱 |
| `ParentID` | uniqueidentifier | NULL | FK→self | 父分類（NULL = 根） |
| `Sort` | int | NOT NULL | – | 顯示序 |

**業務規則**（DB 不限制，**應用層 enforce**）：
- 兩層階層：根（ParentID=null）或第一層（ParentID 指向根）；第一層之下不可再新增
- 刪除限制：`Signups` 無此 CeremonyCategoryID 且無子分類才可刪

三個固定根 GUID：
- 春季：`18927907-dcad-42b2-8f2a-635c2e0fa98d`
- 中元：`0c478f0e-787c-448e-ba7b-b1579f3f1fce`
- 秋季：`3864e4dc-24db-4544-acb3-3351592f6dab`

> **本機 dev 資料現況（2026-05-29）**：dev DB 只有上述 3 筆根分類、**0 筆子項**，故「法會類型維護」頁原本看不到階層（程式正常，純資料缺）。已備 idempotent dev seed 在每個根下塞範例子法會（梁皇寶懺/藥師法會/三時繫念…）：[backend/db/seed/dev-seed-categories.sql](../../backend/db/seed/dev-seed-categories.sql)。**僅供本機 dev**；正式 DB 凍結（[data-migration.md](../blueprints/data-migration.md)）。

### 3. `Zipcodes`

| 欄位 | 型別 | NULL | 約束 | 說明 |
|---|---|---|---|---|
| `ZipcodeID` | int IDENTITY | NOT NULL | PK | |
| `CountryID` | int | NOT NULL | – | 國家 ID（目前唯一台灣） |
| `City` | nvarchar(10) | NOT NULL | – | 縣市 |
| `Area` | nvarchar(10) | NOT NULL | – | 鄉鎮區 |
| `Zipcode` | nvarchar(10) | NOT NULL | – | 郵遞區號 |
| `IsDisplay` | int | NOT NULL | – | 顯示旗標（注意：型別是 int 非 bit） |

### 4. `Believers`（信眾）

| 欄位 | 型別 | NULL | 約束 | 說明 |
|---|---|---|---|---|
| `BelieverID` | uniqueidentifier | NOT NULL | PK | 信眾編號 |
| `EmployeeType` | int | NOT NULL | – | 1=非員工, 2=大殿, 3=地藏殿 |
| `HallName` | nvarchar(10) | NULL | – | 堂號 |
| `Name` | nvarchar(30) | NOT NULL | – | 姓名 |
| `Phone` | nvarchar(30) | NULL | – | 電話 |
| `MailZipcodeID` | int | NULL | FK→Zipcodes | 郵寄區號 |
| `MailZipcode` | nvarchar(10) | NULL | – | 郵寄郵遞區號（**UI 未使用**，保留欄位） |
| `MailAddress` | nvarchar(250) | NULL | – | 郵寄地址（門牌） |
| `TextZipcodeID` | int | NULL | FK→Zipcodes | 疏文區號 |
| `TextZipcode` | nvarchar(10) | NULL | – | 疏文郵遞區號（**UI 未使用**，保留欄位） |
| `TextAddress` | nvarchar(250) | NULL | – | 疏文地址 |
| `LivingNameOne`～`LivingNameSix` | nvarchar(30) × 6 | NULL | – | 陽上名單 |
| `DeadNameOne`～`DeadNameSix` | nvarchar(30) × 6 | NULL | – | 往生名單 |
| `IsFixedNumber` | bit | NOT NULL | – | 固定編號旗標 |

Navigation properties（EF 自動命名）：
- `Zipcodes`：對應 MailZipcodeID
- `Zipcodes1`：對應 TextZipcodeID（命名 quirk，重構時用 attribute 重命名為 `MailZipcode` / `TextZipcode` nav，但 column 名不變）
- `Signups`：1:N，用於刪除檢查

### 5. `Signups`（報名）

| 欄位 | 型別 | NULL | 約束 | 說明 |
|---|---|---|---|---|
| `SignupID` | uniqueidentifier | NOT NULL | PK | |
| `Year` | int | NOT NULL | – | 民國年 |
| `CeremonyCategoryID` | uniqueidentifier | NOT NULL | FK→CeremonyCategorys | 法會 |
| `CeremonyDate` | datetime | NULL | – | **死欄位**（從未讀寫；保留不刪） |
| `SignupType` | int | NOT NULL | – | 1=一般 2=寺方 3=觀音會 4=普桌 5=郵撥 |
| `BelieverID` | uniqueidentifier | NULL | FK→Believers | 信眾（可空） |
| `NumberTitle` | nvarchar(5) | NULL | – | No/寺/觀/普/郵（**由 SignupType 推導，service 層 enforce**） |
| `Number` | int | NULL | – | 編號 |
| `Fee` | int | NULL | – | 費用 |
| `Name` | nvarchar(30) | NULL | – | 報名快照姓名 |
| `Phone` | nvarchar(30) | NULL | – | 報名快照電話 |
| `LivingNameOne`～`LivingNameSix` | nvarchar(30) × 6 | NULL | – | 快照陽上 |
| `DeadNameOne`～`DeadNameSix` | nvarchar(30) × 6 | NULL | – | 快照往生 |
| `MailZipcodeID` | int | NULL | FK→Zipcodes | |
| `MailZipcode` | nvarchar(10) | NULL | – | （保留欄位，UI 未填） |
| `MailAddress` | nvarchar(250) | NULL | – | |
| `TextZipcodeID` | int | NULL | FK→Zipcodes | |
| `TextZipcode` | nvarchar(10) | NULL | – | （保留欄位） |
| `TextAddress` | nvarchar(250) | NULL | – | |
| `Remark` | nvarchar(250) | NULL | – | |
| `PrepayYear` | int | NULL | – | 預繳至年份 |
| `PrepayCeremonyCategoryID` | uniqueidentifier | NULL | FK→CeremonyCategorys | 預繳至法會 |
| `AdminID` | int | NOT NULL | **無 FK constraint** | 建立者（邏輯關聯 Admins） |
| `Createdate` | datetime | NOT NULL | Precision=3 | 建立時間 |

**Application-level 規則**：
- `(Year, CeremonyCategoryID, SignupType, Number)` 唯一性由 service 層在配號時驗證（DB 無 unique index）
- `NumberTitle` 由 `SignupType` 推導（1→No, 2→寺, 3→觀, 4→普, 5→郵），service 層 enforce
- `AdminID` 邏輯關聯 Admins，但 DB 無 FK；service 層讀取時手動 join

### 6. `SignupLogs`（審計快照，反正規化）

| 欄位 | 型別 | NULL | 說明 |
|---|---|---|---|
| `SignupLogID` | uniqueidentifier | NOT NULL | PK |
| `SignupID` | uniqueidentifier | NOT NULL | 對應報名（邏輯關聯，無 FK） |
| `Year`, `CeremonyCategoryTitle`, `SignupType` | – | – | 全字串快照（CeremonyCategoryTitle 是 nvarchar 而非 FK） |
| `HallName`, `Name`, `Phone` | – | – | |
| `NumberTitle`, `Number`, `Fee` | – | – | |
| `LivingNameOne`～`Six`, `DeadNameOne`～`Six` | – | – | |
| `MailCity`, `MailZone`, `MailAddress` | – | – | （展開 Zipcodes 為純文字） |
| `TextCity`, `TextZone`, `TextAddress` | – | – | |
| `Remark` | – | – | |
| `PrepayYear`, `PrepayCeremonyCategoryTitle` | – | – | |
| `Admin` (nvarchar(50)) | NOT NULL | 操作管理員姓名快照 |
| `Createdate` | datetime | NOT NULL | 異動時間 (Precision=3) |

設計重點：
- **無 FK 關聯** — 即使原始實體被刪除，審計紀錄仍存
- **無 action 欄位** — 舊 schema 無法區分新增/編輯/刪除；新系統若需區分，於應用層用「相同 SignupID 第一筆 = 新增」推斷
- **無 diff 邏輯** — UI 層做前後比對

### 7. `BelieverView` / `SignupView`（既有 DB View）

兩個 view 已存在於 DB（對應 EDMX 反向產出的 readonly model）。新系統 EF Core 對映用 `[Keyless]` + `ToView()`。

**SignupView 欄位（既有，無需改 view）**：
- SignupID, Year, CeremonyTitle, CeremonySort, CeremonyCategoryID
- SignupType, NumberTitle, Number, Fee
- Employee（CASE Believers.EmployeeType → 字串）
- Name, HallName, Phone, IsFixedNumber
- LivingNameOne～Six, DeadNameOne～Six
- MailZipcode, MailCity, MailZone, MailAddress
- TextZipcode, TextCity, TextZone, TextAddress
- PrepayYear, PrepayCeremonyCategoryID, PrepayCeremonyTitle
- Remark, AdminName, Createdate

**BelieverView 欄位（既有）**：詳見 [reference/legacy-system-analysis.md](../../reference/legacy-system-analysis.md) §3.3

> View 定義若有需要 query 出來確認，可在 SSMS：`sp_helptext 'BelieverView'`。本文不重複 view DDL。

## 索引（既有，**不新增**）

舊 schema 僅 PK index；客戶要求 DB 完全不動，**不新增任何索引**。

> **效能風險**：在 50k+ 報名規模下，搜尋會 full scan。應用層必須以下列手段補足（詳見 [performance.md](performance.md)）：
>
> 1. **強制 server-side 分頁**（pageSize ≤ 200）
> 2. **善用既有 view**（`SignupView`、`BelieverView`）— 兩個 view 已存在 DB，可能由 DBA 為其加過索引（雖然 EDMX 看不到）
> 3. **In-memory cache** 靜態資料（CeremonyCategorys、Zipcodes）
> 4. **限定可排序欄位**白名單
> 5. **限定可搜尋組合**白名單（避免任意組合產生 table scan）
> 6. **debounce 搜尋**（前端 300ms）
> 7. **UPDLOCK + HOLDLOCK** 解編號 race（query hint，**非 DDL**）
> 8. **歸檔舊年資料** — 若資料量爆炸，未來業務允許時才考慮

> 若未來業務同意動 DB（不在本次範圍），可加的索引清單見 [performance.md](performance.md) §「未來可能的索引（需業務同意）」

## 查詢策略（應用層）

由於 schema 不能改，效能優化全部在應用層：

1. **明確 Include / Select projection**：EF Core 預設關閉 lazy loading（覆寫舊 `LazyLoadingEnabled=true`）；所有 nav 需明確 `Include` 或 `Select` projection
2. **AsNoTracking** for 讀取查詢
3. **使用既有 View** 為 grid 搜尋來源（`SignupView`、`BelieverView`），減少 join 次數
4. **分頁** — 所有 list endpoint 強制 server-side pagination；舊 `.ToList()` over 100k 列禁止
5. **批次列印** — 取資料時用 `WHERE SignupID IN (...)` 一次撈

## 連線設定

連線字串範例（取代舊 `Pooling=False` 等問題）：

```
Server=192.168.1.151;Database=Ceremony;User Id=sa;Password=***;
TrustServerCertificate=true;
MultipleActiveResultSets=true;
Pooling=true;          /* 覆寫舊 Pooling=False */
Max Pool Size=100;
Connection Timeout=30;
```

> Connection String 由 `dotnet user-secrets`（dev）或 secret store（prod）注入；不在 App.config 明文。**這是設定變更，不是 schema 變更**。

## 種子資料

無需處理 — 既有 DB 已含三個根 Ceremony Category、所有 Zipcodes、所有歷史 admins。

## 備份與復原

- 既有 SQL Server Agent 排程（不動）
- UI「資料備份」按鈕走後端 API → 執行 `BACKUP DATABASE`，路徑從設定檔讀（不再硬編碼 `D:\Backup\`）— 屬於應用層變更
- RTO ≤ 1 小時 / RPO ≤ 1 小時（依寺方既有備份策略）

## 已知接受風險

| # | 風險 | 影響 | 接受原因 |
|---|---|---|---|
| 1 | `Admins.Password` 明文 | 高（密碼外洩） | **客戶接受**；應用層用 TLS + secret store 緩解 |
| 2 | 後門帳號 `weypro/weypro12ab` 保留 | 中 | 業務未要求移除 |
| 3 | DB 無索引（除 PK）| 中（大量資料效能差） | DB 凍結；應用層分頁/cache/限縮搜尋緩解 |
| 4 | 無 `Username` unique constraint | 中（可能新增重複帳號） | 應用層 enforce |
| 5 | 無 `(Year, CeremonyCategoryID, SignupType, Number)` unique constraint | 中（race condition 可能重複編號） | 應用層 `UPDLOCK + HOLDLOCK` 序列化處理 |
| 6 | `Signups.AdminID` 無 FK | 低（孤兒記錄） | 應用層保證寫入時 admin 存在 |
| 7 | `CeremonyDate` / `MailZipcode` / `TextZipcode` 等死欄位佔空間 | 低 | DB 凍結 |
| 8 | EF nav 命名 `Zipcodes` / `Zipcodes1` 不直覺 | 低 | C# POCO 用語意化命名 |
| 9 | 無 audit log 表 | 中 | 應用層用 Serilog 寫檔案 log |
| 10 | 無 login_attempts 表 | 低 | 應用層用 IMemoryCache 紀錄失敗計數 |

## 部署前要對 DB 跑的查詢（**待補實際值**）

新系統實作前應先連現有 Ceremony DB 跑下列查詢，把結果回填至本檔對應段落 + [performance.md](performance.md) 效能規畫：

### 取得既有 View DDL

```sql
EXEC sp_helptext N'dbo.SignupView';
EXEC sp_helptext N'dbo.BelieverView';
```

> 新版 Dapper 查詢若直接用既有 view 可省 JOIN；若效能差再改手寫 SQL。

### 既有資料量

```sql
SELECT 'Signups' AS TableName, COUNT(*) AS Cnt FROM dbo.Signups
UNION ALL SELECT 'SignupLogs', COUNT(*) FROM dbo.SignupLogs
UNION ALL SELECT 'Believers', COUNT(*) FROM dbo.Believers
UNION ALL SELECT 'CeremonyCategorys', COUNT(*) FROM dbo.CeremonyCategorys
UNION ALL SELECT 'Zipcodes', COUNT(*) FROM dbo.Zipcodes
UNION ALL SELECT 'Admins', COUNT(*) FROM dbo.Admins;

SELECT Year, COUNT(*) AS Cnt FROM dbo.Signups GROUP BY Year ORDER BY Year DESC;
```

> 影響：效能預估、是否需要分頁、AutoComplete 全載 vs typeahead 抉擇。

### 既有索引清單

```sql
SELECT
    t.name AS TableName,
    i.name AS IndexName,
    i.type_desc AS IndexType,
    STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Columns
FROM sys.tables t
JOIN sys.indexes i ON i.object_id = t.object_id
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.is_primary_key = 0 AND i.type > 0
GROUP BY t.name, i.name, i.type_desc;
```

> 影響：確認 DBA 是否手動加過索引（EDMX 看不到）；若有則 view 查詢可能比預期快。

### 跨年法會分類關聯

```sql
SELECT
    cc.Title, cc.CeremonyCategoryID, cc.ParentID, cc.Sort,
    p.Title AS ParentTitle
FROM dbo.CeremonyCategorys cc
LEFT JOIN dbo.CeremonyCategorys p ON p.CeremonyCategoryID = cc.ParentID
ORDER BY p.Sort, cc.Sort;
```

> 影響：確認「去年春季」與「今年春季」是否共用同一 CeremonyCategoryID（推測：是，法會分類跨年）。決定 LoadPrepay 是否要對應同名分類。

### Admins.Password 現況

```sql
SELECT Username, LEN([Password]) AS PwLen, IsEnabled FROM dbo.Admins;
```

> 影響：確認既有密碼長度範圍（schema nvarchar(20)）；新版若加複雜度驗證要相容既有。

### SQL Server 版本

```sql
SELECT @@VERSION;
SELECT SERVERPROPERTY('Edition'), SERVERPROPERTY('ProductVersion');
```

> 影響：`OFFSET / FETCH NEXT`（2012+ 支援）、`STRING_AGG`（2017+）、`WITH (ONLINE = ON)` 索引（Enterprise）等用法決策。

### 備份策略現況

```sql
SELECT
    database_name, backup_start_date, backup_finish_date,
    type, backup_size, recovery_model
FROM msdb.dbo.backupset
WHERE database_name = 'Ceremony'
ORDER BY backup_start_date DESC;
```

> 影響：新系統 MainForm「資料備份」按鈕觸發策略；是否補充至既有排程。

## 業務不變式（應用層 enforce）

由於 schema 不加 constraint，以下規則全部由 service 層保證：

| 規則 | 實作位置 |
|---|---|
| `NumberTitle` 由 `SignupType` 推導 | `Domain.Services.NumberTitleResolver` |
| `(Year, CeremonyCategoryID, SignupType, Number)` 唯一 | `SignupNumberService.CheckConflict` |
| `CeremonyCategorys` 兩層階層 | `CategoryService.Create`（檢查 ParentID 對應根節點） |
| `CeremonyCategorys` 刪除限制 | `CategoryService.Delete`（檢查 Signups + 子分類） |
| `Believers` 刪除限制 | `BelieverService.Delete`（檢查 Signups） |
| `EmployeeType` 僅 1/2/3 | DTO validator + service guard |
| `SignupType` 僅 1-5 | 同上 |
