# Ceremony 專案文件

本檔為入口目錄表，所有細節分散於 [docs/](docs/)。

## 必要規則

1. **本檔（CLAUDE.md）保持精簡（≤ 200 行）**：只放路由 + 同步規則 + 索引，細節寫到對應 doc
2. **每份 doc 必須含 YAML frontmatter**（schema 見 [docs/architecture.md](docs/architecture.md)）
3. **動 code 必動 doc**：依下方「文件同步規則」執行
4. **討論結論必寫入 doc**：依下方「討論結果自動記錄」執行，不可只留在對話
5. **任務狀態必同步**：依下方「狀態追蹤規則」更新 [docs/status.md](docs/status.md)
6. **記憶按 scope 分流**：依下方「Memory 與專案隔離」執行，本專案知識不寫到 user-scope
7. **新增重大功能必建 blueprint**：複製 [docs/blueprints/_template.md](docs/blueprints/_template.md)
8. **執行非 trivial 任務必走 RPEV**：依 [docs/workflows/research-plan-execute-verify.md](docs/workflows/research-plan-execute-verify.md)
9. **agent 名稱不在內文重複**：只在 frontmatter `related_agents` 與本檔路由表維護
10. **API 任務必走雙向稽核**：
    - **forward**：新增 endpoint 前必查 `reference/old/Ceremony/` 對應 Form，把邏輯/驗證/邊界 case 寫入 [docs/blueprints/api-endpoints/](docs/blueprints/api-endpoints/)（複製 `_template.md`），code 僅放 doc comment 指回 blueprint
    - **reverse**：每個 Form 維護 [docs/blueprints/legacy-coverage/](docs/blueprints/legacy-coverage/) `<form>.md` 覆蓋表；每完成一個 endpoint 回頭勾選；上線前所有 Form 100% 解釋每一行（無 `pending` / `🤔 待確認`）
11. **Secret 絕不入 repo**：DB 帳密 / JWT key / API token 等永不出現在 commit 的檔；dev 走 `dotnet user-secrets`、prod 走 ENV vars / Vault；docs 只寫 template 含 `<from-secrets>` 占位；實際值僅存於 user auto-memory（`~/.claude/.../memory/`）

## 文件同步規則（CRITICAL）

當你修改程式碼、設計、或邏輯時，**必須同步檢視並更新相關文件**。

### 流程

1. **變更前**：先用 grep 搜 `docs/`，找出 frontmatter `keywords` / `applicable_when` 命中變更主題的 doc
2. **變更後**：依下表對照影響面，逐份檢視；若文件描述已不符現況，**主動更新**對應段落並更新 `last_updated`
3. **新功能**：必須在 `docs/blueprints/` 新增一份藍圖（複製 `_template.md`）
4. **不確定**時，**寧可詢問使用者**也不可放任文件腐化

### 變更類型 → 必須檢視的文件

