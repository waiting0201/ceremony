---
title: Tools, MCP & Skills
purpose: 規範如何撰寫 skill、引入 MCP server、設計自訂工具；對齊 agent specialization 與工具限縮原則
applicable_when: 要寫新 skill、要加 MCP、要設計自訂 tool、要為 agent 限縮工具範圍時
related_agents:
  - backend-engineer
  - software-architect-blueprint
related_docs:
  - permissions-sandbox.md
  - ../agents-catalog.md
keywords: [tools, mcp, skills, slash-command, subagent, 工具, 自訂指令, agent-specialization]
last_updated: 2026-05-07
---

## 為什麼

OpenAI Harness Engineering 第二支柱是 **agent specialization**：「焦點 agent + 限縮工具」優於「通用 agent + 全工具」。本檔規範如何在 Claude Code 內實踐這原則。

## 1. 三種擴充機制比較

| 機制 | 觸發 | 適合 | 位置 |
|---|---|---|---|
| **Skill** | user 打 `/<name>` 或 description 自動匹配 | 流程封裝、prompt 模板、SOP | `~/.claude/skills/` 或 `.claude/skills/` |
| **MCP server** | agent 自動呼叫 `mcp__<server>__<tool>` | 外部系統整合、新 capability | `~/.claude.json` 或 `.mcp.json` |
| **自訂 sub-agent** | user 指定或 description 匹配 | 領域專家 + 工具限縮 + 獨立 system prompt | `~/.claude/agents/` 或 `.claude/agents/` |

### 選擇指引

- **要封裝對話流程** → Skill（例如：`/code-review`、`/init`）
- **要存取新外部資源** → MCP server（例如：chrome-devtools、Slack、自家 API）
- **要長期穩定的領域專家** → Sub-agent（例如：qa-test-engineer、frontend-architect）

可組合：sub-agent 內部 system prompt 可呼叫 skill 與 MCP tool。

## 2. Skill 撰寫規範

### 檔案結構

`~/.claude/skills/<name>/SKILL.md`：

```yaml
---
name: <kebab-case-name>
description: <≤ 1536 字元；用自然語言描述用途與觸發條件；模型會用這段判斷是否啟動>
allowed-tools: [Read, Grep, Bash]   # 選填：限縮可用工具（agent specialization）
---

# Skill 內容（system-prompt 風格）
```

### 命名約定

- kebab-case
- 動詞開頭（`init` / `review` / `summarize`）或名詞短語（`security-review`）
- slash command 自動為 `/<name>`

### Description 撰寫要點

description 是觸發核心，要包含：

1. **這個 skill 做什麼**（一句話）
2. **何時應該觸發**（自然語言觸發詞）
3. **何時不該觸發**（避免誤觸）

範例（取自實際 skill）：
> Use when the user asks to set up a recurring task, poll for status, or run something repeatedly on an interval (e.g. "check the deploy every 5 minutes"). Do NOT invoke for one-off tasks.

### `allowed-tools`（重要）

明確列出 skill 可用的工具，**體現 agent specialization**。例如 `init` skill 只需要 Read / Write / Bash，就不要開放 Edit。

## 3. MCP Server 接入流程

### 兩種 scope

| Scope | 設定位置 | 適用 |
|---|---|---|
| User | `~/.claude.json`，用 `claude mcp add --scope user --` 維護 | 跨專案個人工具（例如 chrome-devtools） |
| Project | `<project>/.mcp.json` 提交進 repo | 團隊共用 |

### 接入步驟

```bash
# User scope（個人）
claude mcp add <name> --scope user -- <command> [args...]

# Project scope（建立 .mcp.json）
# 範例 .mcp.json:
{
  "mcpServers": {
    "chrome-devtools": {
      "command": "npx",
      "args": ["-y", "chrome-devtools-mcp@latest"]
    }
  }
}
```

接入後：

1. 在本檔「已接入 MCP 清單」新增條目
2. 若是 project scope，在 [permissions-sandbox.md](permissions-sandbox.md) 的 `enabledMcpjsonServers` 白名單列出
3. 寫一個對應的使用指引（在哪個 agent / workflow 用、用來做什麼）

### 已接入 MCP 清單

| Name | Scope | 用途 | 主要使用者 |
|---|---|---|---|
| chrome-devtools | user | 前端 runtime 審查（console / network / performance / a11y） | qa-test-engineer |

## 4. 自訂 Sub-agent

### 目錄
- User scope：`~/.claude/agents/<name>.md`（跨專案共用）
- Project scope：`.claude/agents/<name>.md`（專案專用）

### Frontmatter

```yaml
---
name: <agent-name>
description: <用途說明 + 範例情境>
model: sonnet | opus | haiku
color: <顯示顏色>
tools: [Read, Edit, Bash, Grep, Glob]   # 體現工具限縮，重要！
memory: user                              # user-scope memory
---

# Agent system prompt
```

### 工具限縮原則

預設不寫 `tools:` 的 agent 拿到所有工具，這違反 OpenAI 的 agent specialization 原則。建議：

| Agent 類型 | 應限縮的工具範圍 |
|---|---|
| 純審查（如 qa-test-engineer） | Read、Grep、Glob、Bash（read-only）；**不**給 Edit / Write |
| 設計規劃（如 software-architect-blueprint） | Read、Grep、Glob、Write（plan 檔）；**不**給 Edit |
| 實作執行（backend-engineer 等） | 全工具，但敏感操作交由 [permissions-sandbox.md](permissions-sandbox.md) 把關 |

> 目前 [agents-catalog.md](../agents-catalog.md) 的 8 個 user-scope agent 多採隱式宣告（無 `tools:`）。**遷移計畫**：每次修改 agent 時順手加上 `tools:`，不另開 PR。

### 命名

- kebab-case
- 名詞或角色描述（`backend-engineer`、`code-review-optimizer`）

## 5. 目錄總覽

```
~/.claude/
├── agents/                          # User-scope agents
├── skills/                          # User-scope skills
├── settings.json                    # User settings
└── (~/.claude.json)                 # User MCP servers (由 claude mcp add 維護)

<project>/.claude/
├── agents/                          # Project-scope agents
├── skills/                          # Project-scope skills
├── settings.json                    # Project settings (commit)
├── settings.local.json              # 個人覆寫 (gitignore)
└── (project)/.mcp.json              # Project MCP servers
```

## 6. 變更流程

新增 / 修改 skill / MCP / agent 時：

1. **撰寫**：依本檔規範產出檔案
2. **限縮**：每個工具與權限都應該被質疑「真的需要嗎」
3. **記錄**：在本檔對應清單（已接入 MCP / agents-catalog）新增條目
4. **同步**：依 [../../CLAUDE.md](../../CLAUDE.md) 「文件同步規則」更新相關檔
5. **驗證**：在 [../evals.md](../evals.md) 新增對應觸發情境的 eval
