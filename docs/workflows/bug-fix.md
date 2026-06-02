---
title: Bug Fix Workflow
purpose: 規範 bug 從回報到修復、預防再發生的標準流程
applicable_when: 收到 bug 回報、要修 bug、要寫事故報告
related_agents:
  - qa-test-engineer
  - backend-engineer
  - frontend-architect
  - mobile-app-engineer
  - code-review-optimizer
related_docs:
  - code-review.md
  - qa-testing.md
  - ../gotchas.md
keywords: [bug, fix, 修復, hotfix, incident, 事故]
last_updated: 2026-05-07
---

> 本檔是通用 [Research → Plan → Execute → Verify](research-plan-execute-verify.md) 框架的 **bug 特化版**。

## 階段

```
重現 → 根因 → 修復 → 驗證 → 預防
 ↑R     ↑R     ↑E     ↑V    ↑V (記入 gotchas)
```

## 1. 重現

- 收集：環境、版本、操作步驟、預期 vs 實際
- 在本地或 dev 環境重現
- 若無法重現：先補 log / 觀測，不要硬猜

## 2. 找根因

- **不要只修表象**：找到根本原因再決定修哪一層
- **agent**：依層級選；複雜 bug 可叫 qa-test-engineer 先做品質審查
- 工具：debugger、log、APM、git bisect

## 3. 修復

- **agent**：依領域選 backend / frontend / mobile
- 修最少必要的 code
- **不要**順便重構（除非根因就是重構需求）
- 加測試證明 bug 不再發生

## 4. 驗證

- 跑相關測試
- 在重現環境驗證
- 必要時走 [qa-testing.md](qa-testing.md)

## 5. 預防

- 把這次踩雷加入 [../gotchas.md](../gotchas.md)
- 思考是否有同類風險（grep 找類似程式碼）
- 若是設計問題 → 同步更新對應 [../design/](../design/) doc

## Hotfix 特例

production 緊急修復：

1. 從 main 切 `hotfix/<short-desc>`
2. 最小修復 → 直接合 main 並部署
3. **事後**補：測試、blueprint 更新、gotchas 條目

## Commit / PR 標題

- `fix(<scope>): <短描述>`
- PR 描述含：根因、修復方式、影響範圍、驗證步驟