| 變更面向 | 必須同步的文件 |
|---|---|
| UI / 視覺樣式 / 設計語言 | [docs/design/visual-design.md](docs/design/visual-design.md)、[docs/design/frontend-design.md](docs/design/frontend-design.md) |
| 前端元件 / 路由 / 狀態管理 | [docs/design/frontend-design.md](docs/design/frontend-design.md) |
| 後端服務 / 模組職責 / 資料流 | [docs/design/backend-design.md](docs/design/backend-design.md) |
| API endpoint / 契約 / 錯誤碼 | [docs/design/api-design.md](docs/design/api-design.md)、[docs/design/backend-design.md](docs/design/backend-design.md) |
| 資料表 / 索引 / 遷移 | [docs/design/database-design.md](docs/design/database-design.md)、[docs/blueprints/data-migration.md](docs/blueprints/data-migration.md) |
| 效能 / 索引 / 分頁 / Virtual scroll | [docs/design/performance.md](docs/design/performance.md) |
| 業務語意 / 名詞解釋 | [docs/glossary.md](docs/glossary.md) |
| 隱含業務規則 | [docs/business-rules-implicit.md](docs/business-rules-implicit.md) |
| 部署 / 環境變數 / CI/CD | [docs/design/infrastructure.md](docs/design/infrastructure.md) |
| 認證 / 授權 / 加密 | [docs/design/security.md](docs/design/security.md) |
| 新功能 / 重大功能修改 | [docs/blueprints/](docs/blueprints/)`<feature>.md`（新增或更新） |
| 工作流程 / commit / 命名規範 | [docs/conventions.md](docs/conventions.md)、[docs/workflows/](docs/workflows/) |
| 程式碼風格 / lint 規則變更 | [docs/design/frontend-coding-style.md](docs/design/frontend-coding-style.md) 或 [docs/design/backend-coding-style.md](docs/design/backend-coding-style.md) |
| 工具權限 / sandbox / MCP 政策變更 | [docs/design/permissions-sandbox.md](docs/design/permissions-sandbox.md) |
| 新增 / 修改 skill / MCP / 自訂工具 | [docs/design/tools-and-skills.md](docs/design/tools-and-skills.md) |
| 新增 API endpoint | [docs/blueprints/api-endpoints/](docs/blueprints/api-endpoints/) `<verb>-<resource>.md` + [docs/design/api-design.md](docs/design/api-design.md) endpoint 表 + 對應 [docs/blueprints/legacy-coverage/](docs/blueprints/legacy-coverage/) `<form>.md` 行勾選 |
| 月度 / 上線前稽核 | [docs/blueprints/legacy-coverage/](docs/blueprints/legacy-coverage/) 全部 10 份覆蓋表 `last_audited` 與 `coverage_percentage` |
| Secret / 連線設定變動 | [docs/design/infrastructure.md](docs/design/infrastructure.md) Secret 管理規則段（**不寫真實密碼**） |
| 結構化執行流程調整 | [docs/workflows/research-plan-execute-verify.md](docs/workflows/research-plan-execute-verify.md) |
| Harness 行為驗證標準變更 | [docs/evals.md](docs/evals.md) |
| 已知陷阱 / 踩雷紀錄 | [docs/gotchas.md](docs/gotchas.md) |

## 討論結果自動記錄（CRITICAL）

當你與使用者**討論**設計、決策、需求釐清、取捨、規範時，**結論必須立即寫入相關文件**，不可只記在對話或 todo。

### 流程

1. **辨識結論**：對話中出現以下訊號就觸發
   - 「我們決定 X」/「結論是 Y」/「改成 Z」/「不要 A 用 B」/「以後都要這樣做」
   - 使用者明確選擇某選項、回覆 AskUserQuestion、批准 plan
   - 「記下來」/「記到文件」/「同步到 doc」等明確指令
2. **判斷主題**：依下表將結論定位到對應文件
3. **寫入 doc**：直接 Edit 對應段落，並更新 frontmatter 的 `last_updated`
4. **告知使用者**：在回覆中明確說「已記錄到 `<path>`」
5. **不確定** → 用 AskUserQuestion 問「這個結論該記到哪份文件？」

### 結論類型 → 寫入位置

| 結論類型 | 寫入位置 |
|---|---|
| 視覺 / 設計語言決策 | [docs/design/visual-design.md](docs/design/visual-design.md) |
| 前端架構 / 元件規範 | [docs/design/frontend-design.md](docs/design/frontend-design.md) |
| 後端模組 / 資料流決策 | [docs/design/backend-design.md](docs/design/backend-design.md) |
| API 契約 / 錯誤碼 | [docs/design/api-design.md](docs/design/api-design.md) |
| 資料表 / 索引決策 | [docs/design/database-design.md](docs/design/database-design.md) |
| 部署 / 環境策略 | [docs/design/infrastructure.md](docs/design/infrastructure.md) |
| 認證 / 授權 / 加密政策 | [docs/design/security.md](docs/design/security.md) |
| 功能規格 / 範圍取捨 | 對應的 `docs/blueprints/<feature>.md`（不存在則建立） |
| 命名 / commit / 流程規範 | [docs/conventions.md](docs/conventions.md) |
| 程式碼風格決策（語言層級） | [docs/design/frontend-coding-style.md](docs/design/frontend-coding-style.md) 或 [docs/design/backend-coding-style.md](docs/design/backend-coding-style.md) |
| 工作流程 / 步驟調整 | 對應的 `docs/workflows/*.md` |
| 已知陷阱 / 踩雷 | [docs/gotchas.md](docs/gotchas.md) |
| 跨專案通用心得 | Agent user-scope memory（**不寫入本專案** docs/） |

