---
title: Backend Design
purpose: 法會報名系統重構版的後端架構：ASP.NET Core 8 + EF Core 8，Clean Architecture 四層 + Vertical Slices
applicable_when: 要新增/修改後端服務、要決定模組劃分、要重構後端、要設計資料流、要 review API 實作
related_agents:
  - backend-engineer
related_docs:
  - api-design.md
  - backend-coding-style.md
  - database-design.md
  - infrastructure.md
  - security.md
keywords: [backend, 後端, 服務, 架構, 分層, ASP.NET Core, EF Core, Clean Architecture, vertical slice]
last_updated: 2026-06-29 (解除 DB 凍結，導入 DbUp migration；ORM 維持 Dapper)
---

## 已落地骨架（2026-05-27）

- 位置：[../../backend/](../../backend/)（4 個 src project + sln + global.json pinned to SDK 10.0.103）
- 結構：`src/Ceremony.{Domain,Application,Infrastructure,Api}`（無 tests project，下階段補）
- 套件：Dapper 2.1.79、Microsoft.Data.SqlClient、Serilog.AspNetCore、System.IdentityModel.Tokens.Jwt、Microsoft.AspNetCore.Authentication.JwtBearer、Microsoft.Extensions.Caching.Memory
- 已實作：
  - `Ceremony.Domain`：`Admin` entity、`DomainException`
  - `Ceremony.Application/Auth`：`LoginHandler`（後門 + Dapper 比對 + 失敗鎖定 + 常數時間比對）、`JwtTokenService`、`LoginFailureTracker`、`AuthOptions`/`JwtOptions`
  - `Ceremony.Infrastructure`：`SqlConnectionFactory`（IConfiguration 讀 `ConnectionStrings:Ceremony`）、`AdminRepository`（Dapper）
  - `Ceremony.Api`：`AuthController` (`POST /api/v1/auth/login`)、`HealthController` (`/health` SELECT 1)、`ExceptionMiddleware`（DomainException → 400/401/423 + 中文訊息 verbatim）、JWT bearer auth、Serilog console、CORS allow `localhost:4200`
- 設定：`appsettings.json` 只放 template 佔位（`__OVERRIDE_VIA_USER_SECRETS_OR_ENV__`）；實際 connection string + JWT signing key 走 `dotnet user-secrets`（**密碼不入 repo**，見 [infrastructure.md Secret 管理規則](infrastructure.md)）
- 跑：`cd backend/src/Ceremony.Api && ASPNETCORE_ENVIRONMENT=Development dotnet run --urls=http://127.0.0.1:5050`
- 驗證（2026-05-27 通過）：`/health` 連 (local) MSSQL 跑 SELECT 1 = 200 healthy；backdoor `sa@system.local/Admin@123` 發 JWT；驗證錯誤回 400 + 中文 verbatim；錯誤密碼回 401

下階段：先建 `Ceremony.Application.Tests` + `Ceremony.Api.IntegrationTests`，把 LoginHandler 5 個 case 寫成 xUnit，再開始第 2 個 endpoint。

## 技術選型

| 面向 | 選擇 | 理由 |
|---|---|---|
| 語言 | **C# 14**（隨 .NET 10） | 沿用 .NET 生態，舊 EF6 程式邏輯可半移植 |
| Runtime | **.NET 10 LTS**（2026-05-27 升級，原 doc 為 .NET 8） | 現行 LTS；跨平台、長期支援、效能佳 |
| 框架 | **ASP.NET Core 10 Minimal API + Controllers**（hybrid） | 簡單 endpoint 用 Minimal、複雜資源用 Controller |
| ORM | **Dapper 2.x**（micro-ORM）+ 手寫 SQL | 效能可預測、無 N+1、無翻譯地雷；SQL-first 最直接，與 DbUp 腳本式 migration 相容 |
| Migration 工具 | **DbUp**（`Ceremony.Migrations`，版本化 `.sql`） | 2026-06-29 解除 DB 凍結後導入；SQL 腳本式、與 Dapper 相容（不導入 EF Core Migrations） |
| 驗證 | **明文密碼比對**（`FixedTimeEquals`） | `Admins.Password` 現為 nvarchar(20) 明碼；DB 已可變更，雜湊化列為待評估（見 [security.md](security.md)） |
| 授權 | **JWT bearer** | 應用層 token |
| 密碼雜湊 | **暫不採用（待評估）** | 不再受 DB 限制；擴欄/雜湊化可走 migration，待個別決定 |
| Logging | Serilog + Seq（dev）/ File（prod） | 結構化 log；舊系統僅 Debug.Write |
| 任務排程 | 內建 IHostedService（背景備份、log 歸檔） | 不需外部 Hangfire |
| 報表/列印 | **QuestPDF**（首選）或 **Puppeteer Sharp** | 取代 RDLC + LocalReport（Windows-only） |
| Excel 匯出 | **ClosedXML** | 取代 NPOI（.xlsx 比 .xls 健全） |
| 測試 | xUnit + FluentAssertions + Testcontainers (MSSQL) | 跑真 DB，避免舊系統 mock-only 痛點 |

