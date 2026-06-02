---
title: Harness Overview
purpose: 說明本 harness 的定位、設計理念、適用情境
applicable_when: 第一次接觸本 harness、想理解整體哲學、要決定是否套用
related_agents: []
related_docs:
  - architecture.md
  - installation.md
keywords: [overview, 定位, 哲學, 為什麼]
last_updated: 2026-05-07
---

## 是什麼

本 harness 是一套**可移植的 Claude Code 文件骨架**，提供：

- 標準化的文件結構（CLAUDE.md 為入口，其他細節分散到 docs/）
- agent 路由表（任務 → 適合的 agent）
- 文件同步規則（變更程式碼時自動提示要更新哪些 doc）
- 設計層級分層（視覺 / 前端 / 後端 / API / DB / 基礎建設 / 安全）
- 功能藍圖樣板（每個重大功能一份）

## 為什麼

Claude Code 在大型專案中常見痛點：

1. **CLAUDE.md 越長越難維護**：所有 context 塞一個檔，吃 token 預算又難以掃讀
2. **doc 與 code 脫節**：改了程式碼但忘記更新文件，文件腐化
3. **不知道該叫哪個 agent**：分工模糊導致任務失去最佳化
4. **跨專案沒有一致經驗**：每個專案重新發明輪子

本 harness 用「精簡入口 + 分層細節 + 同步規則 + 路由表」四件套對症下藥。

## 適用情境

- 個人或團隊使用 Claude Code 進行多專案開發
- 希望 Claude 能在不同專案間保持一致的工作流程
- 需要將設計、實作、文件三者保持同步

## 不適用

- 一次性原型 / scratch 專案
- 不使用 Claude Code 的純人類團隊（部分結構仍可參考，但同步規則對 Claude 才有意義）

## 設計理念

- **機器讀者優先**：穩定路徑、可解析 metadata、明確關鍵字
- **單一真實來源**：agent 名稱只在 CLAUDE.md 路由表 + frontmatter 維護
- **同步而非自動化**：先用文字規則約束 Claude 行為，避免初期過度依賴 hook
