---
title: DELETE /api/v1/signups/:id
purpose: 刪除報名（硬刪除，無 IsEnabled flag）
status: shipped
endpoint: delete-signup
http_method: DELETE
route: /api/v1/signups/:id
legacy_form: SignupForm.cs
legacy_lines: 405-426
related_agents:
  - backend-engineer
related_docs:
  - ../legacy-coverage/signup-form.md
keywords: [signups, delete, hard-delete]
last_updated: 2026-05-27
---

## 規格

`DELETE /api/v1/signups/{id:guid}`，需要 JWT。

### Response: `204 No Content`。

### 錯誤碼

| HTTP | errorCode | 觸發 |
|---|---|---|
| 404 | `SIGNUP_NOT_FOUND` | id 不存在 |

## 舊系統對照

| 舊方法/事件 | 行 | 對應 |
|---|---|---|
| `SignupForm.tsmiDelete_Click` | 405-426 | `DeleteSignupHandler.HandleAsync` |
| Multi-select 整批刪除 | 410-415 | API 單筆；前端逐個呼叫 |
| Hard delete (Signups 無 IsEnabled) | 412 | `DELETE FROM Signups WHERE SignupID=@id` |

### 業務規則

- **硬刪除**：Signups 表無 IsEnabled，對齊舊行為
- **SignupLog 保留**：依 [database-design.md §6](../../design/database-design.md) 設計，log 表無 FK 關聯，原 Signup 刪除後 log 仍存（審計需要）
- **無連帶刪除**：Signups 是葉節點，刪除不影響其他資料

## 驗收

- [x] 對應 [signup-form.md](../legacy-coverage/signup-form.md) row 14 ✅
- [x] 含 integration test (404 / 204)
