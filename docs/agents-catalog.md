---
title: Agents Catalog
purpose: 列出所有可用的 Claude Code agent 與其用途、適用場景
applicable_when: 要選擇 agent、要查 agent 能力、要新增專案層級 agent
related_agents: []
related_docs:
  - ../CLAUDE.md
keywords: [agents, catalog, 清單, sub-agent, 路由]
last_updated: 2026-05-07
---

## User-Scope Agents（位於 ~/.claude/agents/，跨專案共用）

### backend-engineer
- **用途**：後端開發（API、DB、雲端）
- **語言**：C# / Python / PHP
- **特長**：EF Core / Dapper / SQL 優化、Azure / AWS、安全與效能
- **典型任務**：設計 RESTful API、撰寫資料存取層、SQL 優化、雲端部署架構

### frontend-architect
- **用途**：前端開發
- **技術**：Angular / Vue / React / TypeScript / HTML / CSS
- **特長**：元件設計、狀態管理、路由、效能優化、無障礙
- **典型任務**：建 shared module、寫 composable、JS→TS 遷移

### mobile-app-engineer
- **用途**：行動 App 開發
- **平台**：Flutter（Dart）/ iOS（Swift）/ Android（Java/Kotlin）
- **特長**：跨平台架構、device API（相機 / GPS / 推播）、平台通道
- **典型任務**：寫 Flutter widget、實作推播、debug 平台特定問題

### code-review-optimizer
- **用途**：程式碼審查、重構建議、品質改善
- **特長**：找 code smell、設計模式、效能優化、可讀性
- **典型任務**：審 PR、提重構建議、分析效能瓶頸

### qa-test-engineer
- **用途**：QA 品質審查（**不修改程式碼**）
- **特長**：找 bug、邊界條件、靜態分析（lint）、瀏覽器 runtime 審查（chrome-devtools-mcp）
- **典型任務**：審查程式碼品質、找測試盲點、Web Vitals 檢查

### software-architect-blueprint
- **用途**：產品藍圖、系統設計、開發藍圖
- **特長**：需求分析、使用流程設計、架構規劃
- **典型任務**：把客戶需求轉技術規格、設計新系統架構

### system-analyst
- **用途**：將藍圖轉技術文件（架構 / DB / API 規劃）
- **特長**：系統架構設計、DB schema、API 結構、技術文件
- **典型任務**：multi-tenant DB 設計、RESTful API 規劃、技術規格文件

### visual-design-architect
- **用途**：UI/UX 設計、版型規劃、視覺設計決策
- **特長**：wireframing、layout、design system、視覺層級
- **典型任務**：設計商品列表頁版型、後台介面重設計

## Built-in Agents（Claude Code 內建）

- **Explore**：快速 read-only 搜尋（找檔案、grep 符號）
- **Plan**：軟體架構規劃（設計實作策略）
- **general-purpose**：開放式研究、多步驟任務
- **statusline-setup**：設定 status line
- **claude-code-guide**：Claude Code / SDK / API 使用問題

## 專案層級 Agent（選用）

若專案有特殊需求，可在 `.claude/agents/<name>.md` 新增專案專屬 agent。新增後必須：

1. 在本檔加入條目（含用途、特長、典型任務）
2. 在 [../CLAUDE.md](../CLAUDE.md) 路由表加上對應條目
3. 在相關 design / workflow doc 的 `related_agents` 引用
