---
title: Feature Development Workflow
purpose: 規範新功能從需求到上線的標準流程
applicable_when: 收到新功能需求、要開始開發新功能、要決定如何拆分任務
related_agents:
  - software-architect-blueprint
  - system-analyst
  - backend-engineer
  - frontend-architect
  - mobile-app-engineer
  - qa-test-engineer
  - code-review-optimizer
related_docs:
  - ../blueprints/_template.md
  - ../design/
  - code-review.md
  - qa-testing.md
keywords: [feature, 新功能, 開發, workflow, 流程]
last_updated: 2026-05-27
---

> 本檔是通用 [Research → Plan → Execute → Verify](research-plan-execute-verify.md) 框架的**新功能特化版**。

## 階段總覽

```
需求 → 藍圖 → 設計 → 實作 → 審查 → QA → 上線
   ↑R     ↑P     ↑P     ↑E     ↑V     ↑V    ↑V
```

## 0. 起手式：更新 status.md

- 先讀 [../status.md](../status.md) 了解全局
- 把新功能加入 **🔄 In Progress** 或 **📋 Backlog**
- 詳細規則見 [../../CLAUDE.md](../../CLAUDE.md) 「狀態追蹤規則」

## 1. 需求釐清

- **agent**：software-architect-blueprint
- **產出**：使用者流程、利害關係人、成功指標
- **澄清**：用 AskUserQuestion 而非假設

## 1.5 舊系統對照（API 任務必做 — forward）

- **適用**：所有 API endpoint 任務（CLAUDE.md 規則 10）
- **動作**：開 `reference/old/Ceremony/<Form>.cs` 找對應方法/事件；把業務邏輯、驗證、邊界 case 全部擷取
- **產出**：[../blueprints/api-endpoints/](../blueprints/api-endpoints/) `<verb>-<resource>.md`（複製 `_template.md`），填完「舊系統對照」段才能進下一步
- 同時更新 [../blueprints/api-endpoints/README.md](../blueprints/api-endpoints/README.md) 索引表

## 2. 建立 Blueprint

- **agent**：software-architect-blueprint
- **動作**：
  - 一般功能：複製 [../blueprints/_template.md](../blueprints/_template.md) 為 `<feature>.md`
  - **API 任務**：用 [../blueprints/api-endpoints/_template.md](../blueprints/api-endpoints/_template.md) 而非 `_template.md`
- **必填**：背景、範圍、跨層影響、驗收標準
- **更新**：[../blueprints/README.md](../blueprints/README.md) 索引表

## 3. 技術設計

- **agent**：system-analyst（總體規劃）+ 各層級 agent（細節）
- **動作**：依 blueprint「跨層影響」表，更新對應 design/ doc
  - 視覺改動 → [../design/visual-design.md](../design/visual-design.md)
  - 前端改動 → [../design/frontend-design.md](../design/frontend-design.md)
  - 後端 / API / DB 改動 → 對應 design/ doc
  - 安全相關 → [../design/security.md](../design/security.md)

## 4. 實作

- **agent**：依層級選 backend-engineer / frontend-architect / mobile-app-engineer
- **依據**：blueprint + design/ doc
- **同步**：實作中發現設計需調整 → **立即**回頭更新 blueprint 與 design/

## 5. 程式碼審查

- 依 [code-review.md](code-review.md) 流程
- **agent**：code-review-optimizer

## 6. QA / 測試

- 依 [qa-testing.md](qa-testing.md) 流程
- **agent**：qa-test-engineer
- 前端應用必跑 chrome-devtools-mcp 的 runtime 審查

## 6.5 反向覆蓋稽核（API 任務必做 — reverse）

- **適用**：所有 API endpoint 任務
- **動作**：開對應 [../blueprints/legacy-coverage/](../blueprints/legacy-coverage/) `<form>.md`
- 把已實作的方法/事件行勾為 `✅ 已實作`，連結回本 endpoint blueprint
- 故意捨棄的功能勾為 `❌ 故意捨棄` 並寫理由
- 更新 `coverage_percentage` 與 `last_audited`
- **PR 描述必含**：updated rows X-Y in `legacy-coverage/<form>.md`

## 7. 上線

- 部署遵循 [../design/infrastructure.md](../design/infrastructure.md)
- 更新 blueprint 的 `status: shipped` 與 `last_updated`
- 把功能從 [../status.md](../status.md) 的 In Progress 搬到 **✅ Recently Done**

## Definition of Done

- [ ] Blueprint 完整且 status=shipped
- [ ] 所有受影響的 design/ doc 已同步
- [ ] 通過 code review
- [ ] 通過 QA（含 runtime 審查）
- [ ] 通過 [security 檢核清單](../design/security.md)
- [ ] 監控 / 警報已配置
- [ ] **已對照舊 Form 邏輯**（forward，API 任務）— `api-endpoints/<file>.md` 完整
- [ ] **已更新對應 `legacy-coverage/<form>.md`**（reverse，API 任務）— 相關行勾為 ✅ 或 ❌（含理由）
- [ ] **無 secret 進 repo**（API 任務）— `grep -rE "Password=[^_<]" .` 在新增的 backend/ 內無命中
