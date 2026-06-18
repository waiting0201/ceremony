---
title: Conventions
purpose: 定義命名、commit、分支、程式碼風格的團隊約定
applicable_when: 要寫 commit、要建分支、要決定命名、要設定 linter
related_agents:
  - code-review-optimizer
related_docs:
  - architecture.md
  - workflows/code-review.md
keywords: [conventions, 命名, commit, 分支, lint, 風格, versioning, 版本, semver]
last_updated: 2026-06-18
---

## Commit Message

採 [Conventional Commits](https://www.conventionalcommits.org/)：

```
<type>(<scope>): <subject>

<body>

<footer>
```

**type**：`feat` / `fix` / `docs` / `style` / `refactor` / `test` / `chore` / `perf` / `ci`

**範例**：
```
feat(auth): 加入 Google OAuth 登入

- 支援 Google ID token 驗證
- 新增 /auth/google endpoint

Refs: #123
```

## 分支命名

- `feature/<short-desc>` — 新功能
- `fix/<short-desc>` — bug 修復
- `refactor/<short-desc>` — 重構
- `docs/<short-desc>` — 文件
- `chore/<short-desc>` — 雜項

## 程式碼命名

| 類型 | 命名 | 範例 |
|---|---|---|
| 變數 / 函式 | camelCase | `getUserProfile` |
| 類別 / 型別 | PascalCase | `UserProfile` |
| 常數 | UPPER_SNAKE_CASE | `MAX_RETRY_COUNT` |
| 檔名（程式碼） | kebab-case 或依語言慣例 | `user-service.ts` |
| 檔名（doc） | kebab-case | `user-flow.md` |

## 檔案組織

- 一個檔案一個主要 export
- 測試檔同層放置（`foo.ts` + `foo.test.ts`）或鏡像於 `__tests__/`
- 工具函式放 `utils/` 或 `lib/`，不重複實作

## API 實作約定（CRITICAL — 對應 [CLAUDE.md](../CLAUDE.md) 規則 10）

### 開工前必做（forward）

1. 開 `reference/old/Ceremony/<Form>.cs` 找對應方法 / 事件
2. 複製 [docs/blueprints/api-endpoints/_template.md](blueprints/api-endpoints/_template.md) → `<verb>-<resource>.md`
3. 填完「舊系統對照」段（line ref + 驗證 + 邊界 case 全列），才開 code

### Code 內 doc comment 規範

C# Controller / Service method 必須含 XML doc 指回 blueprint 與舊 code：

```csharp
/// <summary>建立新報名（含編號分配與避 4 規則）</summary>
/// <remarks>
/// Legacy: NewSignupForm.cs:580-720, :842-855 (避4)
/// Blueprint: docs/blueprints/api-endpoints/post-signups.md
/// Coverage: docs/blueprints/legacy-coverage/new-signup-form.md (rows 12-18)
/// </remarks>
[HttpPost]
public async Task<IActionResult> Create(...) { ... }
```

### 實作完成後（reverse）

1. 開對應 [legacy-coverage/<form>.md](blueprints/legacy-coverage/)
2. 把已實作的行勾為 `✅ 已實作`，連結回本 endpoint 的 blueprint
3. 更新 `coverage_percentage`

### PR 描述必含

- (a) 對應 `api-endpoints/<file>.md` 連結
- (b) 已勾選的 `legacy-coverage/<form>.md` 行號（例：`updated rows 12-18`）
- (c) 若 endpoint 對應到「故意捨棄」的舊行為，明確說明理由

### Secret 處理

- 任何 `Password=` / `Secret=` / `Key=` 欄位**絕不**寫進 code / appsettings.json / appsettings.Development.json
- dev：`dotnet user-secrets set "ConnectionStrings:Ceremony" "..."`
- prod：環境變數覆蓋（見 [infrastructure.md](design/infrastructure.md) Secret 管理規則）
- 實際密碼值參考 user auto-memory（`~/.claude/.../memory/db-credentials.md`）

## 軟體版本規範（Versioning）

採 [Semantic Versioning 2.0.0](https://semver.org/)：`MAJOR.MINOR.PATCH`

- **起始版本**：`v2.0.0`（新版 Ceremony 系統的第一個正式版；`v1.x` 保留給 `reference/old/Ceremony/` 舊系統，不再維護）
- **MAJOR**：不相容的 API / 資料結構變更，或重大架構翻新
- **MINOR**：向後相容的新功能（新 endpoint、新頁面、新欄位且不破壞既有契約）
- **PATCH**：向後相容的 bug 修復 / 文案 / 樣式微調

### 規則

- tag 一律加 `v` 前綴：`v2.0.0`、`v2.1.0`、`v2.0.1`
- **版本號單一真實來源 = `frontend/package.json` 的 `version`**（不含 `v` 前綴）。`environment.ts` / `environment.prod.ts` 透過 `import { version } from '../../package.json'` 自動帶入，UI 顯示 `v${version}`（見 [frontend-design.md](design/frontend-design.md)「軟體版本顯示」）。**不可在 environment 內寫死版號**，否則會與 package.json 失聯（2026-06 曾因寫死 `v2.0.0` 導致 UI 不連動）。
- 每次發版只需同步以下 2 處 + 1 筆紀錄：
  - (a) `frontend/package.json` 的 `version`（**唯一要手改的版號**；前端 UI 自動連動，毋需再改 environment）
  - (b) [status.md](status.md) 標頭 Current Version
  - (c) 在 status.md「✅ Recently Done」記一筆對應 commit/PR
- `v0.x` / `v1.x` 不使用（避免與舊系統混淆）

## Lint / Format

- 由各專案 `package.json` / `pyproject.toml` 等設定檔定義
- PR 前必須通過：lint + type-check + test
- 由 [code-review-optimizer](agents-catalog.md) 把關

詳細的程式碼內部風格見：
- [design/frontend-coding-style.md](design/frontend-coding-style.md) — TS / JS / Dart / HTML / CSS
- [design/backend-coding-style.md](design/backend-coding-style.md) — C# / Python / PHP / SQL

## 工具基礎設施

跨檔案層級的工程基礎設施規範（與 lint 規則互補）：

| 項目 | 用途 / 政策 |
|---|---|
| **EditorConfig** | 跨 IDE 縮排 / 換行 / 編碼統一；必 commit `.editorconfig` |
| **Auto-formatter** | Prettier / Black / dotnet format / php-cs-fixer 等；format = 機械、lint = 邏輯，分工明確 |
| **Pre-commit hook** | husky / lint-staged / pre-commit；push 前先擋 lint + format + 部分 test |
| **CI 規則** | format + lint + type-check + test 全綠才能 merge |
| **PR size 軟上限** | 單 PR ≤ 400 行；超過要拆或在 PR 描述說明原因 |
| **Dependency 政策** | lock file 必 commit；定期更新；訂閱 advisory（dependabot / renovate） |
