---
title: Installation
purpose: 說明如何將本 harness 套用到新專案
applicable_when: 要在新專案中啟用本 harness、要更新既有專案的 harness 版本
related_agents: []
related_docs:
  - overview.md
  - architecture.md
keywords: [installation, 安裝, 套用, setup, bootstrap]
last_updated: 2026-05-07
---

## 三種套用方式

### A. 直接複製（最簡單）

```bash
cp -r /path/to/harmess/* /path/to/new-project/
```

優點：完全獨立、可任意客製
缺點：harness 升級時需手動 merge

### B. Git submodule

```bash
cd /path/to/new-project
git submodule add <harness-repo-url> .harness
ln -s .harness/CLAUDE.md CLAUDE.md
ln -s .harness/docs docs
```

優點：可拉新版本
缺點：客製內容要小心 conflict

### C. Symlink（本機開發）

```bash
ln -s /Users/tim/agents/harmess/CLAUDE.md /path/to/new-project/CLAUDE.md
ln -s /Users/tim/agents/harmess/docs /path/to/new-project/docs
```

優點：harness 改一次所有專案受惠
缺點：不適合跨機器、跨團隊

## 套用後必做

1. **替換 placeholder**：grep `<PROJECT_NAME>` / `<placeholder>` 等，填入專案實際資訊
2. **檢視 agent 路由表**：移除專案不會用到的條目（例如純後端專案可移除 mobile、visual-design）
3. **檢視同步規則表**：移除不適用的條目（例如沒有 DB 的專案可移除 database-design 列）
4. **建立第一份 blueprint**：複製 `docs/blueprints/_template.md` 為當前主要功能

## 升級流程

當 harness 樣板有新版本：

1. diff 對照新舊 CLAUDE.md
2. 重點檢視新增的「同步規則」與「路由表」條目
3. 各 doc 的 frontmatter schema 是否有變動（見 [architecture.md](architecture.md)）