### 記錄原則

- **寫入時保留前後文**：不只寫結論，還要寫「為什麼這樣決定」（取捨）
- **取代而非堆疊**：若新結論推翻舊決策，**改寫**該段落並在 commit message 註明（不要保留矛盾敘述）
- **小而即時**：每次討論結束就寫，不要累積到最後

## 狀態追蹤規則（CRITICAL）

[docs/status.md](docs/status.md) 是專案進度的**單一真實來源**，由 Claude 自動維護。每次會話開始與任務變更時都要對照與更新。

### 觸發點

| 情境 | 動作 |
|---|---|
| 會話開始、收到任務 | 先讀 [docs/status.md](docs/status.md) 了解全局，再決定動作 |
| 使用者說「開始做 X」/「我們來做 Y」 | 加入 **🔄 In Progress**，填 Agent / Blueprint / Started |
| 收到新需求或想法（不立即做） | 加入 **📋 Backlog**，依重要性放 P0 / P1 / P2 |
| 任務完成 | 把項目從 In Progress 搬到 **✅ Recently Done**，寫 Outcome |
| 卡住、等決策、等資料 | 從 In Progress 搬到 **🚧 Blocked**，註明 Blocker / Waiting on |
| 阻塞解除 | 從 Blocked 搬回 In Progress |
| 優先級變動 | 重排 Backlog 內順序 |
| Recently Done 滿 10 項或超過 30 天 | 搬到 **🗄 Archive**（只留摘要，詳情靠 blueprint） |

### 寫入原則

- **每次更新都動 `last_updated`**
- **In Progress 上限 3–5 項**：超過代表並行過載，必須 reprioritize
- **Backlog 條目至少含「為什麼」**：避免變成模糊的願望清單
- **完成項目要連結結果**（blueprint / PR / commit），讓未來可追溯
- 與「討論結果自動記錄」搭配：討論定案後若是新任務，**同時**寫入 status.md 與對應 doc

## Memory 與專案隔離

本專案的所有「記憶」依 scope 分流，避免污染跨專案知識庫。

| 類型 | 位置 | 用途 |
|---|---|---|
| 結構化決策 / 設計 / 規範 | `docs/`（依「討論結果自動記錄」） | 可被 grep、frontmatter 索引 |
| 自動記憶（auto-memory） | `.claude/projects/<sanitized-cwd>/memory/`（Claude Code 預設，已是專案隔離） | 跨會話的本專案上下文 |
| Agent 工作筆記（本專案特有） | `.claude/agent-memory/<agent-name>/`（本專案內） | 各 agent 在本專案累積的觀察 |
| Agent 跨專案通用心得 | `~/.claude/agent-memory/<agent-name>/`（user-scope） | 例：React 常見警告、SQL 優化通則 |

### 判斷準則

- **本專案特有** → 寫入專案內（`docs/` 或 `.claude/agent-memory/`）
  - 例：「我們的 auth 服務在 `/internal/refresh` 有特殊 quirk」
  - 例：「本專案 PostgreSQL 用 schema `app_v2` 而非 public」
- **跨專案通用** → 寫入 user-scope（`~/.claude/agent-memory/<agent>/`）
  - 例：「React Hook Form 與 Zod 整合常見坑」
  - 例：「lint rule X 在不同框架的差異」

