---
title: GET /api/v1/signups/:id
purpose: 取得單筆報名完整資料（編輯預填用）
status: shipped
endpoint: get-signup-by-id
http_method: GET
route: /api/v1/signups/:id
legacy_form: EditSignupForm.cs
legacy_lines: 70-73, 562-626
related_agents:
  - backend-engineer
related_docs:
  - get-signups.md
  - ../legacy-coverage/edit-signup-form.md
keywords: [signups, get, prefill]
last_updated: 2026-05-27
---

## 規格

`GET /api/v1/signups/{id:guid}`，需要 JWT。

### Response

200 → `SignupListItem`（與 GET /signups 同形狀，但單筆）
404 → `{"errorCode":"SIGNUP_NOT_FOUND","message":"找不到報名"}`

## 舊系統對照

| 舊方法/事件 | 行 | 對應 |
|---|---|---|
| `EditSignupForm_Load` | 70-73 | controller load → GET |
| `BelieverSelected()` 編輯模式 | 562-626 | API 回傳全欄位後，前端 BelieverSelected 邏輯由前端 form prefill 取代 |

### 邊界

- id 不存在 → 404
- id 非 GUID → 400 (routing 自動)

## 驗收

- [x] 對應 [edit-signup-form.md](../legacy-coverage/edit-signup-form.md) row 2 ✅
- [x] 含 integration test (404 case)
