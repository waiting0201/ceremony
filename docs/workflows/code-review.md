---
title: Code Review Workflow
purpose: 規範 PR / code review 的執行步驟與品質標準
applicable_when: 收到 PR review 請求、要審剛寫好的 code、要評估重構
related_agents:
  - code-review-optimizer
  - qa-test-engineer
related_docs:
  - ../conventions.md
  - ../gotchas.md
  - qa-testing.md
keywords: [code review, pr, 審查, refactor]
last_updated: 2026-05-07
---

## 兩個 agent 的分工

| agent | 角色 | 會修 code 嗎 |
|---|---|---|
| code-review-optimizer | 主審，提改善建議與重構方向 | **會**（依使用者要求） |
| qa-test-engineer | 品質把關，找 bug / 邊界 / 安全 | **絕對不會** |

複雜 PR 可同時叫兩個 agent 互補審查。

## 審查順序

1. **理解意圖**：讀 PR 描述、blueprint、commit message
2. **確認 scope**：差異是否符合 PR 描述？有沒有偷渡無關改動？
3. **正確性**：邏輯、邊界條件、錯誤處理
4. **安全**：依 [../design/security.md](../design/security.md) 檢核清單
5. **效能**：N+1、大 OFFSET、不必要的迴圈
6. **可讀性**：命名、結構、註解
7. **測試**：覆蓋關鍵路徑與邊界
8. **文件同步**：依 [../../CLAUDE.md](../../CLAUDE.md) 「文件同步規則」
9. **lint / type-check / test 全綠**

## Reviewer 行為準則

- 區分**必改**（blocker）與**建議**（nit）
- 對事不對人
- 提供具體替代做法（除非是 qa-test-engineer 角色）
- 引用 doc 連結而非空泛規則

## Author 回應準則

- 每條 comment 都要回應（接受 / 反駁 / 後續處理）
- 重大重構建議：開新 issue 而非阻擋本 PR

## 合併條件

- [ ] 至少一位 reviewer approve
- [ ] CI 全綠（lint / type / test）
- [ ] 衝突已解
- [ ] 受影響的 design/ doc 已同步
- [ ] 若是新功能：blueprint status 已更新

## 對應 RPEV

本流程對應 [research-plan-execute-verify.md](research-plan-execute-verify.md) 的 **V (Verify)** 階段：對外部產出（PR）做系統性審查。
