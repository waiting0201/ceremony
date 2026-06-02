---
title: Research → Plan → Execute → Verify (RPEV)
purpose: 跨任務通用的四階段執行框架；feature / bugfix / refactor 都套用，避免 agent「先寫了再說」
applicable_when: 開始任何非 trivial 任務前；feature-development / bug-fix / refactor 等都從這裡分支
related_agents:
  - software-architect-blueprint
related_docs:
  - feature-development.md
  - bug-fix.md
  - code-review.md
  - qa-testing.md
  - ../evals.md
keywords: [rpev, research, plan, execute, verify, 結構化執行, framework]
last_updated: 2026-05-07
---

## 為什麼

Agent 最常見的失敗模式是「先動手再說」：跳過理解直接 code、跳過計畫直接改、跳過驗證就回報完成。RPEV 強制四階段分隔，每階段有明確產出與終止條件。

> 對齊 [OpenAI Harness Engineering](https://openai.com/index/harness-engineering/) 提出的 structured execution 支柱。

## 四階段

### R — Research（理解）

**目標**：在動手前知道「現況是什麼、相似實作在哪、需求邊界在哪」。

**動作**
- 讀 [../status.md](../status.md) 與 [../CLAUDE.md](../CLAUDE.md) 路由表
- 用 Explore / grep 找相似實作，**避免重新發明輪子**
- 讀對應 [../design/](../design/) doc 與相關 [../blueprints/](../blueprints/)
- 需求模糊時用 AskUserQuestion **而非假設**

**終止條件**：能用一段話說清楚「現況 + 目標 + 已知約束」。

### P — Plan（規劃）

**目標**：在改檔前產出可被審視的方案。

**動作**
- 列關鍵檔案路徑與變更性質
- 標記取捨（為什麼選 A 不選 B）
- 評估跨層影響：對照 [../CLAUDE.md](../CLAUDE.md) 「文件同步規則」表
- 大型任務進入 plan mode；小任務可口頭計畫

**終止條件**：使用者批准（plan mode 用 ExitPlanMode；非 plan mode 用文字陳述讓使用者點頭）。

### E — Execute（執行）

**目標**：依 plan 落實，**偏離必回頭**。

**動作**
- 依計畫順序動手；每個小步驟做完再下一個
- 偏離 plan 時：先停下、評估、更新 plan（plan mode 中即更新檔案；非 plan mode 中明確告知使用者）
- 同步更新 [../status.md](../status.md)：移到 In Progress、寫 Notes
- 每次 Edit / Write 後留意是否觸發 [../CLAUDE.md](../CLAUDE.md) 「文件同步規則」

**終止條件**：plan 上每個項目都已落地。

### V — Verify（驗證）

**目標**：確認真的有用，不是「看起來有跑」。

**動作**
- 跑 lint / type-check / test（依專案技術棧）
- 對照 [../evals.md](../evals.md) 相關 checklist
- 前端應用走 [qa-testing.md](qa-testing.md)（含 chrome-devtools runtime 審查）
- 端到端走一遍主要流程
- 確認所有受影響 doc 已同步（[../CLAUDE.md](../CLAUDE.md) 同步規則）

**終止條件**：可用一段話說清楚「我做了什麼 + 怎麼驗證 + 結果」。

## 跨階段紀律

- **每階段都更新 [status.md](../status.md)**：In Progress 內的 Notes 反映目前在哪一階段
- **plan 偏離要記錄**：在 commit message 或 PR 描述註明（讓未來能追溯為何偏離）
- **不可跳階段**：尤其 R 和 V，這兩個最常被偷跳
- **小任務也走簡化版**：哪怕只是 typo 修復，至少 R（確認沒踩到別處）+ V（看一眼結果）

## 與專門 workflow 的關係

| 任務類型 | 對應 workflow | RPEV 對應 |
|---|---|---|
| 新功能 | [feature-development.md](feature-development.md) | 七步流程 = RPEV 的 R/P → E → V 展開 |
| Bug 修復 | [bug-fix.md](bug-fix.md) | 重現/根因 = R；修復方案 = P；修復 = E；驗證 = V |
| 程式碼審查 | [code-review.md](code-review.md) | 對應 V（外部對 PR 的驗證） |
| QA 測試 | [qa-testing.md](qa-testing.md) | 對應 V（系統性驗證） |

專門 workflow 是 RPEV 的特化版；遇到不在表內的任務型態，直接走通用 RPEV。