## 解決方案結構（Clean Architecture）

```
Ceremony.sln
├── src/
│   ├── Ceremony.Domain/         # 核心 entity、value object、業務規則、領域事件
│   │   ├── Entities/            # Believer, Signup, SignupLog, CeremonyCategory, Admin, Zipcode（對映既有 DB 欄位）
│   │   ├── ValueObjects/        # NumberToken (NumberTitle+Number), Address, PhoneNumber
│   │   ├── Enums/               # SignupType, EmployeeType
│   │   ├── Services/            # NumberGenerator, AvoidFourFormatter, HallNameSplitter, NumberTitleResolver
│   │   └── Exceptions/          # 業務例外
│   │
│   ├── Ceremony.Application/    # use case、command/query handler、DTO
│   │   ├── Common/              # Behaviors (Validation, Logging, Transaction)
│   │   ├── Believers/           # CRUD + Search use cases
│   │   ├── Signups/             # CRUD + Search + Print prepare use cases
│   │   ├── Prepay/              # LoadPrepay use case (6 cases)
│   │   ├── Categories/          # 法會分類
│   │   ├── Admins/              # 管理員
│   │   ├── Reports/             # 列印資料準備
│   │   └── Auth/                # Login / token issue（明文密碼比對）
│   │
│   ├── Ceremony.Infrastructure/ # Dapper + SqlConnection、repository 實作、外部服務
│   │   ├── Persistence/
│   │   │   ├── IDbConnectionFactory.cs  # 注入連線
│   │   │   ├── SqlConnectionFactory.cs  # 實作 + 從設定取連線字串
│   │   │   ├── Entities/                # POCO（對應既有表）：Believer, Signup, ...
│   │   │   └── SqlMappers.cs            # Dapper TypeHandler / SqlMapper config
│   │   ├── Repositories/        # IBelieverRepository 等 Dapper 實作
│   │   ├── Reporting/           # QuestPDF templates
│   │   └── Backup/              # DB 備份服務
│   │
│   ├── Ceremony.Api/            # ASP.NET Core 主機、Controller、Middleware
│   │   ├── Endpoints/           # Minimal API + Controller
│   │   ├── Middleware/          # Exception, Auth
│   │   ├── DependencyInjection.cs
│   │   └── Program.cs
│   │
│   └── Ceremony.Migrations/     # DbUp：版本化 .sql schema 變更腳本（部署時冪等執行）
│
├── tests/
│   ├── Ceremony.Domain.Tests/         # 純單元測試
│   ├── Ceremony.Application.Tests/    # use case 測試（用既有 DB 或 LocalDB snapshot）
│   ├── Ceremony.Api.IntegrationTests/ # 端到端 HTTP 測試
│   └── Ceremony.Reporting.Tests/      # RDLC vs 新版 PDF 視覺比對
└── docker-compose.yml         # MSSQL（測試）+ Seq 本機開發
```

> **Migration 專案 `Ceremony.Migrations`**（DbUp）：2026-06-29 解除 DB 凍結後導入，承載版本化 `.sql` schema 變更腳本，部署時冪等執行。詳見 [data-migration blueprint](../blueprints/data-migration.md) 與 [database-design.md](database-design.md) §「DB schema 可變更」。

依賴方向：`Api → Application → Domain`；`Infrastructure → Application/Domain`（依賴反轉）

## 模組職責

| 模組 | 職責 | 不負責 |
|---|---|---|
| Domain | 業務規則、不變式（編號生成、避 4、堂號拆分、預繳分群）| 持久化、HTTP、PDF 生成 |
| Application | 編排 use case、交易邊界、權限檢查 | DB 細節、HTTP 細節 |
| Infrastructure | EF DbContext、Repo 實作、PDF/Excel/Backup | 業務邏輯 |
| Api | HTTP 入口、DTO 序列化、模型驗證、JWT | 業務邏輯 |

## 業務邏輯歸屬（從舊 Form 移出至 Domain/Application）

舊系統 Form 內含 800+ 行業務邏輯。重構時依下表搬遷；**每行必須對應一份 [api-endpoints blueprint](../blueprints/api-endpoints/) 與一份 [legacy-coverage 表](../blueprints/legacy-coverage/)**：

