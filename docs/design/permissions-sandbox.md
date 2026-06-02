---
title: Permissions & Sandbox Policy
purpose: 定義 Claude Code 工具權限、sandbox 啟用準則、危險指令分類；最小權限原則
applicable_when: 設定 .claude/settings.json、新增 MCP server、agent 要動危險指令時、check 是否 over-privileged
related_agents:
  - backend-engineer
  - code-review-optimizer
related_docs:
  - security.md
  - tools-and-skills.md
keywords: [permissions, sandbox, allow, deny, ask, 權限, 沙箱, 工具限縮, agent-security]
last_updated: 2026-05-07
---

## 與 [security.md](security.md) 的差別

| 文件 | 規範對象 | 例子 |
|---|---|---|
| [security.md](security.md) | **人類使用者**對產品的存取 | OAuth、密碼策略、PII 加密 |
| 本檔 | **Claude / agent** 對工具與檔案系統的存取 | 哪些 Bash 命令需要 ask、MCP server 白名單、sandbox |

兩者互補但不可混為一談。

## 1. 最小權限原則

- **預設 deny**；明確需要才 allow
- 升級權限要有理由（PR 描述或 commit message 註明）
- 個人開發機與 CI / production 應採用**不同**的 settings 檔

## 2. Permissions 三層

[Claude Code settings.json](../../.claude/settings.json) 的 `permissions` 區塊：

```json
{
  "permissions": {
    "allow": ["Bash(git status)", "Bash(git diff *)", "Read(*)"],
    "ask":   ["Bash(git push *)", "Bash(npm publish *)"],
    "deny":  ["Bash(rm -rf *)", "Edit(~/.ssh/*)", "Bash(curl * | sh)"]
  }
}
```

| 層級 | 行為 | 用途 |
|---|---|---|
| `allow` | 直接執行不問 | 高頻、低風險、read-only |
| `ask` | 每次提示確認 | 中風險或不可逆 |
| `deny` | 直接拒絕 | 危險操作 |

`additionalDirectories`：擴充 Claude 可讀寫的目錄範圍（預設只有 cwd）。

## 3. 危險指令分類表

### 🔴 高風險（必 deny 或 ask）
- `rm -rf *`、`rm -rf <絕對路徑>`
- `git push --force` / `git push -f`（除非明確需要 force-with-lease）
- `git reset --hard`、`git clean -fd`
- `gh pr merge`、`gh release create`
- `npm publish`、`pip publish`、`gem push`
- `Edit / Write` 於 `~/.ssh/`、`~/.aws/`、`/etc/`
- `Bash(curl * | sh)` / `Bash(wget * | bash)`（直接執行下載內容）
- DB schema migration 寫入（production）
- `kubectl delete`、`terraform destroy`

### 🟡 中風險（依情境 ask）
- `git push`（非 force）
- `git rebase -i`
- `git commit --amend`（已 push 過要謹慎）
- 檔案系統大規模變更（mv / 大量 rename）
- 跨網域 API 呼叫
- 安裝套件（`npm install`、`pip install`）
- `docker run` / `docker compose up` 含 volume mount

### 🟢 低風險（可 allow）
- 所有 read-only：`ls`、`cat`、`grep`、`find`、`git status`、`git log`、`git diff`、`pwd`
- 本地測試：`npm test`、`pytest`、`go test`
- Lint / type-check（read-only）
- Read tool 對任何檔案
- `mkdir -p`（建目錄不刪檔）

## 4. Sandbox 啟用準則

當需要更強隔離時（例如執行不信任程式碼、跑 untested migration），啟用 sandbox：

```json
{
  "sandbox": {
    "enabled": true,
    "network": {
      "allowedDomains": ["api.openai.com", "*.anthropic.com"],
      "allowLocalBinding": true
    },
    "filesystem": {
      "denyWrite": ["~/.ssh/*", "~/.aws/*", "/etc/*"],
      "denyRead":  ["~/.aws/credentials"]
    }
  }
}
```

| 情境 | 建議設定 |
|---|---|
| 本機個人開發 | sandbox off，靠 permissions 把關 |
| 跑不信任 code（範例專案、第三方 PR） | sandbox on + 嚴格 network allowlist |
| CI runner | sandbox on + 完全 deny 寫入敏感目錄 |

## 5. MCP Server 權限

- **預設**：新 MCP server 需手動批准（不要 `enableAllProjectMcpServers: true`）
- **白名單**：用 `enabledMcpjsonServers: ["chrome-devtools", "..."]` 列出明確允許
- **黑名單**：`disabledMcpjsonServers` 列出明確拒絕（覆蓋全部）
- User-scope MCP（`~/.claude.json`，由 `claude mcp add --scope user` 維護）需在 [tools-and-skills.md](tools-and-skills.md) 文件化用途

## 6. 設定樣板

`.claude/settings.json`（commit 進 repo，團隊共用）：

```json
{
  "permissions": {
    "allow": [
      "Bash(git status)", "Bash(git diff *)", "Bash(git log *)",
      "Bash(ls *)", "Bash(pwd)", "Bash(grep *)", "Bash(find *)",
      "Read(*)"
    ],
    "ask": [
      "Bash(git push *)",
      "Bash(npm publish *)",
      "Bash(git commit --amend *)"
    ],
    "deny": [
      "Bash(rm -rf *)",
      "Bash(git push --force*)",
      "Edit(~/.ssh/*)",
      "Edit(~/.aws/*)",
      "Bash(curl * | sh)",
      "Bash(curl * | bash)"
    ]
  },
  "enabledMcpjsonServers": []
}
```

`.claude/settings.local.json`（**gitignore**，個人覆寫）：

```json
{
  "permissions": {
    "allow": ["Bash(npm test *)", "Bash(pytest *)"]
  }
}
```

## 7. 變更流程

新增 / 移除 / 升級權限的流程：

1. **提出**：在 PR 描述說明「為什麼需要這條權限」
2. **審查**：[code-review-optimizer](../agents-catalog.md) 檢視，特別看 deny 是否被弱化
3. **記錄**：本檔的「危險指令分類表」若有新增條目要更新
4. **同步**：依 [../../CLAUDE.md](../../CLAUDE.md) 「文件同步規則」更新本檔 + commit
