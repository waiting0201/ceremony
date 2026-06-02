---
title: GET /api/v1/believers/:id
purpose: 取得單筆信眾完整資料（編輯預填用）
status: shipped
endpoint: get-believer-by-id
http_method: GET
route: /api/v1/believers/:id
legacy_form: BelieverForm.cs
legacy_lines: 57-99
related_agents:
  - backend-engineer
related_docs:
  - get-believers.md
  - ../legacy-coverage/believer-form.md
keywords: [believers, get, prefill, edit]
last_updated: 2026-05-27
---

## 規格

`GET /api/v1/believers/{id:guid}`，需要 JWT。

### Response

200：`BelieverListItem`（與 GET /believers 同形狀，但單筆）
404：`{"errorCode":"BELIEVER_NOT_FOUND","message":"找不到信眾"}`

## 舊系統對照

| 舊方法/事件 | 行 | 行為 |
|---|---|---|
| `BelieverForm.dgvBelievers_CellClick` | 57-99 | 點選 grid 列後 `GetByID(BelieverID)` 並把全部欄位填入表單 |

新版：HTTP GET 取代 `GetByID` 呼叫；UI 預填邏輯由前端 BelieverEditModal 處理。

## 邊界 case

| 場景 | 行為 |
|---|---|
| id 不存在 | 404 |
| id 格式錯誤 (非 GUID) | 400（ASP.NET routing 自動） |
| 無 JWT | 401 |

## 驗收

- [x] 對應 [believer-form.md](../legacy-coverage/believer-form.md) row 4 ✅
- [x] 含 integration test (404 case)
