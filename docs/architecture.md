---
title: Harness Architecture
purpose: 描述 harness 內部結構、frontmatter schema、檔案命名約定
applicable_when: 要新增/修改 doc 結構、要理解 harness 如何運作、要寫 doc-lint
related_agents:
  - software-architect-blueprint
related_docs:
  - overview.md
  - conventions.md
keywords: [architecture, schema, frontmatter, 結構, 約定]
last_updated: 2026-05-07
---

## 目錄結構

```
<project>/
├── CLAUDE.md                # 入口：路由表 + 同步規則 + 索引
├── docs/
│   ├── status.md            # 專案進度 / 待辦 / 完成清單（自動維護）
│   ├── evals.md             # Harness 行為驗證 checklist
│   ├── overview.md          # 定位
│   ├── installation.md      # 套用流程
│   ├── architecture.md      # 本檔
│   ├── conventions.md       # 命名 / commit / 工具基礎設施
│   ├── agents-catalog.md    # agent 清單
│   ├── gotchas.md           # 已知陷阱
│   ├── design/              # 設計層級（含 permissions-sandbox.md、tools-and-skills.md）
│   ├── blueprints/          # 功能藍圖（每個功能一份）
│   └── workflows/           # 任務流程（含 research-plan-execute-verify.md 通用框架）
└── .claude/
    ├── settings.json        # 樣板設定
    ├── skills/              # 專案層級 skill
    └── agent-memory/        # 各 agent 在本專案的工作筆記（本專案特有；跨專案心得仍在 ~/.claude/agent-memory/）
        └── <agent-name>/
            └── MEMORY.md
```

## Frontmatter Schema

每份 `docs/**/*.md` 開頭**必須**有 YAML frontmatter：

```yaml
---
title: <人類可讀標題>                       # 必填
purpose: <一句話：這份文件解決什麼問題>      # 必填
applicable_when: <Claude 何時該讀這份文件>   # 必填，自然語言觸發詞
related_agents:                              # 必填（可空陣列）
  - <agent-name>
related_docs:                                # 必填（可空陣列），相對路徑
  - <relative/path.md>
keywords: [<關鍵字1>, <關鍵字2>]            # 必填，利於 grep
last_updated: YYYY-MM-DD                     # 必填，變更時更新
status: draft | active | deprecated          # 選填，預設 active
---
```

## 命名約定

- 檔名：`kebab-case.md`（如 `feature-development.md`）
- 樣板檔：以 `_` 開頭（如 `_template.md`）— 不被視為實際 doc
- 索引檔：`README.md` 放在子目錄根，列出該目錄所有檔案
- 路徑：所有 `related_docs` 用相對路徑（從當前 doc 出發）

## 三層索引設計

1. **CLAUDE.md 路由表**（逆向索引）：任務類型 → agent + doc
2. **Frontmatter `related_agents`**（正向索引）：doc → 適用 agent
3. **Frontmatter `keywords`**（搜尋索引）：grep 進入點

三者**互補不冗餘**：agent 名稱在 CLAUDE.md + frontmatter 維護，doc 內文不重複。

## 知識儲存分層

| 知識類型 | 儲存位置 | 特性 |
|---|---|---|
| 結構化決策 / 規範 | `docs/`（本 harness） | 可被 grep、人/機都讀 |
| 本專案上下文記憶 | `.claude/projects/<sanitized-cwd>/memory/` | Claude Code auto-memory，預設已專案隔離 |
| 本專案 agent 觀察 | `.claude/agent-memory/<agent>/` | 跟著專案走 |
| 跨專案通用心得 | `~/.claude/agent-memory/<agent>/` | 跟著 user 走，不污染專案 |

完整規則見 [../CLAUDE.md](../CLAUDE.md) 「Memory 與專案隔離」段。

## 文件分類原則

| 類別 | 特性 | 命名 |
|---|---|---|
| 通用 | 跨專案共通的元層級資訊 | `docs/<name>.md` |
| 設計（design/） | 按職能分層的設計決策 | `docs/design/<layer>.md` |
| 藍圖（blueprints/） | 按功能的實作規格 | `docs/blueprints/<feature>.md` |
| 流程（workflows/） | 按任務類型的執行步驟 | `docs/workflows/<task>.md` |

## 常見問題

**Q: 一份內容要放 design/ 還是 blueprints/？**
A: design/ 是「持久的設計決策」（例如「我們用 React Query 做 data fetching」）；blueprints/ 是「特定功能的設計」（例如「會員登入功能」）。

**Q: 兩個 design/ doc 內容重疊怎麼辦？**
A: 用 `related_docs` 互相連結，內容只在主導 doc 寫一次，另一份用「見 X.md」帶過。