### 禁止

- 將本專案特有的設計、踩雷、業務邏輯寫入 `~/.claude/agent-memory/`
- 將跨專案通用心得寫入本專案 `docs/`（會誤導其他專案套用本 harness 的人）

## 任務 → Agent 路由表

| 任務類型 | 主要 agent | 參考文件 |
|---|---|---|
| 新功能開發 | backend-engineer / frontend-architect | [docs/workflows/feature-development.md](docs/workflows/feature-development.md) |
| Bug 修復 | (依領域選擇) | [docs/workflows/bug-fix.md](docs/workflows/bug-fix.md) |
| 程式碼審查 | code-review-optimizer | [docs/workflows/code-review.md](docs/workflows/code-review.md) |
| QA / 測試 | qa-test-engineer | [docs/workflows/qa-testing.md](docs/workflows/qa-testing.md) |
| 系統 / 架構規劃 | software-architect-blueprint / system-analyst | [docs/architecture.md](docs/architecture.md) |
| 視覺 / UI 設計 | visual-design-architect | [docs/design/visual-design.md](docs/design/visual-design.md) |
| 前端設計 / 元件 | frontend-architect | [docs/design/frontend-design.md](docs/design/frontend-design.md) |
| 後端設計 / 服務 | backend-engineer | [docs/design/backend-design.md](docs/design/backend-design.md) |
| API 設計 | backend-engineer / system-analyst | [docs/design/api-design.md](docs/design/api-design.md) |
| 資料庫設計 | backend-engineer / system-analyst | [docs/design/database-design.md](docs/design/database-design.md) |
| 效能議題 | backend-engineer | [docs/design/performance.md](docs/design/performance.md) |
| 行動 App 開發 | mobile-app-engineer | [docs/design/frontend-design.md](docs/design/frontend-design.md) |
| 功能藍圖 / 規劃 | software-architect-blueprint | [docs/blueprints/](docs/blueprints/) |

## 文件索引

- [Status](docs/status.md) — **目前進度 / 待辦 / 完成清單**（會話開始先讀這份）
- [Pending Business Input](docs/pending-business-input.md) — **待業務/DBA/客戶確認的 27 項**（含確認看板）
- [Glossary](docs/glossary.md) — **業務術語對照表**（不熟法會領域必看）
- [Business Rules (Implicit)](docs/business-rules-implicit.md) — **隱含業務規則**（舊系統 code 反推、新版必沿用）
- [Overview](docs/overview.md) — harness 定位與設計理念
- [Installation](docs/installation.md) — 套用到新專案
- [Architecture](docs/architecture.md) — harness 內部結構 + frontmatter schema
- [Conventions](docs/conventions.md) — 命名 / commit / 分支 / 工具基礎設施
- [Agents Catalog](docs/agents-catalog.md) — 所有可用 agent 清單與用途
- [Design](docs/design/) — 各層級設計（視覺 / 前端 / 後端 / API / DB / **效能** / 基礎建設 / 安全 / **編碼風格** / **權限沙箱** / **工具與技能**）
- [Blueprints](docs/blueprints/) — 功能藍圖
- [API Endpoints](docs/blueprints/api-endpoints/) — 每個 endpoint 一份藍圖（規則 A forward）
- [Legacy Coverage](docs/blueprints/legacy-coverage/) — 10 個舊 Form 反向稽核表（規則 A' reverse）
- [Workflows](docs/workflows/) — 各類任務流程（含 **RPEV** 通用框架）
- [Evals](docs/evals.md) — Harness 行為驗證 checklist
- [Gotchas](docs/gotchas.md) — 已知陷阱

## 約定

- **找文件流程**：先讀本檔路由表 → 再依 frontmatter 的 `applicable_when` / `keywords` 確認 → Read 取用
- **agent 不在 doc 內文重複**：只在 frontmatter 標記，避免改名要改一堆地方
- **變更時務必依「文件同步規則」**：這是讓 harness 不腐化的唯一機制
