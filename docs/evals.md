---
title: Harness Evals
purpose: harness 行為的可驗證 checklist；harness 變更後人工跑一遍確保沒退化
applicable_when: 改 CLAUDE.md / agent / workflow 後驗證、回報 harness 異常時對照、每月抽樣
related_agents: []
related_docs:
  - workflows/research-plan-execute-verify.md
  - design/tools-and-skills.md
  - ../CLAUDE.md
keywords: [evals, evaluation, regression, baseline, checklist, 驗證]
last_updated: 2026-05-07
---

## 為什麼

沒有 evals，harness 就只是一堆「希望」。任何規則改動都可能讓既有行為退化。本檔列出**可被人工驗證的具體情境**，作為 harness 改動後的 regression baseline。

> 對齊 [OpenAI Harness Engineering](https://openai.com/index/harness-engineering/) 提出的 evaluation 實踐，採輕量 markdown checklist 形式（不寫 JSONL trace 與自動化 runner，留待未來需要時再加）。

## 使用方式

| 何時跑 | 跑什麼 |
|---|---|
| 改 [CLAUDE.md](../CLAUDE.md) 後 | 「CLAUDE.md 規則 evals」全跑 |
| 新增 / 修改 agent 後 | 「Agent 路由 evals」相關項 |
| 新增 / 修改 workflow doc 後 | 「Workflow evals」相關項 |
| 每月抽樣（recommend） | 隨機抽 5 項全跑 |
| 回報 harness 異常時 | 從相關章節找對應情境，重現 |

**怎麼跑**：開新 Claude Code 會話 → 用 checklist 中的 prompt 對話 → 對照「預期行為」打勾或記錄落差。

**落差處理**：失敗就改 CLAUDE.md / 對應 doc 直到通過；無法修復的列入 [gotchas.md](gotchas.md)。

---

## 1. CLAUDE.md 規則 evals

### 1.1 文件同步規則
- [ ] **API 變更**：說「我改了 `/api/v1/users` endpoint，回傳結構變了」→ 預期 Claude 主動列出 [api-design.md](design/api-design.md) 與 [backend-design.md](design/backend-design.md) 需要更新
- [ ] **DB schema 變更**：說「我加了 `users.last_login_at` 欄位」→ 預期主動指向 [database-design.md](design/database-design.md)
- [ ] **跨層變更**：說「新增登入功能」→ 預期列出 frontend-design / backend-design / api-design / database-design / security 全部要看
- [ ] **無關變更**：說「修個 typo」→ 預期不會無中生有要求更新 design/

### 1.2 討論結果自動記錄
- [ ] **明確結論**：說「我們決定 TS 一律用 strict mode、不用 any」→ 預期自動 Edit [frontend-coding-style.md](design/frontend-coding-style.md) 對應段，更新 `last_updated`，並回報路徑
- [ ] **AskUserQuestion 回覆**：使用者選了 A 選項 → 預期把該決定記錄到對應 doc
- [ ] **不確定情境**：使用者隨口提一句沒明確結論 → 預期**不**亂寫 doc，必要時主動問「這要記嗎」

### 1.3 狀態追蹤
- [ ] **任務開始**：說「來做 X」→ 預期把 X 加到 [status.md](status.md) 的 🔄 In Progress
- [ ] **任務完成**：執行完一個任務 → 預期把它從 In Progress 搬到 ✅ Recently Done 並寫 Outcome
- [ ] **卡住**：說「等使用者回覆 design 問題」→ 預期搬到 🚧 Blocked 並註明 Blocker
- [ ] **新需求**：說「未來想加 Y」→ 預期加到 📋 Backlog 並標 P0/P1/P2

### 1.4 Memory 與專案隔離
- [ ] **本專案特有**：說「我們 auth 服務在 `/internal/refresh` 有特殊 quirk」→ 預期寫入專案內 doc 或 `.claude/agent-memory/`，**不**寫到 `~/.claude/agent-memory/`
- [ ] **跨專案通用**：說「React Hook Form 的 register 常見坑」→ 預期寫到 user-scope agent memory
- [ ] **誤判時要拒絕**：使用者誤要求把專案特有資訊寫 user-scope → 預期禮貌指出並建議正確位置

---

## 2. Agent 路由 evals

對每個任務類型驗證 agent 選擇正確：

- [ ] **「審 PR」** → 預期推薦 `code-review-optimizer`
- [ ] **「找品質問題、不要改 code」** → 預期推薦 `qa-test-engineer`
- [ ] **「設計 RESTful API」** → 預期推薦 `backend-engineer`
- [ ] **「Vue composable」** → 預期推薦 `frontend-architect`
- [ ] **「Flutter widget」** → 預期推薦 `mobile-app-engineer`
- [ ] **「multi-tenant DB schema」** → 預期推薦 `system-analyst` 或 `backend-engineer`
- [ ] **「商品列表頁版型」** → 預期推薦 `visual-design-architect`
- [ ] **「電商平台需求轉技術規格」** → 預期推薦 `software-architect-blueprint`
- [ ] **錯置防呆**：要求 `qa-test-engineer` 改 code → 預期被拒絕並建議改用其他 agent

---

## 3. Workflow evals

每個 workflow doc dry-run 一個場景，確認 agent 會走完整流程：

- [ ] **feature-development**：「來開發會員登入功能」→ 預期走 RPEV 四階段、加 status.md In Progress、複製 `_template.md` 建 blueprint、列出受影響的 design/ doc
- [ ] **bug-fix**：「production 結帳 500」→ 預期先重現 → 找根因 → 修復 → 驗證 → 把踩雷加入 [gotchas.md](gotchas.md)
- [ ] **code-review**：「幫我審這份 PR」→ 預期依 [code-review.md](workflows/code-review.md) 八步審查，並對照 [security.md](design/security.md) 檢核清單
- [ ] **qa-testing**（前端）：「驗收新頁面」→ 預期用 chrome-devtools-mcp 跑 console / network / performance / a11y 四面向

---

## 4. RPEV 框架 evals

- [ ] **R**：交付任務時，Claude 是否先 Explore / 讀 status.md，再動手？
- [ ] **P**：非 trivial 任務是否提出 plan（口頭或 plan mode），列關鍵檔案？
- [ ] **E**：執行中偏離 plan 是否主動回頭更新而非偷跑？
- [ ] **V**：完成後是否主動驗證（lint / test / 對照 evals.md），而非只說「已完成」？

---

## 5. 退化偵測（Regression flags）

每月或大改後跑：

- [ ] CLAUDE.md ≤ 200 行（`wc -l CLAUDE.md`）
- [ ] 所有 doc 含 frontmatter（`grep -L "^---$" docs/**/*.md` 應無輸出）
- [ ] 所有 `related_agents` 引用的 agent 實際存在於 `~/.claude/agents/`
- [ ] CLAUDE.md 路由表中的 agent 名稱無 typo
- [ ] CLAUDE.md 同步規則表的 doc 路徑都指向實際存在的檔案
- [ ] design/ 下每份 doc 都被 CLAUDE.md 同步規則表引用
- [ ] status.md 沒有「殘留 In Progress 超過 30 天」的項目（代表卡住沒搬到 Blocked）

---

## 6. 失敗紀錄

> 跑 evals 時發現的退化記錄於此，附修復連結。

| 日期 | 失敗項目 | 表現 | 修復 |
|---|---|---|---|
| – | – | – | – |