| 舊邏輯 | 新位置 | 說明 | 對應 blueprint / coverage |
|---|---|---|---|
| `Library.GetSignupNumber()` | `Domain.Services.NumberGenerator` | 純函式 + 注入 ISignupReadRepository | [post-signups.md](../blueprints/api-endpoints/) / [signup-form.md](../blueprints/legacy-coverage/signup-form.md) |
| `Library.GetNumberText()`（避 4） | `Domain.Services.AvoidFourFormatter` | 純函式，個位 4 → "3-1" | 同上 |
| `Library.DrawText()` | `Infrastructure.Reporting.VerticalTextRenderer` | 圖片 IO 屬於 infra | 列印 PoC 決策後補 |
| 堂號拆分（SignupForm 內聯） | `Domain.Services.HallNameSplitter` | 2 字 1+1、4 字 2+2 | [signup-form.md](../blueprints/legacy-coverage/signup-form.md) |
| NumberTitle 推導 switch | `Domain.Enums.SignupType.ToNumberTitle()` 擴充方法 | | [new-signup-form.md](../blueprints/legacy-coverage/new-signup-form.md) |
| 預繳 6-case 分群 | `Application.Prepay.LoadPrepayHandler` | 重構為 data-driven，6 case 用 strategy/table | [load-prepay-form.md](../blueprints/legacy-coverage/load-prepay-form.md) |
| PredicateBuilder | `Application.Common.Search.QueryBuilder` | 改用 EF Core 內建 + Expression composition | [signup-form.md](../blueprints/legacy-coverage/signup-form.md) |
| 三段 SaveChanges | `Application.Common.Behaviors.TransactionBehavior` | MediatR pipeline 包整個 command | – |
| Address fallback（疏文 ← 寄件） | `Application.Signups.AddressFallback` | 共用 | [new-signup-form.md](../blueprints/legacy-coverage/new-signup-form.md) |
| BelieverSelected 資料優先順序 | `Application.Signups.PopulateFromBelieverHandler` | 明確化 Signup > GridRow > Believer | [new-signup-form.md](../blueprints/legacy-coverage/new-signup-form.md) |

### API 實作檢核清單（forward + reverse）

每個 endpoint 完成必同時通過：

- [ ] forward：[api-endpoints/<file>.md](../blueprints/api-endpoints/) 完整含舊系統對照、驗證、邊界 case
- [ ] reverse：[legacy-coverage/<form>.md](../blueprints/legacy-coverage/) 對應行勾為 ✅ / ❌（含理由）
- [ ] Code 含 XML doc comment 指向 blueprint + 舊 Form line ref（見 [conventions.md](../conventions.md)）
- [ ] 無 Secret 進 repo（`grep -rE "Password=[^_<]"` 0 命中）

## 資料流（典型請求）

```
Client (Electron Angular/Vue)
   ↓ HTTPS + JWT
Api Endpoint (Minimal/Controller)
   ↓ 驗證 DTO
MediatR Command/Query Handler (Application)
   ↓ 編排
Domain Service / Entity（業務不變式檢查）
   ↓ 透過 Repository
Infrastructure (EF Core DbContext)
   ↓ TSQL
SQL Server
```

跨切面（pipeline behaviors）：
1. **ValidationBehavior** — FluentValidation 自動驗 command DTO
2. **LoggingBehavior** — 記錄 request/response（避免 PII，做欄位 mask）
3. **TransactionBehavior** — 對 ICommand 開啟 EF transaction，pipeline 完成才 commit
4. **AuditBehavior** — 變更類操作自動寫 audit log

## 錯誤處理

- **Domain 拋業務例外**：`DomainException`、`NotFoundException`、`ConflictException`、`ValidationException`
- **Application 轉 Result**：成功 `Result.Success<T>(data)`、失敗 `Result.Failure(errorCode, message)`
- **Api 統一 middleware**：依例外型別映射 HTTP status：
  - `ValidationException` → 400
  - `NotFoundException` → 404
  - `ConflictException` → 409
  - `UnauthorizedException` → 401
  - `ForbiddenException` → 403
  - 其他未捕獲 → 500，log + 回傳 traceId

舊系統 IResult 模式被 `Result<T>`（更明確）取代。詳見 [api-design.md](api-design.md) 錯誤碼章節。

## 交易邊界

舊系統 NewSignup / EditSignup **三段 SaveChanges 非原子**（信眾 → 報名 → 審計）。新系統：

- 一個 use case = 一個 EF transaction
- **CreateSignupCommand** 透過 `TransactionBehavior` 包覆，三個實體新增在同一 transaction，任一失敗整個 rollback
- LoadPrepayCommand：原本就單次 SaveChanges → 保留 atomic
- 跨服務交易（例：寄送 email）採 outbox pattern（目前無此需求）

## 觀測

- 結構化 log（含 `trace_id` / `user_id` / `command_name`），輸出到 Seq（dev）/ 檔案（prod）
- 關鍵指標（metric）：
  - 報名建立 P50/P95 latency
  - 預繳載入次數與耗時
  - 列印失敗率
