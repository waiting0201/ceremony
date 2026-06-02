---
title: PUT /api/v1/believers/:id
purpose: 編輯既有信眾（全欄位覆寫）
status: shipped
endpoint: put-believer
http_method: PUT
route: /api/v1/believers/:id
legacy_form: BelieverForm.cs
legacy_lines: 154-185
related_agents:
  - backend-engineer
related_docs:
  - post-believers.md
  - ../legacy-coverage/believer-form.md
keywords: [believers, update, put]
last_updated: 2026-05-27
---

## 規格

`PUT /api/v1/believers/{id:guid}`，需要 JWT。Body 同 [post-believers.md](post-believers.md) `BelieverUpsertRequest`。

### Response

`200 OK` + `BelieverListItem`。

### 錯誤碼

| HTTP | errorCode | message | 觸發 |
|---|---|---|---|
| 400 / 同 POST | 同 POST | 同 POST | 同 POST |
| 404 | `BELIEVER_NOT_FOUND` | `找不到信眾` | id 不存在 |

## 舊系統對照

| 舊方法/事件 | 行 | 對應新版 |
|---|---|---|
| `btnConfirm_Click` edit path | 154-184 | `UpdateBelieverHandler.HandleAsync` |
| `GetByID + 逐欄位賦值 + SaveChanges` | 157-181 | Dapper UPDATE 全欄位 |

新版用「全欄位覆寫」（PUT 慣例），不做欄位 diff。

## 驗收

- [x] 對應 [believer-form.md](../legacy-coverage/believer-form.md) row 5 (edit path) ✅
- [x] 含 integration test (404 / 200)
