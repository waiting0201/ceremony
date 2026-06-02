---
title: Blueprints Index
purpose: 列出所有功能藍圖，方便查找與了解專案功能全貌
applicable_when: 想了解專案有哪些功能、要找特定功能的設計文件、要新增功能藍圖
related_agents:
  - software-architect-blueprint
related_docs:
  - _template.md
  - ../architecture.md
keywords: [blueprints, 藍圖, 功能, 索引]
last_updated: 2026-05-26
---

## 如何新增藍圖

1. 複製 [_template.md](_template.md) 為 `<feature-name>.md`
2. 填入 frontmatter（特別注意 `related_agents` / `related_docs` / `keywords`）
3. 在本檔加入下方表格條目
4. 開發過程中**持續更新** `status` 與 `last_updated`

## 已存在的 Blueprint

法會報名系統重構藍圖（依模組劃分）：

| 功能 | Status | 主要 agent | 連結 | 最後更新 |
|---|---|---|---|---|
| 認證與管理員維護 | draft | backend-engineer / frontend-architect | [auth-and-admin.md](auth-and-admin.md) | 2026-05-26 |
| 信眾維護 | draft | backend-engineer / frontend-architect | [believer-management.md](believer-management.md) | 2026-05-26 |
| 法會分類維護 | draft | backend-engineer / frontend-architect | [ceremony-category.md](ceremony-category.md) | 2026-05-26 |
| 報名管理（搜尋/新增/編輯/歷程） | draft | backend-engineer / frontend-architect | [signup-management.md](signup-management.md) | 2026-05-26 |
| 載入預繳 | draft | backend-engineer | [prepay-loading.md](prepay-loading.md) | 2026-05-26 |
| 列印與報表 | draft | backend-engineer / visual-design-architect | [printing-reports.md](printing-reports.md) | 2026-05-26 |
| RDLC 位置參考（19 模板逐欄位 cm 座標） | reference | backend-engineer | [printing-reports-positions.md](printing-reports-positions.md) | 2026-05-26 |
| 資料遷移（deprecated — DB 完全凍結，無需 migration）| deprecated | backend-engineer | [data-migration.md](data-migration.md) | 2026-05-26 |

## Status 定義

- **draft**：規劃中，尚未開發
- **in-progress**：開發中
- **shipped**：已上線
- **deprecated**：已廢棄（保留紀錄供查詢）