- Health check：`/health`（DB 連線、磁碟空間）
- 詳見 [infrastructure.md](infrastructure.md)

## 重要決策（與舊系統的差異）

> 注意：**DB schema 可變更，走 DbUp migration**（2026-06-29 解除凍結，見 [database-design.md](database-design.md)）。下列差異多數仍發生在 .NET 應用層 / 設定層；schema 變更則一律走 migration 腳本。

| 主題 | 舊 | 新 | 原因 |
|---|---|---|---|
| ORM | EF6 EDMX | **Dapper 2.x + 手寫 SQL** | 效能可預測、無 N+1、無 EF 翻譯地雷；SQL-first 最直接 |
| Migration | EDMX 反向工程 | **DbUp**（`Ceremony.Migrations`） | 版本化 SQL 腳本管理 schema 變更；與 Dapper 相容 |
| Repository pattern | 泛型 GenericRepository（薄） | 每聚合一個 specific repository | 包 Dapper 提供強型別查詢 |
| Service pattern | BaseService 純繼承（CRUD 包裝） | MediatR Handlers（每 use case 一個） | use case 為一級概念，便於測試 |
| Dispose 模式 | `this.Dispose()` 無窮遞迴 ⚠️ | DI lifecycle 管理 | 修 bug |
| Validation | Form 內散落 MessageBox | FluentValidation + 統一錯誤回應 | 集中 + i18n-ready |
| Connection pooling | `Pooling=False`（每次重建） | 預設啟用 + Max Pool Size 100 | 效能（連線字串設定，非 schema） |
| LazyLoading / nav | EF6 預設 lazy，N+1 滿天飛 | 無 nav（Dapper 不做 nav） | 改用 SQL JOIN 一次取齊 |
| Transaction | 多次 SaveChanges 非原子 | `IDbTransaction` + MediatR TransactionBehavior | 資料完整性 |
| 業務邏輯 | 在 Form (800+ 行) | 在 Domain Service | 可測試 / 可重用 |
| 編號生成 | `MAX(number)+1`，race condition | `UPDLOCK + HOLDLOCK` + retry | 多 session 安全（query hint，非 DDL） |
| 密碼 | 明文 | **現況明文 + 常數時間比對**；雜湊化待評估 | 不再受 DB 限制，雜湊化可走 migration（見 [security.md](security.md)） |
| JWT | 無 session | JWT access + refresh token | 應用層 token |
| 連線字串存放 | App.config 明文 sa 帳號 | secret store / DPAPI；DB 帳號改應用專用 | 設定層 + DB 帳號層保護 |

## Dapper 使用慣例

```csharp
// repository 範例
public class SignupRepository : ISignupRepository
{
    private readonly IDbConnectionFactory _factory;

    public async Task<PagedResult<SignupRow>> SearchAsync(SignupSearchQuery q, CancellationToken ct)
    {
        using var conn = await _factory.CreateAsync(ct);
        var sql = @"
            SELECT s.SignupID, s.Year, cc.Title AS CeremonyTitle, ...
            FROM Signups s
            LEFT JOIN Believers b ON b.BelieverID = s.BelieverID
            INNER JOIN CeremonyCategorys cc ON cc.CeremonyCategoryID = s.CeremonyCategoryID
            WHERE (@Year IS NULL OR s.Year = @Year)
              AND (@CeremonyId IS NULL OR s.CeremonyCategoryID = @CeremonyId)
              AND (@SignupType = -1 OR s.SignupType = @SignupType)
              -- ... 其他條件
            ORDER BY s.Year DESC, cc.Sort, s.NumberTitle, s.Number
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*) FROM Signups s WHERE ...;";
        using var multi = await conn.QueryMultipleAsync(sql, q, commandTimeout: 30);
        var items = (await multi.ReadAsync<SignupRow>()).ToList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<SignupRow>(items, total, q.Page, q.PageSize);
    }
}
```

慣例：
- 所有 query 用 **參數化**（`@Name`）；禁止字串拼接
- 動態條件用 conditional `WHERE`（`(@X IS NULL OR col = @X)`）
- 分頁用 `OFFSET / FETCH NEXT`
- 多 result set 用 `QueryMultipleAsync`
- Multi-mapping 用 `splitOn`
- 列印 / 報名等熱路徑明確設 `commandTimeout`
- 寫入用 `IDbTransaction` 包多步驟

## 程式碼風格

詳見 [backend-coding-style.md](backend-coding-style.md)。重點：
- nullable reference types 開啟
- 不寫 `var` 在公開 API 簽章
- 公開 API XML doc comment（會被 Swagger 取用）
- 每個 Handler 一個檔案
- DTO 用 record，entity 用 class
- **所有 SQL 集中在 Repository**，不在 Handler 內出現 SQL 字串
